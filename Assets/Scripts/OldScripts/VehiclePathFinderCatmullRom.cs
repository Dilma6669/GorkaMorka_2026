using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for .OrderBy() and .Any()

public class VehiclePathFinderCatmullRom : MonoBehaviour // Renamed class to reflect capital 'F'
{
    [Header("Pathfinding Settings")]
    [Tooltip("Maximum height difference allowed between adjacent hexagons for movement")]
    public float maxClimbHeight = 2f;
    
    [Tooltip("Cost multiplier for moving uphill (higher = more expensive)")]
    public float uphillCostMultiplier = 1.5f;
    
    [Tooltip("Cost multiplier for moving downhill (lower = cheaper)")]
    public float downhillCostMultiplier = 0.8f;
    
    [Tooltip("Cost multiplier for moving through rocky terrain")]
    public float rockTerrainCostMultiplier = 1.3f;

    [Tooltip("Additional cost for turning (left or right turns). Increase this value to make pathfinding prefer straight lines.")]
    public float turningCostPenalty = 1.5f; 

    [Tooltip("Minimum number of straight hexagons the vehicle must move before it is allowed to pivot or turn. Set to 0 to allow turning at any step.")]
    public int minStraightMovesBeforeTurn = 0; 

    [Tooltip("Maximum world distance (radius) a vehicle can move from its starting point.")]
    public float maxMovementRange = 10f; 

    [Tooltip("Cost penalty for final facing direction that doesn't match the ideal trajectory direction. Higher values make the vehicle prioritize ending up facing the 'arrow' direction.")]
    public float finalOrientationCostPenalty = 2f; // NEW: Cost penalty for misalignment with target direction
    
    [Header("Movement Settings")]
    [Tooltip("Speed of movement along the path")]
    public float movementSpeed = 5f;
    
    [Tooltip("How smoothly the object rotates to face movement direction")]
    public float rotationSpeed = 180f; // Degrees per second for rotation
    
    [Tooltip("Height offset above terrain for the moving object")]
    public float heightOffset = 0.5f;
    
    [Tooltip("Enable smooth height interpolation between hexagons")]
    public bool smoothHeightTransition = true; // This will be handled by spline now
    
    [Header("Catmull-Rom Spline Settings")]
    [Tooltip("Higher values make the spline straighter by simplifying the path more aggressively. This is an angle in degrees. Set to 0 for no simplification.")]
    [Range(0f, 60f)] // Angle can range from 0 to 60 degrees (a full hex turn)
    public float splineAngleTolerance = 10f; // NEW: Public variable for spline strength
    
    private HexagonController hexController;
    
    // --- A* specific data structures ---
    private Dictionary<VehicleState, float> gCost; // Cost from start to this node
    private Dictionary<VehicleState, float> hCost; // Heuristic cost from this node to target
    private Dictionary<VehicleState, VehicleState> cameFrom; // Parent node in the path
    private HashSet<VehicleState> closedSet; // Nodes already evaluated
    private List<VehicleState> openSet; // Nodes to be evaluated

    // Stores the currently running movement coroutine for a specific GameObject
    private Dictionary<VehicleController, Coroutine> activeMoveCoroutines = new Dictionary<VehicleController, Coroutine>();
    
    // Represents a vehicle's position, facing direction, AND consecutive straight moves
    public struct VehicleState : System.IEquatable<VehicleState> 
    {
        public Vector2Int gridPos; 
        public int facingDirection; 
        public int straightMovesCount; 
        
        public VehicleState(Vector2Int pos, int facing, int straightCount)
        {
            gridPos = pos;
            facingDirection = facing;
            straightMovesCount = straightCount;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is VehicleState other)
            {
                return Equals(other);
            }
            return false;
        }

        public bool Equals(VehicleState other) 
        {
            return gridPos == other.gridPos && 
                   facingDirection == other.facingDirection &&
                   straightMovesCount == other.straightMovesCount;
        }
        
        public override int GetHashCode()
        {
            unchecked 
            {
                int hash = 17;
                hash = hash * 23 + gridPos.GetHashCode();
                hash = hash * 23 + facingDirection.GetHashCode();
                hash = hash * 23 + straightMovesCount.GetHashCode();
                return hash;
            }
        }
    }
    
    // A* pathfinding node for vehicles (simplified, now just holds VehicleState)
    private class VehicleNode 
    {
        public HexagonController.HexData hexData;
        public int facingDirection; 

        public VehicleNode(HexagonController.HexData data, int facing)
        {
            hexData = data;
            facingDirection = facing;
        }
    }

    /// <summary>
    /// Represents a single step in the vehicle's path, including target position and rotation.
    /// </summary>
    public struct VehiclePathStep
    {
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public int facingDirection; 

        public VehiclePathStep(Vector3 pos, Quaternion rot, int facing)
        {
            targetPosition = pos;
            targetRotation = rot;
            facingDirection = facing;
        }
    }
    
    void Start()
    {
        hexController = FindObjectOfType<HexagonController>(); 
        if (hexController == null)
        {
            Debug.LogError("VehiclePathFinderCatmullRom requires a HexagonController component in the scene!");
        }
    }
    
    /// <summary>
    /// Find a path between two world positions with initial facing direction
    /// </summary>
    public List<VehiclePathStep> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos, int initialFacingDirection)
    {
        DebugPathfinding(startWorldPos, targetWorldPos, initialFacingDirection);
        
        Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
        Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);

        Debug.Log($"Finding path from world {startWorldPos} (grid {startGrid}) to world {targetWorldPos} (grid {targetGrid})");

        // Early exit if target is out of max movement range
        if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
        {
            Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
            return new List<VehiclePathStep>();
        }
        
        return FindPathInternal(startGrid, targetGrid, initialFacingDirection, startWorldPos);
    }
    
    /// <summary>
    /// Find a path between two grid positions with initial facing direction
    /// </summary>
    private List<VehiclePathStep> FindPathInternal(Vector2Int startGrid, Vector2Int targetGrid, int initialFacingDirection, Vector3 startWorldPosForRangeCheck)
    {
        if (hexController == null)
        {
            Debug.LogError("HexagonController not found. Cannot find path.");
            return new List<VehiclePathStep>();
        }
        
        // --- Initialize A* data structures for a new search ---
        gCost = new Dictionary<VehicleState, float>();
        hCost = new Dictionary<VehicleState, float>();
        cameFrom = new Dictionary<VehicleState, VehicleState>();
        closedSet = new HashSet<VehicleState>();
        openSet = new List<VehicleState>(); 

        // Initialize start state - straightMovesCount is 0 at the start
        VehicleState startState = new VehicleState(startGrid, initialFacingDirection, 0);
        
        // --- Validate start and target hexes ---
        HexagonController.HexData startHexData = hexController.GetHexData(startGrid.x, startGrid.y);
        if (startHexData.gridX == -1) 
        {
            Debug.LogWarning($"Cannot find path: Start position ({startGrid}) is out of bounds or invalid.");
            return new List<VehiclePathStep>();
        }
        if (!startHexData.isWalkable)
        {
            Debug.LogWarning($"Cannot find path: Start position ({startGrid}) is not walkable.");
            return new List<VehiclePathStep>();
        }
        
        bool targetReachable = false;
        HexagonController.HexData targetHexData = hexController.GetHexData(targetGrid.x, targetGrid.y);
        if (targetHexData.gridX == -1) 
        {
            Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is out of bounds or invalid.");
            return new List<VehiclePathStep>();
        }
        if (!targetHexData.isWalkable)
        {
            Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is not walkable.");
            return new List<VehiclePathStep>();
        }
        targetReachable = true; 

        if (!targetReachable)
        {
            Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is not walkable (after detailed check).");
            return new List<VehiclePathStep>();
        }
        
        // A* algorithm initialization
        gCost[startState] = 0;
        hCost[startState] = GetDistance(startGrid, targetGrid); 
        openSet.Add(startState);
        
        int iterations = 0;
        const int maxIterations = 50000; 
        
        Debug.Log($"Pathfinding A* started. Initial openSet count: {openSet.Count}");

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            
            // Get node with lowest fCost from openSet
            VehicleState currentNodeState = openSet.OrderBy(state => gCost.GetValueOrDefault(state, float.MaxValue) + hCost.GetValueOrDefault(state, float.MaxValue)).First();
            openSet.Remove(currentNodeState);
            closedSet.Add(currentNodeState);
            
            // Path found check: If current node is the target grid position (any facing)
            if (currentNodeState.gridPos == targetGrid)
            {
                Debug.Log($"Path found after {iterations} iterations!");
                return ReconstructPath(cameFrom, startState, currentNodeState);
            }
            
            // Check if current position is within max movement range (from original start)
            Vector3 currentNodeWorldPos = hexController.GetHexWorldPosition(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
            if (Vector3.Distance(startWorldPosForRangeCheck, currentNodeWorldPos) > maxMovementRange)
            {
                continue; 
            }

            // Get possible vehicle moves from current state (forward, turn left, turn right)
            List<VehicleState> possibleNextStates = GetVehicleMovementOptions(currentNodeState);
            
            foreach (VehicleState nextState in possibleNextStates)
            {
                if (closedSet.Contains(nextState))
                {
                    continue;
                }
                
                HexagonController.HexData nextHexData = hexController.GetHexData(nextState.gridPos.x, nextState.gridPos.y);

                if (nextHexData.gridX == -1 || !nextHexData.isWalkable)
                {
                    continue;
                }

                HexagonController.HexData currentNodeHexData = hexController.GetHexData(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
                if (!CanMoveBetween(currentNodeHexData, nextHexData))
                {
                    continue;
                }
                
                // MODIFIED: Pass targetGrid to GetMovementCost
                float tentativeGCost = gCost.GetValueOrDefault(currentNodeState, float.MaxValue) + GetMovementCost(currentNodeState, nextState, targetGrid);
                
                if (!openSet.Contains(nextState) || tentativeGCost < gCost.GetValueOrDefault(nextState, float.MaxValue))
                {
                    cameFrom[nextState] = currentNodeState;
                    gCost[nextState] = tentativeGCost;
                    hCost[nextState] = GetDistance(nextState.gridPos, targetGrid);
                    
                    if (!openSet.Contains(nextState))
                    {
                        openSet.Add(nextState);
                    }
                }
            }
        }
        
        Debug.LogWarning($"No path found after {iterations} iterations! (Max iterations: {maxIterations})");
        return new List<VehiclePathStep>();
    }
    
    /// <summary>
    /// Get the three possible vehicle movement options from current state,
    /// respecting the "move X hexagons straight before pivoting" rule.
    /// </summary>
    private List<VehicleState> GetVehicleMovementOptions(VehicleState currentState)
    {
        List<VehicleState> options = new List<VehicleState>();
        
        // Get the correct hex directions array for the current column's parity
        Vector2Int[] hexDirectionsForColumn = hexController.GetHexDirectionsForColumn(currentState.gridPos.x);

        if (currentState.facingDirection < 0 || currentState.facingDirection >= hexDirectionsForColumn.Length) 
        {
            Debug.LogWarning($"Invalid facing direction: {currentState.facingDirection} for hex {currentState.gridPos}. Defaulting to 0.");
            currentState.facingDirection = 0; 
        }
        
        // Get the forward hex position based on the current facing direction index
        Vector2Int forwardOffset = hexDirectionsForColumn[currentState.facingDirection]; 
        Vector2Int forwardPos = currentState.gridPos + forwardOffset;

        // Option 1: Move forward and continue straight
        options.Add(new VehicleState(forwardPos, currentState.facingDirection, currentState.straightMovesCount + 1));
        
        // Allow turning only if straightMovesCount is equal to or greater than the defined minimum.
        if (currentState.straightMovesCount >= minStraightMovesBeforeTurn) 
        {
            // Option 2: Move forward and turn left (60 degrees counter-clockwise)
            int leftDirection = (currentState.facingDirection + 5) % 6; 
            options.Add(new VehicleState(forwardPos, leftDirection, 0)); // Reset straightMovesCount to 0 on a turn
            
            // Option 3: Move forward and turn right (60 degrees clockwise)
            int rightDirection = (currentState.facingDirection + 1) % 6;
            options.Add(new VehicleState(forwardPos, rightDirection, 0)); // Reset straightMovesCount to 0 on a turn
        }
        else
        {
            Debug.Log($"  - From {currentState.gridPos} (facing {currentState.facingDirection}, straight: {currentState.straightMovesCount}): Only straight move allowed. Need {minStraightMovesBeforeTurn - currentState.straightMovesCount} more straight moves to turn.");
        }
        
        return options;
    }
    
    /// <summary>
    /// Move a game object along a path using Catmull-Rom splines for smooth movement.
    /// </summary>
    public void MoveAlongPath(VehicleController vehicle, List<VehiclePathStep> path, System.Action onComplete = null)
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("MoveAlongPath called with an empty or null path.");
            onComplete?.Invoke(); 
            return;
        }
        
        if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
        {
            StopCoroutine(activeMoveCoroutines[vehicle]);
            activeMoveCoroutines.Remove(vehicle);
        }

        // NEW: Simplify the path before feeding it to the spline
        List<Vector3> simplifiedSplinePoints = SimplifyPathForSpline(vehicle.transform.position, path);

        Coroutine newCoroutine = StartCoroutine(MoveCoroutine(vehicle.gameObject, simplifiedSplinePoints, () => { // Pass simplified points
            if (activeMoveCoroutines.ContainsKey(vehicle))
            {
                activeMoveCoroutines.Remove(vehicle);
            }
            onComplete?.Invoke();
        }));
        activeMoveCoroutines[vehicle] = newCoroutine;
    }

    /// <summary>
    /// Simplifies the A* path by removing intermediate points that don't represent a significant turn,
    /// based on the splineAngleTolerance.
    /// </summary>
    /// <param name="currentVehiclePosition">The current world position of the vehicle (start of the path).</param>
    /// <param name="originalPathSteps">The list of VehiclePathSteps from the A* algorithm.</param>
    /// <returns>A simplified list of Vector3 points for spline interpolation.</returns>
    private List<Vector3> SimplifyPathForSpline(Vector3 currentVehiclePosition, List<VehiclePathStep> originalPathSteps)
    {
        List<Vector3> simplifiedPoints = new List<Vector3>();

        if (originalPathSteps.Count == 0)
        {
            return simplifiedPoints;
        }

        // Always add the vehicle's current position as the true start of the spline
        simplifiedPoints.Add(currentVehiclePosition);

        // If the path has only one step, just add that target position
        if (originalPathSteps.Count == 1)
        {
            simplifiedPoints.Add(originalPathSteps[0].targetPosition);
            return simplifiedPoints;
        }

        // Add the first target position from the A* path
        simplifiedPoints.Add(originalPathSteps[0].targetPosition);

        // Iterate through the intermediate points of the A* path
        // We need at least 3 points (prev, current, next) to check for a turn
        for (int i = 1; i < originalPathSteps.Count - 1; i++)
        {
            Vector3 pPrev = originalPathSteps[i - 1].targetPosition;
            Vector3 pCurrent = originalPathSteps[i].targetPosition;
            Vector3 pNext = originalPathSteps[i + 1].targetPosition;

            // Calculate direction vectors, flattening Y to ignore height for angle calculation
            Vector3 dir1 = (pCurrent - pPrev);
            dir1.y = 0;
            dir1.Normalize();

            Vector3 dir2 = (pNext - pCurrent);
            dir2.y = 0;
            dir2.Normalize();

            // Calculate the angle between the two direction vectors
            float angle = Vector3.Angle(dir1, dir2);

            // If the angle is greater than the tolerance, it's a significant turn, so keep this point
            if (angle > splineAngleTolerance)
            {
                simplifiedPoints.Add(pCurrent);
            }
        }

        // Always add the very last point of the A* path
        simplifiedPoints.Add(originalPathSteps[originalPathSteps.Count - 1].targetPosition);

        return simplifiedPoints;
    }


    private System.Collections.IEnumerator MoveCoroutine(GameObject obj, List<Vector3> splinePoints, System.Action onComplete) // MODIFIED: now takes List<Vector3>
    {
        Transform objTransform = obj.transform;

        if (splinePoints == null || splinePoints.Count < 2) // Need at least 2 points for a line, 4 for a full spline segment
        {
            Debug.LogWarning("MoveCoroutine called with insufficient spline points.");
            onComplete?.Invoke();
            yield break;
        }

        // Calculate total approximate length for consistent speed
        float totalSplineLength = 0f;
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            totalSplineLength += Vector3.Distance(splinePoints[i], splinePoints[i + 1]);
        }
        float totalJourneyTime = totalSplineLength / movementSpeed;
        
        // Iterate through each segment of the path (from splinePoints[i] to splinePoints[i+1])
        // Note: Catmull-Rom requires P0, P1, P2, P3. P1 and P2 define the segment.
        // The loop goes up to splinePoints.Count - 1 because P2 is splinePoints[i + 1].
        // We need at least 4 points for the CatmullRom function to work fully,
        // but we handle edge cases by clamping p0 and p3.
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            // Define the 4 control points for the current spline segment
            // P0: Point before current segment start (clamped to P1 if at beginning)
            // P1: Current segment start point
            // P2: Current segment end point
            // P3: Point after current segment end (clamped to P2 if at end)
            Vector3 p0 = (i == 0) ? splinePoints[i] : splinePoints[i - 1]; // If first segment, P0 = P1
            Vector3 p1 = splinePoints[i];
            Vector3 p2 = splinePoints[i + 1];
            Vector3 p3 = (i + 2 >= splinePoints.Count) ? splinePoints[i + 1] : splinePoints[i + 2]; // If last segment, P3 = P2

            float segmentLength = Vector3.Distance(p1, p2);
            if (segmentLength < 0.01f) // Avoid division by zero or very small segments
            {
                objTransform.position = p2; // Snap to next point if segment is tiny
                continue;
            }
            float segmentDuration = (segmentLength / totalSplineLength) * totalJourneyTime;
            float elapsedTime = 0f; // Renamed from elapsedSegmentTime to avoid confusion

            while (elapsedTime < segmentDuration)
            {
                float t = elapsedTime / segmentDuration; // t from 0 to 1 for this segment

                Vector3 currentSplinePos = CatmullRom(p0, p1, p2, p3, t);
                
                // Calculate tangent for rotation
                Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, t);
                
                // Ensure tangent is not zero and is horizontal for LookRotation
                if (tangent.sqrMagnitude > 0.001f)
                {
                    tangent.y = 0; // Keep rotation horizontal
                    Quaternion targetRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                    objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                objTransform.position = currentSplinePos;

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        // Ensure vehicle ends exactly at the last point and facing the last direction
        objTransform.position = splinePoints[splinePoints.Count - 1];
        // The path variable is the original A* path, use its last step's facing direction
        // The original path is not passed to MoveCoroutine directly anymore, so we need to get the final facing direction differently
        // For simplicity, we'll make the vehicle face the direction of the last segment.
        // If you need the exact A* final facing, you might need to pass it as an argument or ensure it's calculated here.
        if (splinePoints.Count >= 2)
        {
            Vector3 lastSegmentDir = (splinePoints[splinePoints.Count - 1] - splinePoints[splinePoints.Count - 2]).normalized;
            lastSegmentDir.y = 0; // Flatten
            if (lastSegmentDir.sqrMagnitude > 0.001f)
            {
                Quaternion finalRotation = Quaternion.LookRotation(lastSegmentDir, Vector3.up);
                while (Quaternion.Angle(objTransform.rotation, finalRotation) > 0.1f)
                {
                    objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, finalRotation, rotationSpeed * Time.deltaTime);
                    yield return null;
                }
                objTransform.rotation = finalRotation;
            }
        }


        onComplete?.Invoke();
    }

    /// <summary>
    /// Calculates a point on a Catmull-Rom spline.
    /// </summary>
    /// <param name="p0">The first control point (previous to P1).</param>
    /// <param name="p1">The second control point (start of the segment).</param>
    /// <param name="p2">The third control point (end of the segment).</param>
    /// <param name="p3">The fourth control point (next after P2).</param>
    /// <param name="t">Interpolation value from 0 to 1.</param>
    /// <returns>The interpolated position on the spline.</returns>
    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// Calculates the tangent (direction) at a point on a Catmull-Rom spline.
    /// </summary>
    /// <param name="p0">The first control point (previous to P1).</param>
    /// <param name="p1">The second control point (start of the segment).</param>
    /// <param name="p2">The third control point (end of the segment).</param>
    /// <param name="p3">The fourth control point (next after P2).</param>
    /// <param name="t">Interpolation value from 0 to 1.</param>
    /// <returns>The tangent vector at the interpolated position.</returns>
    private Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;

        return 0.5f * (
            (-p0 + p2) +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * (2.0f * t) +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * (3.0f * t2)
        );
    }
    
    private bool CanMoveBetween(HexagonController.HexData fromHex, HexagonController.HexData toHex)
    {
        float heightDifference = toHex.height - fromHex.height;
        return heightDifference <= maxClimbHeight;
    }
    
    /// <summary>
    /// Calculates the movement cost between two vehicle states, including terrain, height, turning,
    /// and a penalty for misalignment with the final target direction.
    /// </summary>
    private float GetMovementCost(VehicleState fromState, VehicleState toState, Vector2Int targetGrid) // MODIFIED: Added targetGrid
    {
        float baseCost = 1f;
        
        HexagonController.HexData fromHex = hexController.GetHexData(fromState.gridPos.x, fromState.gridPos.y);
        HexagonController.HexData toHex = hexController.GetHexData(toState.gridPos.x, toState.gridPos.y);

        if (fromHex.gridX == -1 || toHex.gridX == -1) 
        {
            return float.MaxValue; 
        }

        float heightDifference = toHex.height - fromHex.height;
        
        if (heightDifference > 0)
        {
            baseCost *= uphillCostMultiplier;
        }
        else if (heightDifference < 0)
        {
            baseCost *= downhillCostMultiplier;
        }
        
        if (toHex.isRock)
        {
            baseCost *= rockTerrainCostMultiplier;
        }

        // Add cost for turning (if facing direction changed)
        if (fromState.facingDirection != toState.facingDirection)
        {
            baseCost += turningCostPenalty;
        }

        // NEW: Add a penalty for misalignment with the final target
        Vector3 toWorldPos = hexController.GridToWorldPosition(toState.gridPos.x, toState.gridPos.y);
        Vector3 targetWorldPos = hexController.GridToWorldPosition(targetGrid.x, targetGrid.y);

        Vector3 currentToTargetDirection = (targetWorldPos - toWorldPos);
        currentToTargetDirection.y = 0; // Flatten for horizontal direction
        currentToTargetDirection.Normalize();

        Vector3 vehicleFacingDirection = GetRotationFromDirection(toState.facingDirection) * Vector3.forward;
        vehicleFacingDirection.y = 0; // Flatten
        vehicleFacingDirection.Normalize();

        float angleToTarget = Vector3.Angle(vehicleFacingDirection, currentToTargetDirection);
        // Normalize angle to a 0-1 range (0 for perfectly aligned, 1 for 180 degrees opposite)
        float normalizedAngle = angleToTarget / 180f; 
        
        baseCost += normalizedAngle * finalOrientationCostPenalty;
        
        return baseCost;
    }
    
    private float GetDistance(Vector2Int a, Vector2Int b)
    {
        int ax = a.x;
        int az = a.y;
        int bx = b.x;
        int bz = b.y;

        int dx = Mathf.Abs(ax - bx);
        int dy = Mathf.Abs(az - bz);
        int dz = Mathf.Abs((-ax - az) - (-bx - bz)); 

        return (dx + dy + dz) / 2f; 
    }
    
    private List<VehiclePathStep> ReconstructPath(Dictionary<VehicleState, VehicleState> cameFrom, VehicleState startState, VehicleState endState)
    {
        List<VehicleState> pathStates = new List<VehicleState>();
        VehicleState? currentState = endState; 
        
        while (currentState.HasValue && cameFrom.ContainsKey(currentState.Value))
        {
            pathStates.Add(currentState.Value);
            currentState = cameFrom[currentState.Value];
        }
        if (currentState.HasValue) {
            pathStates.Add(currentState.Value);
        }
        
        pathStates.Reverse(); 

        List<VehiclePathStep> pathSteps = new List<VehiclePathStep>();

        for (int i = 0; i < pathStates.Count; i++)
        {
            VehicleState state = pathStates[i];
            
            Vector3 stepTargetWorldPos = hexController.GridToWorldPosition(state.gridPos.x, state.gridPos.y);
            stepTargetWorldPos.y += hexController.hexMeshHeight * 0.5f; 
            stepTargetWorldPos.y += heightOffset; 

            Quaternion stepTargetRotation;

            if (i == 0)
            {
                // For the very first step in the path, the rotation should be the initial facing direction of the vehicle.
                stepTargetRotation = GetRotationFromDirection(startState.facingDirection);
            }
            else
            {
                // For subsequent steps, the rotation should be from the previous hex to the current hex.
                VehicleState prevState = pathStates[i - 1];
                int directionIndex = GetHexDirection(prevState.gridPos.x, prevState.gridPos.y, state.gridPos.x, state.gridPos.y);
                stepTargetRotation = GetRotationFromDirection(directionIndex);
            }
            
            pathSteps.Add(new VehiclePathStep(stepTargetWorldPos, stepTargetRotation, state.facingDirection));
        }

        return pathSteps;
    }
    
    /// <summary>
    /// Convert a facing direction (0-5) to a Unity Quaternion
    /// </summary>
    private Quaternion GetRotationFromDirection(int facingDirection)
    {
        float angle = facingDirection * 60f;
        return Quaternion.Euler(0, angle, 0);
    }

    /// <summary>
    /// Calculates the integer direction (0-5) from one hex to an adjacent hex.
    /// Assumes 'toHexGridPos' is an immediate neighbor of 'fromHexGridPos'.
    /// </summary>
    private int GetHexDirection(int fromX, int fromY, int toX, int toY)
    {
        Vector2Int offset = new Vector2Int(toX - fromX, toY - fromY);
        
        if (hexController == null) 
        {
            Debug.LogError("GetHexDirection: HexagonController not accessible.");
            return 0; 
        }

        Vector2Int[] currentColumnHexDirections = hexController.GetHexDirectionsForColumn(fromX);

        for (int i = 0; i < currentColumnHexDirections.Length; i++)
        {
            if (currentColumnHexDirections[i] == offset)
            {
                return i;
            }
        }
        Debug.LogWarning($"GetHexDirection: Could not find direction index for offset {offset} from ({fromX},{fromY}) to ({toX},{toY}). Returning 0.");
        return 0; 
    }
    
    /// <summary>
    /// Get the nearest walkable hex to a world position
    /// </summary>
    public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
    {
        Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
        
        HexagonController.HexData hexData = hexController.GetHexData(gridPos.x, gridPos.y);
        if (hexData.gridX != -1 && hexData.isWalkable) 
        {
            return gridPos;
        }
        
        for (int radius = 1; radius <= 10; radius++) 
        {
            for (int xOffset = -radius; xOffset <= radius; xOffset++)
            {
                for (int zOffset = -radius; zOffset <= radius; zOffset++)
                {
                    Vector2Int checkPos = new Vector2Int(gridPos.x + xOffset, gridPos.y + zOffset);
                    HexagonController.HexData checkHexData = hexController.GetHexData(checkPos.x, checkPos.y);
                    
                    if (checkHexData.gridX != -1 && checkHexData.isWalkable) 
                    {
                        return checkPos;
                    }
                }
            }
        }
        
        Debug.LogWarning($"GetNearestWalkableHex: No walkable hex found near {worldPos} within radius 10. Returning original grid position {gridPos}.");
        return gridPos; 
    }
    
    /// <summary>
    /// Check if a path exists between two positions
    /// </summary>
    public bool PathExists(Vector3 startPos, Vector3 endPos, int initialFacingDirection)
    {
        List<VehiclePathStep> path = FindPath(startPos, endPos, initialFacingDirection);
        return path.Count > 0;
    }
    
    /// <summary>
    /// Get the distance of a path (useful for AI decision making)
    /// </summary>
    public float GetPathDistance(Vector3 startPos, Vector3 endPos, int initialFacingDirection)
    {
        List<VehiclePathStep> path = FindPath(startPos, endPos, initialFacingDirection);
        if (path.Count == 0) return float.MaxValue;
        
        float totalDistance = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            totalDistance += Vector3.Distance(path[i].targetPosition, path[i + 1].targetPosition);
        }
        return totalDistance;
    }
    
    public void DebugPathfinding(Vector3 startPos, Vector3 endPos, int facing)
    {
        Vector2Int startGrid = hexController.WorldToGridPosition(startPos);
        Vector2Int endGrid = hexController.WorldToGridPosition(endPos);
    
        Debug.Log($"Debug: Start World: {startPos} -> Grid: {startGrid}");
        Debug.Log($"Debug: End World: {endPos} -> Grid: {endGrid}");
        Debug.Log($"Debug: Facing: {facing}");
    
        bool startInBounds = hexController.IsValidGridPosition(startGrid.x, startGrid.y);
        bool endInBounds = hexController.IsValidGridPosition(endGrid.x, endGrid.y);
    
        Debug.Log($"Debug: Start in bounds: {startInBounds}, End in bounds: {endInBounds}");
    
        if (startInBounds)
        {
            var startHex = hexController.GetHexData(startGrid.x, startGrid.y);
            Debug.Log($"Debug: Start hex walkable: {startHex.isWalkable}, height: {startHex.height}");
        }
        else
        {
            Debug.LogWarning($"Debug: Start grid position {startGrid} is OUT OF BOUNDS!");
        }
    
        if (endInBounds)
        {
            var endHex = hexController.GetHexData(endGrid.x, endGrid.y);
            Debug.Log($"Debug: End hex walkable: {endHex.isWalkable}, height: {endHex.height}");
        }
        else
        {
            Debug.LogWarning($"Debug: End grid position {endGrid} is OUT OF BOUNDS!");
        }
    }

    public int GetFacingDirectionFromRotation(Quaternion rotation)
    {
        float angle = rotation.eulerAngles.y;

        // Normalize angle to 0-360 range
        angle = angle % 360f;
        if (angle < 0) angle += 360f;

        // Convert to direction index (0-5)
        int direction = Mathf.RoundToInt(angle / 60f) % 6;
        return direction;
    }
}


// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq; // Required for .OrderBy() and .Any()
//
// public class VehiclePathFinderCatmullRom : MonoBehaviour // Renamed class to reflect capital 'F'
// {
//     [Header("Pathfinding Settings")]
//     [Tooltip("Maximum height difference allowed between adjacent hexagons for movement")]
//     public float maxClimbHeight = 2f;
//     
//     [Tooltip("Cost multiplier for moving uphill (higher = more expensive)")]
//     public float uphillCostMultiplier = 1.5f;
//     
//     [Tooltip("Cost multiplier for moving downhill (lower = cheaper)")]
//     public float downhillCostMultiplier = 0.8f;
//     
//     [Tooltip("Cost multiplier for moving through rocky terrain")]
//     public float rockTerrainCostMultiplier = 1.3f;
//
//     [Tooltip("Additional cost for turning (left or right turns). Increase this value to make pathfinding prefer straight lines.")]
//     public float turningCostPenalty = 1.5f; 
//
//     [Tooltip("Minimum number of straight hexagons the vehicle must move before it is allowed to pivot or turn. Set to 0 to allow turning at any step.")]
//     public int minStraightMovesBeforeTurn = 0; 
//
//     [Tooltip("Maximum world distance (radius) a vehicle can move from its starting point.")]
//     public float maxMovementRange = 10f; 
//     
//     [Header("Movement Settings")]
//     [Tooltip("Speed of movement along the path")]
//     public float movementSpeed = 5f;
//     
//     [Tooltip("How smoothly the object rotates to face movement direction")]
//     public float rotationSpeed = 180f; // Degrees per second for rotation
//     
//     [Tooltip("Height offset above terrain for the moving object")]
//     public float heightOffset = 0.5f;
//     
//     [Tooltip("Enable smooth height interpolation between hexagons")]
//     public bool smoothHeightTransition = true; // This will be handled by spline now
//     
//     [Header("Catmull-Rom Spline Settings")]
//     [Tooltip("Higher values make the spline straighter by simplifying the path more aggressively. This is an angle in degrees. Set to 0 for no simplification.")]
//     [Range(0f, 60f)] // Angle can range from 0 to 60 degrees (a full hex turn)
//     public float splineAngleTolerance = 10f; // NEW: Public variable for spline strength
//     
//     private HexagonController hexController;
//     
//     // --- A* specific data structures ---
//     private Dictionary<VehicleState, float> gCost; // Cost from start to this node
//     private Dictionary<VehicleState, float> hCost; // Heuristic cost from this node to target
//     private Dictionary<VehicleState, VehicleState> cameFrom; // Parent node in the path
//     private HashSet<VehicleState> closedSet; // Nodes already evaluated
//     private List<VehicleState> openSet; // Nodes to be evaluated
//
//     // Stores the currently running movement coroutine for a specific GameObject
//     private Dictionary<VehicleController, Coroutine> activeMoveCoroutines = new Dictionary<VehicleController, Coroutine>();
//     
//     // Represents a vehicle's position, facing direction, AND consecutive straight moves
//     public struct VehicleState : System.IEquatable<VehicleState> 
//     {
//         public Vector2Int gridPos; 
//         public int facingDirection; 
//         public int straightMovesCount; 
//         
//         public VehicleState(Vector2Int pos, int facing, int straightCount)
//         {
//             gridPos = pos;
//             facingDirection = facing;
//             straightMovesCount = straightCount;
//         }
//         
//         public override bool Equals(object obj)
//         {
//             if (obj is VehicleState other)
//             {
//                 return Equals(other);
//             }
//             return false;
//         }
//
//         public bool Equals(VehicleState other) 
//         {
//             return gridPos == other.gridPos && 
//                    facingDirection == other.facingDirection &&
//                    straightMovesCount == other.straightMovesCount;
//         }
//         
//         public override int GetHashCode()
//         {
//             unchecked 
//             {
//                 int hash = 17;
//                 hash = hash * 23 + gridPos.GetHashCode();
//                 hash = hash * 23 + facingDirection.GetHashCode();
//                 hash = hash * 23 + straightMovesCount.GetHashCode();
//                 return hash;
//             }
//         }
//     }
//     
//     // A* pathfinding node for vehicles (simplified, now just holds VehicleState)
//     private class VehicleNode 
//     {
//         public HexagonController.HexData hexData;
//         public int facingDirection; 
//
//         public VehicleNode(HexagonController.HexData data, int facing)
//         {
//             hexData = data;
//             facingDirection = facing;
//         }
//     }
//
//     /// <summary>
//     /// Represents a single step in the vehicle's path, including target position and rotation.
//     /// </summary>
//     public struct VehiclePathStep
//     {
//         public Vector3 targetPosition;
//         public Quaternion targetRotation;
//         public int facingDirection; 
//
//         public VehiclePathStep(Vector3 pos, Quaternion rot, int facing)
//         {
//             targetPosition = pos;
//             targetRotation = rot;
//             facingDirection = facing;
//         }
//     }
//     
//     void Start()
//     {
//         hexController = FindObjectOfType<HexagonController>(); 
//         if (hexController == null)
//         {
//             Debug.LogError("VehiclePathFinderCatmullRom requires a HexagonController component in the scene!");
//         }
//     }
//     
//     /// <summary>
//     /// Find a path between two world positions with initial facing direction
//     /// </summary>
//     public List<VehiclePathStep> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos, int initialFacingDirection)
//     {
//         DebugPathfinding(startWorldPos, targetWorldPos, initialFacingDirection);
//         
//         Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
//         Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);
//
//         Debug.Log($"Finding path from world {startWorldPos} (grid {startGrid}) to world {targetWorldPos} (grid {targetGrid})");
//
//         // Early exit if target is out of max movement range
//         if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
//         {
//             Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
//             return new List<VehiclePathStep>();
//         }
//         
//         return FindPathInternal(startGrid, targetGrid, initialFacingDirection, startWorldPos);
//     }
//     
//     /// <summary>
//     /// Find a path between two grid positions with initial facing direction
//     /// </summary>
//     private List<VehiclePathStep> FindPathInternal(Vector2Int startGrid, Vector2Int targetGrid, int initialFacingDirection, Vector3 startWorldPosForRangeCheck)
//     {
//         if (hexController == null)
//         {
//             Debug.LogError("HexagonController not found. Cannot find path.");
//             return new List<VehiclePathStep>();
//         }
//         
//         // --- Initialize A* data structures for a new search ---
//         gCost = new Dictionary<VehicleState, float>();
//         hCost = new Dictionary<VehicleState, float>();
//         cameFrom = new Dictionary<VehicleState, VehicleState>();
//         closedSet = new HashSet<VehicleState>();
//         openSet = new List<VehicleState>(); 
//
//         // Initialize start state - straightMovesCount is 0 at the start
//         VehicleState startState = new VehicleState(startGrid, initialFacingDirection, 0);
//         
//         // --- Validate start and target hexes ---
//         HexagonController.HexData startHexData = hexController.GetHexData(startGrid.x, startGrid.y);
//         if (startHexData.gridX == -1) 
//         {
//             Debug.LogWarning($"Cannot find path: Start position ({startGrid}) is out of bounds or invalid.");
//             return new List<VehiclePathStep>();
//         }
//         if (!startHexData.isWalkable)
//         {
//             Debug.LogWarning($"Cannot find path: Start position ({startGrid}) is not walkable.");
//             return new List<VehiclePathStep>();
//         }
//         
//         bool targetReachable = false;
//         HexagonController.HexData targetHexData = hexController.GetHexData(targetGrid.x, targetGrid.y);
//         if (targetHexData.gridX == -1) 
//         {
//             Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is out of bounds or invalid.");
//             return new List<VehiclePathStep>();
//         }
//         if (!targetHexData.isWalkable)
//         {
//             Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is not walkable.");
//             return new List<VehiclePathStep>();
//         }
//         targetReachable = true; 
//
//         if (!targetReachable)
//         {
//             Debug.LogWarning($"Cannot find path: Target ({targetGrid}) is not walkable (after detailed check).");
//             return new List<VehiclePathStep>();
//         }
//         
//         // A* algorithm initialization
//         gCost[startState] = 0;
//         hCost[startState] = GetDistance(startGrid, targetGrid); 
//         openSet.Add(startState);
//         
//         int iterations = 0;
//         const int maxIterations = 50000; 
//         
//         Debug.Log($"Pathfinding A* started. Initial openSet count: {openSet.Count}");
//
//         while (openSet.Count > 0 && iterations < maxIterations)
//         {
//             iterations++;
//             
//             // Get node with lowest fCost from openSet
//             VehicleState currentNodeState = openSet.OrderBy(state => gCost.GetValueOrDefault(state, float.MaxValue) + hCost.GetValueOrDefault(state, float.MaxValue)).First();
//             openSet.Remove(currentNodeState);
//             closedSet.Add(currentNodeState);
//             
//             // Path found check: If current node is the target grid position (any facing)
//             if (currentNodeState.gridPos == targetGrid)
//             {
//                 Debug.Log($"Path found after {iterations} iterations!");
//                 return ReconstructPath(cameFrom, startState, currentNodeState);
//             }
//             
//             // Check if current position is within max movement range (from original start)
//             Vector3 currentNodeWorldPos = hexController.GetHexWorldPosition(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
//             if (Vector3.Distance(startWorldPosForRangeCheck, currentNodeWorldPos) > maxMovementRange)
//             {
//                 continue; 
//             }
//
//             // Get possible vehicle moves from current state (forward, turn left, turn right)
//             List<VehicleState> possibleNextStates = GetVehicleMovementOptions(currentNodeState);
//             
//             foreach (VehicleState nextState in possibleNextStates)
//             {
//                 if (closedSet.Contains(nextState))
//                 {
//                     continue;
//                 }
//                 
//                 HexagonController.HexData nextHexData = hexController.GetHexData(nextState.gridPos.x, nextState.gridPos.y);
//
//                 if (nextHexData.gridX == -1 || !nextHexData.isWalkable)
//                 {
//                     continue;
//                 }
//
//                 HexagonController.HexData currentNodeHexData = hexController.GetHexData(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
//                 if (!CanMoveBetween(currentNodeHexData, nextHexData))
//                 {
//                     continue;
//                 }
//                 
//                 float tentativeGCost = gCost.GetValueOrDefault(currentNodeState, float.MaxValue) + GetMovementCost(currentNodeState, nextState);
//                 
//                 if (!openSet.Contains(nextState) || tentativeGCost < gCost.GetValueOrDefault(nextState, float.MaxValue))
//                 {
//                     cameFrom[nextState] = currentNodeState;
//                     gCost[nextState] = tentativeGCost;
//                     hCost[nextState] = GetDistance(nextState.gridPos, targetGrid);
//                     
//                     if (!openSet.Contains(nextState))
//                     {
//                         openSet.Add(nextState);
//                     }
//                 }
//             }
//         }
//         
//         Debug.LogWarning($"No path found after {iterations} iterations! (Max iterations: {maxIterations})");
//         return new List<VehiclePathStep>();
//     }
//     
//     /// <summary>
//     /// Get the three possible vehicle movement options from current state,
//     /// respecting the "move X hexagons straight before pivoting" rule.
//     /// </summary>
//     private List<VehicleState> GetVehicleMovementOptions(VehicleState currentState)
//     {
//         List<VehicleState> options = new List<VehicleState>();
//         
//         // Get the correct hex directions array for the current column's parity
//         Vector2Int[] hexDirectionsForColumn = hexController.GetHexDirectionsForColumn(currentState.gridPos.x);
//
//         if (currentState.facingDirection < 0 || currentState.facingDirection >= hexDirectionsForColumn.Length) 
//         {
//             Debug.LogWarning($"Invalid facing direction: {currentState.facingDirection} for hex {currentState.gridPos}. Defaulting to 0.");
//             currentState.facingDirection = 0; 
//         }
//         
//         // Get the forward hex position based on the current facing direction index
//         Vector2Int forwardOffset = hexDirectionsForColumn[currentState.facingDirection]; 
//         Vector2Int forwardPos = currentState.gridPos + forwardOffset;
//
//         // Option 1: Move forward and continue straight
//         options.Add(new VehicleState(forwardPos, currentState.facingDirection, currentState.straightMovesCount + 1));
//         
//         // Allow turning only if straightMovesCount is equal to or greater than the defined minimum.
//         if (currentState.straightMovesCount >= minStraightMovesBeforeTurn) 
//         {
//             // Option 2: Move forward and turn left (60 degrees counter-clockwise)
//             int leftDirection = (currentState.facingDirection + 5) % 6; 
//             options.Add(new VehicleState(forwardPos, leftDirection, 0)); // Reset straightMovesCount to 0 on a turn
//             
//             // Option 3: Move forward and turn right (60 degrees clockwise)
//             int rightDirection = (currentState.facingDirection + 1) % 6;
//             options.Add(new VehicleState(forwardPos, rightDirection, 0)); // Reset straightMovesCount to 0 on a turn
//         }
//         else
//         {
//             Debug.Log($"  - From {currentState.gridPos} (facing {currentState.facingDirection}, straight: {currentState.straightMovesCount}): Only straight move allowed. Need {minStraightMovesBeforeTurn - currentState.straightMovesCount} more straight moves to turn.");
//         }
//         
//         return options;
//     }
//     
//     /// <summary>
//     /// Move a game object along a path using Catmull-Rom splines for smooth movement.
//     /// </summary>
//     public void MoveAlongPath(VehicleController vehicle, List<VehiclePathStep> path, System.Action onComplete = null)
//     {
//         if (path == null || path.Count == 0)
//         {
//             Debug.LogWarning("MoveAlongPath called with an empty or null path.");
//             onComplete?.Invoke(); 
//             return;
//         }
//         
//         if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
//         {
//             StopCoroutine(activeMoveCoroutines[vehicle]);
//             activeMoveCoroutines.Remove(vehicle);
//         }
//
//         // NEW: Simplify the path before feeding it to the spline
//         List<Vector3> simplifiedSplinePoints = SimplifyPathForSpline(vehicle.transform.position, path);
//
//         Coroutine newCoroutine = StartCoroutine(MoveCoroutine(vehicle.gameObject, simplifiedSplinePoints, () => { // Pass simplified points
//             if (activeMoveCoroutines.ContainsKey(vehicle))
//             {
//                 activeMoveCoroutines.Remove(vehicle);
//             }
//             onComplete?.Invoke();
//         }));
//         activeMoveCoroutines[vehicle] = newCoroutine;
//     }
//
//     /// <summary>
//     /// Simplifies the A* path by removing intermediate points that don't represent a significant turn,
//     /// based on the splineAngleTolerance.
//     /// </summary>
//     /// <param name="currentVehiclePosition">The current world position of the vehicle (start of the path).</param>
//     /// <param name="originalPathSteps">The list of VehiclePathSteps from the A* algorithm.</param>
//     /// <returns>A simplified list of Vector3 points for spline interpolation.</returns>
//     private List<Vector3> SimplifyPathForSpline(Vector3 currentVehiclePosition, List<VehiclePathStep> originalPathSteps)
//     {
//         List<Vector3> simplifiedPoints = new List<Vector3>();
//
//         if (originalPathSteps.Count == 0)
//         {
//             return simplifiedPoints;
//         }
//
//         // Always add the vehicle's current position as the true start of the spline
//         simplifiedPoints.Add(currentVehiclePosition);
//
//         // If the path has only one step, just add that target position
//         if (originalPathSteps.Count == 1)
//         {
//             simplifiedPoints.Add(originalPathSteps[0].targetPosition);
//             return simplifiedPoints;
//         }
//
//         // Add the first target position from the A* path
//         simplifiedPoints.Add(originalPathSteps[0].targetPosition);
//
//         // Iterate through the intermediate points of the A* path
//         // We need at least 3 points (prev, current, next) to check for a turn
//         for (int i = 1; i < originalPathSteps.Count - 1; i++)
//         {
//             Vector3 pPrev = originalPathSteps[i - 1].targetPosition;
//             Vector3 pCurrent = originalPathSteps[i].targetPosition;
//             Vector3 pNext = originalPathSteps[i + 1].targetPosition;
//
//             // Calculate direction vectors, flattening Y to ignore height for angle calculation
//             Vector3 dir1 = (pCurrent - pPrev);
//             dir1.y = 0;
//             dir1.Normalize();
//
//             Vector3 dir2 = (pNext - pCurrent);
//             dir2.y = 0;
//             dir2.Normalize();
//
//             // Calculate the angle between the two direction vectors
//             float angle = Vector3.Angle(dir1, dir2);
//
//             // If the angle is greater than the tolerance, it's a significant turn, so keep this point
//             if (angle > splineAngleTolerance)
//             {
//                 simplifiedPoints.Add(pCurrent);
//             }
//         }
//
//         // Always add the very last point of the A* path
//         simplifiedPoints.Add(originalPathSteps[originalPathSteps.Count - 1].targetPosition);
//
//         return simplifiedPoints;
//     }
//
//
//     private System.Collections.IEnumerator MoveCoroutine(GameObject obj, List<Vector3> splinePoints, System.Action onComplete) // MODIFIED: now takes List<Vector3>
//     {
//         Transform objTransform = obj.transform;
//
//         if (splinePoints == null || splinePoints.Count < 2) // Need at least 2 points for a line, 4 for a full spline segment
//         {
//             Debug.LogWarning("MoveCoroutine called with insufficient spline points.");
//             onComplete?.Invoke();
//             yield break;
//         }
//
//         // Calculate total approximate length for consistent speed
//         float totalSplineLength = 0f;
//         for (int i = 0; i < splinePoints.Count - 1; i++)
//         {
//             totalSplineLength += Vector3.Distance(splinePoints[i], splinePoints[i + 1]);
//         }
//         float totalJourneyTime = totalSplineLength / movementSpeed;
//         
//         // Iterate through each segment of the path (from splinePoints[i] to splinePoints[i+1])
//         // Note: Catmull-Rom requires P0, P1, P2, P3. P1 and P2 define the segment.
//         // The loop goes up to splinePoints.Count - 1 because P2 is splinePoints[i + 1].
//         // We need at least 4 points for the CatmullRom function to work fully,
//         // but we handle edge cases by clamping p0 and p3.
//         for (int i = 0; i < splinePoints.Count - 1; i++)
//         {
//             // Define the 4 control points for the current spline segment
//             // P0: Point before current segment start (clamped to P1 if at beginning)
//             // P1: Current segment start point
//             // P2: Current segment end point
//             // P3: Point after current segment end (clamped to P2 if at end)
//             Vector3 p0 = (i == 0) ? splinePoints[i] : splinePoints[i - 1]; // If first segment, P0 = P1
//             Vector3 p1 = splinePoints[i];
//             Vector3 p2 = splinePoints[i + 1];
//             Vector3 p3 = (i + 2 >= splinePoints.Count) ? splinePoints[i + 1] : splinePoints[i + 2]; // If last segment, P3 = P2
//
//             float segmentLength = Vector3.Distance(p1, p2);
//             if (segmentLength < 0.01f) // Avoid division by zero or very small segments
//             {
//                 objTransform.position = p2; // Snap to next point if segment is tiny
//                 continue;
//             }
//             float segmentDuration = (segmentLength / totalSplineLength) * totalJourneyTime;
//             float elapsedTime = 0f; // Renamed from elapsedSegmentTime to avoid confusion
//
//             while (elapsedTime < segmentDuration)
//             {
//                 float t = elapsedTime / segmentDuration; // t from 0 to 1 for this segment
//
//                 Vector3 currentSplinePos = CatmullRom(p0, p1, p2, p3, t);
//                 
//                 // Calculate tangent for rotation
//                 Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, t);
//                 
//                 // Ensure tangent is not zero and is horizontal for LookRotation
//                 if (tangent.sqrMagnitude > 0.001f)
//                 {
//                     tangent.y = 0; // Keep rotation horizontal
//                     Quaternion targetRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
//                     objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
//                 }
//
//                 objTransform.position = currentSplinePos;
//
//                 elapsedTime += Time.deltaTime;
//                 yield return null;
//             }
//         }
//
//         // Ensure vehicle ends exactly at the last point and facing the last direction
//         objTransform.position = splinePoints[splinePoints.Count - 1];
//         // The path variable is the original A* path, use its last step's facing direction
//         // The original path is not passed to MoveCoroutine directly anymore, so we need to get the final facing direction differently
//         // For simplicity, we'll make the vehicle face the direction of the last segment.
//         // If you need the exact A* final facing, you might need to pass it as an argument or ensure it's calculated here.
//         if (splinePoints.Count >= 2)
//         {
//             Vector3 lastSegmentDir = (splinePoints[splinePoints.Count - 1] - splinePoints[splinePoints.Count - 2]).normalized;
//             lastSegmentDir.y = 0; // Flatten
//             if (lastSegmentDir.sqrMagnitude > 0.001f)
//             {
//                 Quaternion finalRotation = Quaternion.LookRotation(lastSegmentDir, Vector3.up);
//                 while (Quaternion.Angle(objTransform.rotation, finalRotation) > 0.1f)
//                 {
//                     objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, finalRotation, rotationSpeed * Time.deltaTime);
//                     yield return null;
//                 }
//                 objTransform.rotation = finalRotation;
//             }
//         }
//
//
//         onComplete?.Invoke();
//     }
//
//     /// <summary>
//     /// Calculates a point on a Catmull-Rom spline.
//     /// </summary>
//     /// <param name="p0">The first control point (previous to P1).</param>
//     /// <param name="p1">The second control point (start of the segment).</param>
//     /// <param name="p2">The third control point (end of the segment).</param>
//     /// <param name="p3">The fourth control point (next after P2).</param>
//     /// <param name="t">Interpolation value from 0 to 1.</param>
//     /// <returns>The interpolated position on the spline.</returns>
//     private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
//     {
//         float t2 = t * t;
//         float t3 = t2 * t;
//
//         return 0.5f * (
//             (2.0f * p1) +
//             (-p0 + p2) * t +
//             (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
//             (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
//         );
//     }
//
//     /// <summary>
//     /// Calculates the tangent (direction) at a point on a Catmull-Rom spline.
//     /// </summary>
//     /// <param name="p0">The first control point (previous to P1).</param>
//     /// <param name="p1">The second control point (start of the segment).</param>
//     /// <param name="p2">The third control point (end of the segment).</param>
//     /// <param name="p3">The fourth control point (next after P2).</param>
//     /// <param name="t">Interpolation value from 0 to 1.</param>
//     /// <returns>The tangent vector at the interpolated position.</returns>
//     private Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
//     {
//         float t2 = t * t;
//
//         return 0.5f * (
//             (-p0 + p2) +
//             (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * (2.0f * t) +
//             (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * (3.0f * t2)
//         );
//     }
//     
//     private bool CanMoveBetween(HexagonController.HexData fromHex, HexagonController.HexData toHex)
//     {
//         float heightDifference = toHex.height - fromHex.height;
//         return heightDifference <= maxClimbHeight;
//     }
//     
//     private float GetMovementCost(VehicleState fromState, VehicleState toState)
//     {
//         float baseCost = 1f;
//         
//         HexagonController.HexData fromHex = hexController.GetHexData(fromState.gridPos.x, fromState.gridPos.y);
//         HexagonController.HexData toHex = hexController.GetHexData(toState.gridPos.x, toState.gridPos.y);
//
//         if (fromHex.gridX == -1 || toHex.gridX == -1) 
//         {
//             return float.MaxValue; 
//         }
//
//         float heightDifference = toHex.height - fromHex.height;
//         
//         if (heightDifference > 0)
//         {
//             baseCost *= uphillCostMultiplier;
//         }
//         else if (heightDifference < 0)
//         {
//             baseCost *= downhillCostMultiplier;
//         }
//         
//         if (toHex.isRock)
//         {
//             baseCost *= rockTerrainCostMultiplier;
//         }
//
//         // Add cost for turning (if facing direction changed)
//         if (fromState.facingDirection != toState.facingDirection)
//         {
//             baseCost += turningCostPenalty;
//         }
//         
//         return baseCost;
//     }
//     
//     private float GetDistance(Vector2Int a, Vector2Int b)
//     {
//         int ax = a.x;
//         int az = a.y;
//         int bx = b.x;
//         int bz = b.y;
//
//         int dx = Mathf.Abs(ax - bx);
//         int dy = Mathf.Abs(az - bz);
//         int dz = Mathf.Abs((-ax - az) - (-bx - bz)); 
//
//         return (dx + dy + dz) / 2f; 
//     }
//     
//     private List<VehiclePathStep> ReconstructPath(Dictionary<VehicleState, VehicleState> cameFrom, VehicleState startState, VehicleState endState)
//     {
//         List<VehicleState> pathStates = new List<VehicleState>();
//         VehicleState? currentState = endState; 
//         
//         while (currentState.HasValue && cameFrom.ContainsKey(currentState.Value))
//         {
//             pathStates.Add(currentState.Value);
//             currentState = cameFrom[currentState.Value];
//         }
//         if (currentState.HasValue) {
//             pathStates.Add(currentState.Value);
//         }
//         
//         pathStates.Reverse(); 
//
//         List<VehiclePathStep> pathSteps = new List<VehiclePathStep>();
//
//         for (int i = 0; i < pathStates.Count; i++)
//         {
//             VehicleState state = pathStates[i];
//             
//             Vector3 stepTargetWorldPos = hexController.GridToWorldPosition(state.gridPos.x, state.gridPos.y);
//             stepTargetWorldPos.y += hexController.hexMeshHeight * 0.5f; 
//             stepTargetWorldPos.y += heightOffset; 
//
//             Quaternion stepTargetRotation;
//
//             if (i == 0)
//             {
//                 // For the very first step in the path, the rotation should be the initial facing direction of the vehicle.
//                 stepTargetRotation = GetRotationFromDirection(startState.facingDirection);
//             }
//             else
//             {
//                 // For subsequent steps, the rotation should be from the previous hex to the current hex.
//                 VehicleState prevState = pathStates[i - 1];
//                 int directionIndex = GetHexDirection(prevState.gridPos.x, prevState.gridPos.y, state.gridPos.x, state.gridPos.y);
//                 stepTargetRotation = GetRotationFromDirection(directionIndex);
//             }
//             
//             pathSteps.Add(new VehiclePathStep(stepTargetWorldPos, stepTargetRotation, state.facingDirection));
//         }
//
//         return pathSteps;
//     }
//     
//     /// <summary>
//     /// Convert a facing direction (0-5) to a Unity Quaternion
//     /// </summary>
//     private Quaternion GetRotationFromDirection(int facingDirection)
//     {
//         float angle = facingDirection * 60f;
//         return Quaternion.Euler(0, angle, 0);
//     }
//
//     /// <summary>
//     /// Calculates the integer direction (0-5) from one hex to an adjacent hex.
//     /// Assumes 'toHexGridPos' is an immediate neighbor of 'fromHexGridPos'.
//     /// </summary>
//     private int GetHexDirection(int fromX, int fromY, int toX, int toY)
//     {
//         Vector2Int offset = new Vector2Int(toX - fromX, toY - fromY);
//         
//         if (hexController == null) 
//         {
//             Debug.LogError("GetHexDirection: HexagonController not accessible.");
//             return 0; 
//         }
//
//         Vector2Int[] currentColumnHexDirections = hexController.GetHexDirectionsForColumn(fromX);
//
//         for (int i = 0; i < currentColumnHexDirections.Length; i++)
//         {
//             if (currentColumnHexDirections[i] == offset)
//             {
//                 return i;
//             }
//         }
//         Debug.LogWarning($"GetHexDirection: Could not find direction index for offset {offset} from ({fromX},{fromY}) to ({toX},{toY}). Returning 0.");
//         return 0; 
//     }
//     
//     /// <summary>
//     /// Get the nearest walkable hex to a world position
//     /// </summary>
//     public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
//     {
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         HexagonController.HexData hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         if (hexData.gridX != -1 && hexData.isWalkable) 
//         {
//             return gridPos;
//         }
//         
//         for (int radius = 1; radius <= 10; radius++) 
//         {
//             for (int xOffset = -radius; xOffset <= radius; xOffset++)
//             {
//                 for (int zOffset = -radius; zOffset <= radius; zOffset++)
//                 {
//                     Vector2Int checkPos = new Vector2Int(gridPos.x + xOffset, gridPos.y + zOffset);
//                     HexagonController.HexData checkHexData = hexController.GetHexData(checkPos.x, checkPos.y);
//                     
//                     if (checkHexData.gridX != -1 && checkHexData.isWalkable) 
//                     {
//                         return checkPos;
//                     }
//                 }
//             }
//         }
//         
//         Debug.LogWarning($"GetNearestWalkableHex: No walkable hex found near {worldPos} within radius 10. Returning original grid position {gridPos}.");
//         return gridPos; 
//     }
//     
//     /// <summary>
//     /// Check if a path exists between two positions
//     /// </summary>
//     public bool PathExists(Vector3 startPos, Vector3 endPos, int initialFacingDirection)
//     {
//         List<VehiclePathStep> path = FindPath(startPos, endPos, initialFacingDirection);
//         return path.Count > 0;
//     }
//     
//     /// <summary>
//     /// Get the distance of a path (useful for AI decision making)
//     /// </summary>
//     public float GetPathDistance(Vector3 startPos, Vector3 endPos, int initialFacingDirection)
//     {
//         List<VehiclePathStep> path = FindPath(startPos, endPos, initialFacingDirection);
//         if (path.Count == 0) return float.MaxValue;
//         
//         float totalDistance = 0f;
//         for (int i = 0; i < path.Count - 1; i++)
//         {
//             totalDistance += Vector3.Distance(path[i].targetPosition, path[i + 1].targetPosition);
//         }
//         return totalDistance;
//     }
//     
//     public void DebugPathfinding(Vector3 startPos, Vector3 endPos, int facing)
//     {
//         Vector2Int startGrid = hexController.WorldToGridPosition(startPos);
//         Vector2Int endGrid = hexController.WorldToGridPosition(endPos);
//     
//         Debug.Log($"Debug: Start World: {startPos} -> Grid: {startGrid}");
//         Debug.Log($"Debug: End World: {endPos} -> Grid: {endGrid}");
//         Debug.Log($"Debug: Facing: {facing}");
//     
//         bool startInBounds = hexController.IsValidGridPosition(startGrid.x, startGrid.y);
//         bool endInBounds = hexController.IsValidGridPosition(endGrid.x, endGrid.y);
//     
//         Debug.Log($"Debug: Start in bounds: {startInBounds}, End in bounds: {endInBounds}");
//     
//         if (startInBounds)
//         {
//             var startHex = hexController.GetHexData(startGrid.x, startGrid.y);
//             Debug.Log($"Debug: Start hex walkable: {startHex.isWalkable}, height: {startHex.height}");
//         }
//         else
//         {
//             Debug.LogWarning($"Debug: Start grid position {startGrid} is OUT OF BOUNDS!");
//         }
//     
//         if (endInBounds)
//         {
//             var endHex = hexController.GetHexData(endGrid.x, endGrid.y);
//             Debug.Log($"Debug: End hex walkable: {endHex.isWalkable}, height: {endHex.height}");
//         }
//         else
//         {
//             Debug.LogWarning($"Debug: End grid position {endGrid} is OUT OF BOUNDS!");
//         }
//     }
//
//     // This method already exists in this class.
//     public int GetFacingDirectionFromRotation(Quaternion rotation)
//     {
//         float angle = rotation.eulerAngles.y;
//
//         // Normalize angle to 0-360 range
//         angle = angle % 360f;
//         if (angle < 0) angle += 360f;
//
//         // Convert to direction index (0-5)
//         int direction = Mathf.RoundToInt(angle / 60f) % 6;
//         return direction;
//     }
// }
