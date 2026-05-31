using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for .OrderBy() and .Any()

public class VehiclePathFinder : MonoBehaviour
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

    [Tooltip(
        "Additional cost for turning (left or right turns). Increase this value to make pathfinding prefer straight lines.")]
    public float turningCostPenalty = 3f;

    [Tooltip("Minimum number of straight hexagons the vehicle must move before it is allowed to pivot or turn.")]
    public int minStraightMovesBeforeTurn = 1; // NEW: Configurable minimum straight moves

    [Tooltip("Maximum world distance (radius) a vehicle can move from its starting point.")]
    public float maxMovementRange = 10f;

    [Tooltip("Cost penalty for final facing direction that doesn't match the ideal trajectory direction. Higher values make the vehicle prioritize ending up facing the 'arrow' direction.")]
    public float finalOrientationCostPenalty = 2f;

    [Header("Movement Settings")] [Tooltip("Speed of movement along the path")]
    public float movementSpeed = 5f;

    [Tooltip("How smoothly the object rotates to face movement direction")]
    public float rotationSpeed = 180f;

    [Tooltip("Height offset above terrain for the moving object")]
    public float heightOffset = 0.5f;

    [Tooltip("Enable smooth height interpolation between hexagons")]
    public bool smoothHeightTransition = true;

    private HexagonController hexController;

    // --- MODIFIED: A* specific data structures ---
    private Dictionary<VehicleState, float> gCost; // Cost from start to this node
    private Dictionary<VehicleState, float> hCost; // Heuristic cost from this node to target
    private Dictionary<VehicleState, VehicleState> cameFrom; // Parent node in the path
    private HashSet<VehicleState> closedSet; // Nodes already evaluated
    private List<VehicleState> openSet; // Nodes to be evaluated

    // NEW: Store the ideal final facing direction for the current pathfinding operation
    private int idealFinalFacingDirection;

    // Stores the currently running movement coroutine for a specific GameObject
    private Dictionary<VehicleController, Coroutine> activeMoveCoroutines =
        new Dictionary<VehicleController, Coroutine>();

    // Represents a vehicle's position, facing direction, AND consecutive straight moves
    public struct VehicleState : System.IEquatable<VehicleState>
    {
        public Vector2Int gridPos;
        public int facingDirection;
        public int straightMovesCount; // NEW: How many consecutive straight moves made to reach this state

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
            // Equality now includes straightMovesCount
            return gridPos == other.gridPos &&
                   facingDirection == other.facingDirection &&
                   straightMovesCount == other.straightMovesCount;
        }

        public override int GetHashCode()
        {
            // Hash combining gridPos, facingDirection, and straightMovesCount
            unchecked // Overflow is fine
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
            Debug.LogError("VehiclePathFinder requires a HexagonController component in the scene!");
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

        Debug.Log(
            $"Finding path from world {startWorldPos} (grid {startGrid}) to world {targetWorldPos} (grid {targetGrid})");

        // Early exit if target is out of max movement range
        if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
        {
            Debug.LogWarning(
                $"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
            return new List<VehiclePathStep>();
        }

        // NEW: Calculate the ideal final facing direction based on start->end trajectory
        idealFinalFacingDirection = GetIdealFacingDirection(startGrid, targetGrid);
        Debug.Log($"Ideal final facing direction calculated as: {idealFinalFacingDirection}");

        return FindPathInternal(startGrid, targetGrid, initialFacingDirection, startWorldPos);
    }

    /// <summary>
    /// NEW: Calculate the ideal facing direction from start to end hex (like an arrow)
    /// </summary>
    private int GetIdealFacingDirection(Vector2Int startGrid, Vector2Int targetGrid)
    {
        // Convert grid positions to world positions to get the direction vector
        Vector3 startWorld = hexController.GridToWorldPosition(startGrid.x, startGrid.y);
        Vector3 targetWorld = hexController.GridToWorldPosition(targetGrid.x, targetGrid.y);
        
        // Calculate the direction vector (in world space)
        Vector3 directionVector = (targetWorld - startWorld).normalized;
        
        // Convert to angle (Y rotation)
        float angle = Mathf.Atan2(directionVector.x, directionVector.z) * Mathf.Rad2Deg;
        
        // Normalize angle to 0-360 range
        angle = angle % 360f;
        if (angle < 0) angle += 360f;
        
        // Convert to hex direction index (0-5, where each direction is 60 degrees apart)
        int direction = Mathf.RoundToInt(angle / 60f) % 6;
        
        return direction;
    }

    /// <summary>
    /// NEW: Calculate the angular difference between two hex directions (0-5)
    /// Returns a value between 0 and 3 (where 3 is opposite directions)
    /// </summary>
    private int GetDirectionDifference(int dir1, int dir2)
    {
        int diff = Mathf.Abs(dir1 - dir2);
        return Mathf.Min(diff, 6 - diff); // Handle wrap-around (e.g., direction 0 and 5 are only 1 apart)
    }

    /// <summary>
    /// Find a path between two grid positions with initial facing direction
    /// </summary>
    private List<VehiclePathStep> FindPathInternal(Vector2Int startGrid, Vector2Int targetGrid,
        int initialFacingDirection, Vector3 startWorldPosForRangeCheck)
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
        hCost[startState] = GetHeuristicCost(startState, targetGrid);
        openSet.Add(startState);

        int iterations = 0;
        const int maxIterations = 50000;

        Debug.Log($"Pathfinding A* started. Initial openSet count: {openSet.Count}");

        // NEW: Keep track of all valid end states at the target to choose the best one
        List<VehicleState> validEndStates = new List<VehicleState>();

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // Get node with lowest fCost from openSet
            VehicleState currentNodeState = openSet.OrderBy(state =>
                    gCost.GetValueOrDefault(state, float.MaxValue) + hCost.GetValueOrDefault(state, float.MaxValue))
                .First();
            openSet.Remove(currentNodeState);
            closedSet.Add(currentNodeState);

            // MODIFIED: Instead of immediately returning when we reach target, collect all valid end states
            if (currentNodeState.gridPos == targetGrid)
            {
                validEndStates.Add(currentNodeState);
                
                // Continue searching for a bit more to find other possible end states
                // But if we've found several options, we can stop
                if (validEndStates.Count >= 6) // Maximum possible facing directions
                {
                    break;
                }
                
                // Don't continue processing neighbors for this state since we've reached the target
                continue;
            }

            // Check if current position is within max movement range (from original start)
            Vector3 currentNodeWorldPos =
                hexController.GetHexWorldPosition(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
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

                HexagonController.HexData nextHexData =
                    hexController.GetHexData(nextState.gridPos.x, nextState.gridPos.y);

                if (nextHexData.gridX == -1 || !nextHexData.isWalkable)
                {
                    continue;
                }

                HexagonController.HexData currentNodeHexData =
                    hexController.GetHexData(currentNodeState.gridPos.x, currentNodeState.gridPos.y);
                if (!CanMoveBetween(currentNodeHexData, nextHexData))
                {
                    continue;
                }

                float tentativeGCost = gCost.GetValueOrDefault(currentNodeState, float.MaxValue) +
                                       GetMovementCost(currentNodeState, nextState);

                if (!openSet.Contains(nextState) || tentativeGCost < gCost.GetValueOrDefault(nextState, float.MaxValue))
                {
                    cameFrom[nextState] = currentNodeState;
                    gCost[nextState] = tentativeGCost;
                    hCost[nextState] = GetHeuristicCost(nextState, targetGrid);

                    if (!openSet.Contains(nextState))
                    {
                        openSet.Add(nextState);
                    }
                }
            }
        }

        // NEW: Choose the best end state based on final orientation preference
        if (validEndStates.Count > 0)
        {
            VehicleState bestEndState = validEndStates.OrderBy(state =>
            {
                float totalCost = gCost.GetValueOrDefault(state, float.MaxValue);
                
                // Add final orientation penalty
                int directionDifference = GetDirectionDifference(state.facingDirection, idealFinalFacingDirection);
                totalCost += directionDifference * finalOrientationCostPenalty;
                
                return totalCost;
            }).First();

            Debug.Log($"Path found after {iterations} iterations! Best end state facing: {bestEndState.facingDirection} (ideal: {idealFinalFacingDirection})");
            return ReconstructPath(cameFrom, startState, bestEndState);
        }

        Debug.LogWarning($"No path found after {iterations} iterations! (Max iterations: {maxIterations})");
        return new List<VehiclePathStep>();
    }

    /// <summary>
    /// MODIFIED: Enhanced heuristic that includes final orientation preference
    /// </summary>
    private float GetHeuristicCost(VehicleState state, Vector2Int targetGrid)
    {
        float distanceCost = GetDistance(state.gridPos, targetGrid);
        
        // Add final orientation heuristic if we're close to the target
        if (state.gridPos == targetGrid)
        {
            int directionDifference = GetDirectionDifference(state.facingDirection, idealFinalFacingDirection);
            distanceCost += directionDifference * finalOrientationCostPenalty;
        }
        
        return distanceCost;
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
            Debug.LogWarning(
                $"Invalid facing direction: {currentState.facingDirection} for hex {currentState.gridPos}. Defaulting to 0.");
            currentState.facingDirection = 0;
        }

        // Get the forward hex position based on the current facing direction index
        Vector2Int forwardOffset = hexDirectionsForColumn[currentState.facingDirection];
        Vector2Int forwardPos = currentState.gridPos + forwardOffset;

        // Option 1: Move forward and continue straight
        options.Add(new VehicleState(forwardPos, currentState.facingDirection, currentState.straightMovesCount + 1));

        // Allow turning only if straightMovesCount is equal to or greater than the defined minimum.
        if (currentState.straightMovesCount >= minStraightMovesBeforeTurn) // Using the new variable
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
            Debug.Log(
                $"  - From {currentState.gridPos} (facing {currentState.facingDirection}, straight: {currentState.straightMovesCount}): Only straight move allowed. Need {minStraightMovesBeforeTurn - currentState.straightMovesCount} more straight moves to turn.");
        }

        return options;
    }

    /// <summary>
    /// Move a game object along a path
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

        Coroutine newCoroutine = StartCoroutine(MoveCoroutine(vehicle.gameObject, path, () =>
        {
            if (activeMoveCoroutines.ContainsKey(vehicle))
            {
                activeMoveCoroutines.Remove(vehicle);
            }

            onComplete?.Invoke();
        }));
        activeMoveCoroutines[vehicle] = newCoroutine;
    }

    private System.Collections.IEnumerator MoveCoroutine(GameObject obj, List<VehiclePathStep> path,
        System.Action onComplete)
    {
        Transform objTransform = obj.transform;

        // For the first step, we need to rotate from the vehicle's current orientation
        // to the direction of the first movement segment.
        if (path.Count > 0)
        {
            Quaternion initialTargetRotation = path[0].targetRotation;
            while (Quaternion.Angle(objTransform.rotation, initialTargetRotation) > 0.1f)
            {
                objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, initialTargetRotation,
                    rotationSpeed * Time.deltaTime);
                yield return null;
            }

            objTransform.rotation = initialTargetRotation;
        }

        for (int i = 0; i < path.Count; i++)
        {
            VehiclePathStep currentStep = path[i];

            // Now, move to the target position
            if (Vector3.Distance(objTransform.position, currentStep.targetPosition) > 0.01f)
            {
                Vector3 startPos = objTransform.position;
                float journeyLength = Vector3.Distance(startPos, currentStep.targetPosition);
                float journeyTime = journeyLength / movementSpeed;
                float elapsedTime = 0;

                while (elapsedTime < journeyTime)
                {
                    float t = elapsedTime / journeyTime;
                    objTransform.position = Vector3.Lerp(startPos, currentStep.targetPosition, t);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
            }

            objTransform.position = currentStep.targetPosition;

            // For subsequent steps (if any), rotate to face the *next* segment's direction
            if (i < path.Count - 1)
            {
                VehiclePathStep nextStep = path[i + 1];
                Quaternion nextSegmentRotation = nextStep.targetRotation;

                while (Quaternion.Angle(objTransform.rotation, nextSegmentRotation) > 0.1f)
                {
                    objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, nextSegmentRotation,
                        rotationSpeed * Time.deltaTime);
                    yield return null;
                }

                objTransform.rotation = nextSegmentRotation;
            }
        }

        onComplete?.Invoke();
    }

    private bool CanMoveBetween(HexagonController.HexData fromHex, HexagonController.HexData toHex)
    {
        float heightDifference = toHex.height - fromHex.height;
        return heightDifference <= maxClimbHeight;
    }

    private float GetMovementCost(VehicleState fromState, VehicleState toState)
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

    private List<VehiclePathStep> ReconstructPath(Dictionary<VehicleState, VehicleState> cameFrom,
        VehicleState startState, VehicleState endState)
    {
        List<VehicleState> pathStates = new List<VehicleState>();
        VehicleState? currentState = endState;

        while (currentState.HasValue && cameFrom.ContainsKey(currentState.Value))
        {
            pathStates.Add(currentState.Value);
            currentState = cameFrom[currentState.Value];
        }

        if (currentState.HasValue)
        {
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
                int directionIndex = GetHexDirection(prevState.gridPos.x, prevState.gridPos.y, state.gridPos.x,
                    state.gridPos.y);
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

        Debug.LogWarning(
            $"GetHexDirection: Could not find direction index for offset {offset} from ({fromX},{fromY}) to ({toX},{toY}). Returning 0.");
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

        Debug.LogWarning(
            $"GetNearestWalkableHex: No walkable hex found near {worldPos} within radius 10. Returning original grid position {gridPos}.");
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