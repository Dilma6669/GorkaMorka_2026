using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VehiclePathFinderHybrid: MonoBehaviour
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

    [Tooltip("Additional cost applied when the unit changes its movement direction.")]
    public float directionChangePenalty = 0.5f; 

    [Tooltip("Maximum world distance (radius) a unit can move from its starting point.")]
    public float maxMovementRange = 10f;
    
    [Header("Movement Settings")]
    [Tooltip("Speed of movement to the destination")]
    public float movementSpeed = 5f;
    
    [Tooltip("How smoothly the object rotates to face movement direction")]
    public float rotationSpeed = 180f;
    
    [Tooltip("Height offset above terrain for the moving object")]
    public float heightOffset = 0.5f;
    
    [Header("Smooth Turning Settings")]
    // Removed: [Tooltip("Distance the vehicle must move forward before it can start turning")]
    // Removed: public float forwardMovementDistance = 2f;
    
    [Tooltip("Influence on how tightly the vehicle can turn (lower = tighter turns, higher = wider turns)")]
    public float turningRadius = 2f; // Now directly influences turn arc
    
    [Tooltip("How quickly the vehicle adjusts its heading towards the target")]
    public float headingAdjustmentSpeed = 60f; 
    
    [Tooltip("Minimum distance to destination before using smooth turning")]
    public float smoothTurningMinDistance = 3f;
    
    [Tooltip("How often to update the vehicle's heading during smooth movement (in seconds)")]
    public float headingUpdateInterval = 0.1f;
    
    [Tooltip("Distance threshold for switching to direct movement to avoid orbiting")]
    public float directMovementThreshold = 1f;
    
    private HexagonController hexController;
    private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();

    // Stores the currently running movement coroutine for a specific GameObject
    private Dictionary<VehicleController, Coroutine> activeMoveCoroutines = new Dictionary<VehicleController, Coroutine>();
    
    // A* pathfinding data structures
    private class HexNode
    {
        public Vector2Int gridPos;
        public float gCost; // Distance from start
        public float hCost; // Distance to target (heuristic)
        public float fCost => gCost + hCost; // Total cost
        public HexNode parent;
        public bool isWalkable;
        public float height;
        public bool isRock;
        public Vector2Int cameFromDirection;

        public HexNode(Vector2Int pos, bool walkable, float h, bool rock, Vector2Int cameFromDir)
        {
            gridPos = pos;
            isWalkable = walkable;
            height = h;
            isRock = rock;
            cameFromDirection = cameFromDir;
        }
    }

    /// <summary>
    /// Simple movement data containing only the final destination
    /// </summary>
    public struct VehicleMovementData
    {
        public Vector3 finalDestination;
        public Quaternion finalRotation;
        public bool pathExists;
        public bool useSmoothTurning;

        public VehicleMovementData(Vector3 destination, Quaternion rotation, bool hasPath, bool smoothTurning = false)
        {
            finalDestination = destination;
            finalRotation = rotation;
            pathExists = hasPath;
            useSmoothTurning = smoothTurning;
        }
    }
    
    void Start()
    {
        hexController = FindObjectOfType<HexagonController>(); 
        if (hexController == null)
        {
            Debug.LogError("VehiclePathFinderSimple requires a HexagonController component in the scene!");
        }
    }
    
    /// <summary>
    /// Calculate movement data for direct movement between two world positions
    /// </summary>
    public VehicleMovementData CalculateMovement(Vector3 startWorldPos, Vector3 targetWorldPos)
    {
        Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
        Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);

        // Early exit if target is out of max movement range
        if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
        {
            Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
            return new VehicleMovementData(startWorldPos, Quaternion.identity, false);
        }
        
        return CalculateMovement(startGrid, targetGrid, startWorldPos);
    }
    
    /// <summary>
    /// Calculate movement data for direct movement between two grid positions
    /// </summary>
    private VehicleMovementData CalculateMovement(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
    {
        if (hexController == null)
        {
            Debug.LogError("HexagonController not found. Cannot calculate movement.");
            return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
        }
        
        // Check if a valid path exists using A* pathfinding
        bool pathExists = FindPathExists(startGrid, targetGrid, startWorldPosForRangeCheck);
        
        if (!pathExists)
        {
            Debug.LogWarning($"No valid path found from {startGrid} to {targetGrid}");
            return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
        }
        
        // Calculate final destination world position
        Vector3 finalDestination = hexController.GetHexWorldPosition(targetGrid.x, targetGrid.y);
        finalDestination.y += hexController.hexMeshHeight * 0.5f;
        finalDestination.y += heightOffset;
        
        // Calculate rotation to face the destination
        Vector3 directionToTarget = (finalDestination - startWorldPosForRangeCheck).normalized;
        directionToTarget.y = 0; // Flatten for rotation
        
        Quaternion finalRotation = Quaternion.identity;
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            finalRotation = Quaternion.LookRotation(directionToTarget);
        }
        
        // Determine if we should use smooth turning based on distance
        float distanceToTarget = Vector3.Distance(startWorldPosForRangeCheck, finalDestination);
        bool useSmoothTurning = distanceToTarget >= smoothTurningMinDistance;
        
        return new VehicleMovementData(finalDestination, finalRotation, true, useSmoothTurning);
    }
    
    /// <summary>
    /// Move a vehicle directly to the destination (ignoring the path)
    /// </summary>
    public void MoveToDestination(VehicleController vehicle, Vector3 startPos, Vector3 targetPos, System.Action onComplete = null)
    {
        VehicleMovementData movementData = CalculateMovement(startPos, targetPos);
        
        if (!movementData.pathExists)
        {
            Debug.LogWarning("Cannot move vehicle - no valid path exists");
            onComplete?.Invoke();
            return;
        }
        
        MoveToDestination(vehicle, movementData, onComplete);
    }
    
    /// <summary>
    /// Move a vehicle directly to the destination using pre-calculated movement data
    /// </summary>
    public void MoveToDestination(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete = null)
    {
        if (!movementData.pathExists)
        {
            Debug.LogWarning("Cannot move vehicle - movement data indicates no valid path");
            onComplete?.Invoke();
            return;
        }
        
        // Stop any existing movement coroutine for this vehicle
        if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
        {
            StopCoroutine(activeMoveCoroutines[vehicle]);
            activeMoveCoroutines.Remove(vehicle);
        }

        // Choose the appropriate movement coroutine based on whether we're using smooth turning
        Coroutine newCoroutine;
        if (movementData.useSmoothTurning)
        {
            newCoroutine = StartCoroutine(MoveSmoothContinuousCoroutine(vehicle, movementData, () => {
                if (activeMoveCoroutines.ContainsKey(vehicle))
                {
                    activeMoveCoroutines.Remove(vehicle);
                }
                onComplete?.Invoke();
            }));
        }
        else
        {
            newCoroutine = StartCoroutine(MoveDirectlyCoroutine(vehicle, movementData, () => {
                if (activeMoveCoroutines.ContainsKey(vehicle))
                {
                    activeMoveCoroutines.Remove(vehicle);
                }
                onComplete?.Invoke();
            }));
        }
        
        activeMoveCoroutines[vehicle] = newCoroutine;
    }
    
    private System.Collections.IEnumerator MoveDirectlyCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
    {
        Transform vehicleTransform = vehicle.transform;
        Vector3 startPos = vehicleTransform.position;
        
        // First, rotate to face the destination
        while (Quaternion.Angle(vehicleTransform.rotation, movementData.finalRotation) > 0.1f)
        {
            vehicleTransform.rotation = Quaternion.RotateTowards(vehicleTransform.rotation, movementData.finalRotation, rotationSpeed * Time.deltaTime);
            yield return null;
        }
        
        // Then move directly to the destination with terrain following
        float journeyLength = Vector3.Distance(startPos, movementData.finalDestination);
        float journeyTime = journeyLength / movementSpeed;
        float elapsedTime = 0;
        
        while (elapsedTime < journeyTime)
        {
            float t = elapsedTime / journeyTime;
            Vector3 currentPos = Vector3.Lerp(startPos, movementData.finalDestination, t);
            
            // Adjust height based on terrain at current position
            currentPos.y = GetTerrainHeightAtPosition(currentPos) + heightOffset;
            
            vehicleTransform.position = currentPos;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we end up exactly at the destination and set final rotation
        vehicleTransform.position = movementData.finalDestination;
        vehicleTransform.rotation = movementData.finalRotation; 
        
        onComplete?.Invoke();
    }
    
    private System.Collections.IEnumerator MoveSmoothContinuousCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
    {
        Transform vehicleTransform = vehicle.transform;
        Vector3 currentPosition = vehicleTransform.position;
        Vector3 startPosition = currentPosition; 
        Vector3 currentVelocity = vehicleTransform.forward * movementSpeed;
        
        float lastHeadingUpdate = 0f;
        
        // Track the previous position to calculate distance traveled
        Vector3 previousPosition = currentPosition;
        
        while (Vector3.Distance(currentPosition, movementData.finalDestination) > 0.1f)
        {
            float deltaTime = Time.deltaTime;
            float distanceToTarget = Vector3.Distance(currentPosition, movementData.finalDestination);
            
            // Switch to direct movement when very close to destination to avoid orbiting
            if (distanceToTarget <= directMovementThreshold)
            {
                // Direct movement to destination
                Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
                currentVelocity = directionToTarget * movementSpeed;
                
                // Move directly towards destination
                Vector3 movement = currentVelocity * deltaTime;
                
                // Don't overshoot the destination
                if (movement.magnitude >= distanceToTarget)
                {
                    currentPosition = movementData.finalDestination;
                }
                else
                {
                    currentPosition += movement;
                }
            }
            else
            {
                // Normal smooth turning behavior
                
                // Update heading periodically
                if (Time.time - lastHeadingUpdate >= headingUpdateInterval)
                {
                    Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
                    directionToTarget.y = 0; // Keep on horizontal plane
                    
                    if (directionToTarget.sqrMagnitude > 0.001f)
                    {
                        // Calculate the desired velocity direction
                        Vector3 desiredVelocity = directionToTarget * movementSpeed;
                        
                        // Smoothly adjust current velocity towards desired velocity
                        // This creates the curved motion effect.
                        // A larger turningRadius means a smaller maxTurnRate, leading to wider turns.
                        float maxTurnRate = (headingAdjustmentSpeed / turningRadius) * Mathf.Deg2Rad; 
                        
                        Vector3 velocityChange = Vector3.RotateTowards(
                            currentVelocity.normalized, 
                            desiredVelocity.normalized, 
                            maxTurnRate * deltaTime, 
                            0f
                        );
                        
                        currentVelocity = velocityChange * movementSpeed;
                    }
                    
                    lastHeadingUpdate = Time.time;
                }
                
                // Move the vehicle
                Vector3 movement = currentVelocity * deltaTime;
                currentPosition += movement;
                
                // Slow down as we approach the destination to avoid overshooting
                if (distanceToTarget < turningRadius) 
                {
                    float speedMultiplier = Mathf.Clamp01(distanceToTarget / turningRadius);
                    currentVelocity = currentVelocity.normalized * (movementSpeed * speedMultiplier);
                }
            }
            
            previousPosition = currentPosition; 
            
            // Adjust height based on terrain
            currentPosition.y = GetTerrainHeightAtPosition(currentPosition) + heightOffset;
            
            // Update vehicle position
            vehicleTransform.position = currentPosition;
            
            // Smoothly rotate to face the movement direction
            // IMPORTANT: Only rotate if we are not yet at the final destination (or very close)
            // This prevents any final "snapping" rotations as the vehicle reaches its target.
            if (currentVelocity.sqrMagnitude > 0.001f && distanceToTarget > 0.1f) 
            {
                Vector3 lookDirection = currentVelocity.normalized;
                lookDirection.y = 0; // Keep rotation on horizontal plane
                
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    vehicleTransform.rotation = Quaternion.RotateTowards(
                        vehicleTransform.rotation, 
                        targetRotation, 
                        rotationSpeed * deltaTime
                    );
                }
            }
            
            yield return null;
        }
        
        // Final positioning: Ensure the vehicle is exactly at the destination.
        // The rotation should be whatever it was when the loop condition became false.
        Vector3 finalDestination = movementData.finalDestination;
        finalDestination.y = GetTerrainHeightAtPosition(finalDestination) + heightOffset;
        vehicleTransform.position = finalDestination;
        
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Check if a valid path exists between two grid positions (using A* pathfinding)
    /// </summary>
    private bool FindPathExists(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
    {
        // Clear previous search data
        nodeCache.Clear();
        
        // Initialize nodes
        HexNode startNode = GetNode(startGrid);
        startNode.cameFromDirection = Vector2Int.zero;
        
        HexNode targetNode = GetNode(targetGrid);
        
        if (startNode == null || targetNode == null || !targetNode.isWalkable)
        {
            return false;
        }
        
        // A* algorithm (simplified - just checking if path exists)
        List<HexNode> openSet = new List<HexNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        
        openSet.Add(startNode);
        startNode.gCost = 0;
        startNode.hCost = GetDistance(startGrid, targetGrid);
        
        while (openSet.Count > 0)
        {
            HexNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || 
                    (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }
            
            openSet.Remove(currentNode);
            closedSet.Add(currentNode.gridPos);
            
            if (currentNode.gridPos == targetGrid)
            {
                return true; // Path found
            }
            
            Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
            
            foreach (Vector2Int neighborPos in neighbors)
            {
                if (closedSet.Contains(neighborPos)) continue;
                
                HexNode neighborNode = GetNode(neighborPos);
                if (neighborNode == null) continue;
                if (!neighborNode.isWalkable) continue;

                // Check if neighbor is within max movement range
                Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
                if (Vector3.Distance(startWorldPosForRangeCheck, neighborWorldPos) > maxMovementRange)
                {
                    continue;
                }
                
                neighborNode.cameFromDirection = neighborPos - currentNode.gridPos;
                if (!CanMoveBetween(currentNode, neighborNode)) continue;
                
                float moveCost = GetMovementCost(currentNode, neighborNode);
                float newGCost = currentNode.gCost + moveCost;
                
                if (newGCost < neighborNode.gCost || !openSet.Contains(neighborNode))
                {
                    neighborNode.gCost = newGCost;
                    neighborNode.hCost = GetDistance(neighborPos, targetGrid);
                    neighborNode.parent = currentNode;
                    
                    if (!openSet.Contains(neighborNode))
                    {
                        openSet.Add(neighborNode);
                    }
                }
            }
        }
        
        return false; // No path found
    }
    
    private HexNode GetNode(Vector2Int gridPos)
    {
        if (nodeCache.ContainsKey(gridPos))
        {
            return nodeCache[gridPos];
        }
        
        if (hexController == null || gridPos.x < 0 || gridPos.x >= hexController.gridWidth || 
            gridPos.y < 0 || gridPos.y >= hexController.gridHeight)
        {
            return null;
        }
        
        var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
        HexNode node = new HexNode(gridPos, hexData.isWalkable, hexData.height, hexData.isRock, Vector2Int.zero);
        nodeCache[gridPos] = node;
        return node;
    }
    
    private Vector2Int[] GetHexNeighbors(int x, int z)
    {
        if (x % 2 == 0)
        {
            return new Vector2Int[]
            {
                new Vector2Int(x, z - 1),     // Top
                new Vector2Int(x + 1, z - 1), // Top Right
                new Vector2Int(x + 1, z),     // Bottom Right
                new Vector2Int(x, z + 1),     // Bottom
                new Vector2Int(x - 1, z),     // Bottom Left
                new Vector2Int(x - 1, z - 1)  // Top Left
            };
        }
        else
        {
            return new Vector2Int[]
            {
                new Vector2Int(x, z - 1),     // Top
                new Vector2Int(x + 1, z),     // Top Right
                new Vector2Int(x + 1, z + 1), // Bottom Right
                new Vector2Int(x, z + 1),     // Bottom
                new Vector2Int(x - 1, z + 1), // Bottom Left
                new Vector2Int(x - 1, z)      // Top Left
            };
        }
    }
    
    private bool CanMoveBetween(HexNode from, HexNode to)
    {
        float heightDifference = to.height - from.height;
        return heightDifference <= maxClimbHeight;
    }
    
    private float GetMovementCost(HexNode from, HexNode to)
    {
        float baseCost = 1f;
        float heightDifference = to.height - from.height;
        
        if (heightDifference > 0)
        {
            baseCost *= uphillCostMultiplier;
        }
        else if (heightDifference < 0)
        {
            baseCost *= downhillCostMultiplier;
        }
        
        if (to.isRock)
        {
            baseCost *= rockTerrainCostMultiplier;
        }

        Vector2Int currentMoveDirection = to.gridPos - from.gridPos;
        if (from.parent != null && from.cameFromDirection != Vector2Int.zero && from.cameFromDirection != currentMoveDirection)
        {
            baseCost += directionChangePenalty;
        }
        
        return baseCost;
    }
    
    private float GetDistance(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        
        if (Mathf.Sign(dx) == Mathf.Sign(dy))
        {
            return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        }
        else
        {
            return Mathf.Abs(dx) + Mathf.Abs(dy);
        }
    }
    
    /// <summary>
    /// Get the nearest walkable hex to a world position
    /// </summary>
    public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
    {
        Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
        
        HexNode node = GetNode(gridPos);
        if (node != null && node.isWalkable)
        {
            return gridPos;
        }
        
        for (int radius = 1; radius <= 10; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                    HexNode checkNode = GetNode(checkPos);
                    
                    if (checkNode != null && checkNode.isWalkable)
                    {
                        return checkPos;
                    }
                }
            }
        }
        
        return gridPos;
    }
    
    /// <summary>
    /// Check if a valid path exists between two positions
    /// </summary>
    public bool PathExists(Vector3 startPos, Vector3 endPos)
    {
        VehicleMovementData movementData = CalculateMovement(startPos, endPos);
        return movementData.pathExists;
    }
    
    /// <summary>
    /// Get the straight-line distance to the destination (since we move directly)
    /// </summary>
    public float GetMovementDistance(Vector3 startPos, Vector3 endPos)
    {
        VehicleMovementData movementData = CalculateMovement(startPos, endPos);
        if (!movementData.pathExists) return float.MaxValue;
        
        return Vector3.Distance(startPos, movementData.finalDestination);
    }
    
    /// <summary>
    /// Get the terrain height at a specific world position by sampling the hexagon grid
    /// </summary>
    private float GetTerrainHeightAtPosition(Vector3 worldPos)
    {
        if (hexController == null) return worldPos.y;
        
        Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
        
        // Get the hex data at this position
        var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
        if (hexController.IsValidGridPosition(gridPos.x, gridPos.y))
        {
            // Return the hex height plus half the mesh height (to get the surface level)
            return hexData.height + hexController.hexMeshHeight * 0.5f;
        }
        
        // If we can't get hex data, try to interpolate from nearby hexes
        return InterpolateTerrainHeight(worldPos);
    }
    
    /// <summary>
    /// Interpolate terrain height from nearby hexagons for smoother movement
    /// </summary>
    private float InterpolateTerrainHeight(Vector3 worldPos)
    {
        Vector2Int centerGrid = hexController.WorldToGridPosition(worldPos);
        Vector2Int[] neighbors = GetHexNeighbors(centerGrid.x, centerGrid.y);
        
        float totalHeight = 0f;
        float totalWeight = 0f;
        
        // Include the center hex
        Vector3 centerWorldPos = hexController.GetHexWorldPosition(centerGrid.x, centerGrid.y);
        float centerDistance = Vector3.Distance(worldPos, centerWorldPos);
        if (centerDistance < 0.1f) centerDistance = 0.1f; // Prevent division by zero
        
        var centerHexData = hexController.GetHexData(centerGrid.x, centerGrid.y);
        if (hexController.IsValidGridPosition(centerGrid.x, centerGrid.y))
        {
            float centerWeight = 1f / centerDistance;
            totalHeight += (centerHexData.height + hexController.hexMeshHeight * 0.5f) * centerWeight;
            totalWeight += centerWeight;
        }
        
        // Sample neighboring hexes
        foreach (Vector2Int neighborPos in neighbors)
        {
            var neighborHexData = hexController.GetHexData(neighborPos.x, neighborPos.y);
            if (hexController.IsValidGridPosition(neighborPos.x, neighborPos.y))
            {
                Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
                float distance = Vector3.Distance(worldPos, neighborWorldPos);
                if (distance < 0.1f) distance = 0.1f; // Prevent division by zero
                
                float weight = 1f / distance;
                totalHeight += (neighborHexData.height + hexController.hexMeshHeight * 0.5f) * weight;
                totalWeight += weight;
            }
        }
        
        if (totalWeight > 0f)
        {
            return totalHeight / totalWeight;
        }
        
        // Fallback to current Y position if no valid hex data found
        return worldPos.y;
    }

    #region ReachabilityCheckerMethods
    // Add these methods to your VehiclePathFinderHybrid class

    /// <summary>
    /// Check if a vehicle can reach its destination without looping
    /// </summary>
    public bool CanReachDirectly(Vector3 startPos, Vector3 targetPos, Vector3 currentFacing, float vehicleMovementSpeed)
    {
        return VehicleReachabilityChecker.CanReachDirectly(
            startPos, targetPos, currentFacing, vehicleMovementSpeed,
            turningRadius, headingAdjustmentSpeed, smoothTurningMinDistance, directMovementThreshold);
    }

    /// <summary>
    /// Get all hexagons that can be reached directly (without looping) for a specific vehicle
    /// </summary>
    public List<Vector2Int> GetDirectlyReachableHexagons(VehicleController vehicle)
    {
        List<Vector2Int> reachableHexes = new List<Vector2Int>();
        Vector2Int currentGridPos = hexController.WorldToGridPosition(vehicle.transform.position);
        
        // Iterate through a reasonable range of hexes around the vehicle to check reachability
        // This is a simplified approach; for a large map, you might need a more optimized search.
        int searchRadius = (int)(maxMovementRange / hexController.hexSize) + 5; // Search a bit beyond max movement range
        
        for (int x = currentGridPos.x - searchRadius; x <= currentGridPos.x + searchRadius; x++)
        {
            for (int y = currentGridPos.y - searchRadius; y <= currentGridPos.y + searchRadius; y++)
            {
                Vector2Int potentialTargetGrid = new Vector2Int(x, y);
                if (!hexController.IsValidGridPosition(x, y)) continue;

                var hexData = hexController.GetHexData(x, y);
                if (!hexData.isWalkable) continue;

                Vector3 potentialTargetWorldPos = hexController.GetHexWorldPosition(x, y);
                potentialTargetWorldPos.y += hexController.hexMeshHeight * 0.5f; // Center of hex top

                // Check if within max movement range first
                if (Vector3.Distance(vehicle.transform.position, potentialTargetWorldPos) > maxMovementRange)
                {
                    continue;
                }

                // Use the new prediction method
                if (CanReachDirectly(vehicle.transform.position, potentialTargetWorldPos, vehicle.transform.forward, movementSpeed))
                {
                    reachableHexes.Add(potentialTargetGrid);
                }
            }
        }
        return reachableHexes;
    }

    /// <summary>
    /// Get all hexagons that can be reached directly with a specific movement speed
    /// </summary>
    public List<Vector2Int> GetDirectlyReachableHexagonsWithSpeed(Vector3 vehiclePos, Vector3 vehicleFacing,
        float customMovementSpeed)
    {
        List<Vector2Int> reachableHexes = new List<Vector2Int>();
        Vector2Int currentGridPos = hexController.WorldToGridPosition(vehiclePos);
        
        int searchRadius = (int)(maxMovementRange / hexController.hexSize) + 5; 
        
        for (int x = currentGridPos.x - searchRadius; x <= currentGridPos.x + searchRadius; x++)
        {
            for (int y = currentGridPos.y - searchRadius; y <= currentGridPos.y + searchRadius; y++)
            {
                Vector2Int potentialTargetGrid = new Vector2Int(x, y);
                if (!hexController.IsValidGridPosition(x, y)) continue;

                var hexData = hexController.GetHexData(x, y);
                if (!hexData.isWalkable) continue;

                Vector3 potentialTargetWorldPos = hexController.GetHexWorldPosition(x, y);
                potentialTargetWorldPos.y += hexController.hexMeshHeight * 0.5f;

                if (Vector3.Distance(vehiclePos, potentialTargetWorldPos) > maxMovementRange)
                {
                    continue;
                }

                if (CanReachDirectly(vehiclePos, potentialTargetWorldPos, vehicleFacing, customMovementSpeed))
                {
                    reachableHexes.Add(potentialTargetGrid);
                }
            }
        }
        return reachableHexes;
    }

    /// <summary>
    /// Check if changing movement speed would allow direct access to a target
    /// </summary>
    public bool CanReachDirectlyWithSpeed(Vector3 startPos, Vector3 targetPos, Vector3 currentFacing, float testSpeed)
    {
        return VehicleReachabilityChecker.CanReachDirectly(
            startPos, targetPos, currentFacing, testSpeed,
            turningRadius, headingAdjustmentSpeed, smoothTurningMinDistance, directMovementThreshold);
    }

    /// <summary>
    /// Find the minimum speed needed to reach a target directly
    /// </summary>
    public float FindMinimumSpeedForDirectReach(Vector3 startPos, Vector3 targetPos, Vector3 currentFacing)
    {
        float minSpeed = 1f;
        float maxSpeed = 20f; // Reasonable upper bound for binary search
        float precision = 0.1f; // How accurate the speed needs to be

        // Binary search for minimum speed
        while (maxSpeed - minSpeed > precision)
        {
            float testSpeed = (minSpeed + maxSpeed) / 2f;

            if (CanReachDirectlyWithSpeed(startPos, targetPos, currentFacing, testSpeed))
            {
                maxSpeed = testSpeed; // Try a lower speed
            }
            else
            {
                minSpeed = testSpeed; // Need a higher speed
            }
        }

        return maxSpeed; // Return the lowest speed that allows direct reach
    }

    /// <summary>
    /// Get movement classification for a target
    /// </summary>
    public MovementType GetMovementType(Vector3 startPos, Vector3 targetPos, Vector3 currentFacing, float vehicleSpeed)
    {
        // First, check if a path exists at all (A* based)
        if (!PathExists(startPos, targetPos))
        {
            return MovementType.Unreachable;
        }

        float distance = Vector3.Distance(startPos, targetPos);

        // Classify based on distance and direct reachability
        if (distance <= directMovementThreshold)
        {
            return MovementType.DirectClose; // Very close, will use direct movement
        }

        // If it's within smoothTurningMinDistance but greater than directMovementThreshold,
        // it will still use direct movement (not smooth continuous turning).
        if (distance < smoothTurningMinDistance)
        {
            // Even if direct, check if the initial angle is too sharp for a clean direct approach
            Vector3 initialDirectionToTarget = (targetPos - startPos).normalized;
            float initialAngle = Vector3.Angle(currentFacing, initialDirectionToTarget);
            if (initialAngle > 90f) // If pointing significantly away, it's not a "clean" direct far
            {
                // This might still result in a small adjustment, but technically it's direct movement.
                // We could introduce a "DirectFarWithAdjustment" if more granularity is needed.
                return MovementType.DirectFar; 
            }
            return MovementType.DirectFar;
        }

        // For distances beyond smoothTurningMinDistance, check if smooth turning can hit directly
        if (CanReachDirectly(startPos, targetPos, currentFacing, vehicleSpeed))
        {
            return MovementType.SmoothDirect; // Uses smooth turning and hits directly
        }

        return MovementType.RequiresLooping; // Uses smooth turning but will overshoot the target and need to loop back
    }

    /// <summary>
    /// Defines the different types of movement outcomes for a vehicle.
    /// </summary>
    public enum MovementType
    {
        Unreachable, // No valid path exists (e.g., blocked by terrain, out of range)
        DirectClose, // Very close to target, moves directly without complex turning logic
        DirectFar, // Further away but still uses direct movement (before smooth turning kicks in)
        SmoothDirect, // Uses smooth continuous turning and can reach the target directly without overshooting
        RequiresLooping // Uses smooth continuous turning but will overshoot the target and need to loop back
    }
    
    #endregion
}




// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;
//
// public class VehiclePathFinderHybrid : MonoBehaviour
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
//     [Tooltip("Additional cost applied when the unit changes its movement direction.")]
//     public float directionChangePenalty = 0.5f;
//
//     [Tooltip("Maximum world distance (radius) a unit can move from its starting point.")]
//     public float maxMovementRange = 10f;
//
//     [Header("Movement Settings")] [Tooltip("Speed of movement to the destination")]
//     public float movementSpeed = 5f;
//
//     [Tooltip("How smoothly the object rotates to face movement direction")]
//     public float rotationSpeed = 180f;
//
//     [Tooltip("Height offset above terrain for the moving object")]
//     public float heightOffset = 0.5f;
//
//     [Header("Smooth Turning Settings")]
//     // Removed: [Tooltip("Distance the vehicle must move forward before it can start turning")]
//     // Removed: public float forwardMovementDistance = 2f;
//
//     [Tooltip("Influence on how tightly the vehicle can turn (lower = tighter turns, higher = wider turns)")]
//     public float turningRadius = 2f; // Now directly influences turn arc
//
//     [Tooltip("How quickly the vehicle adjusts its heading towards the target")]
//     public float headingAdjustmentSpeed = 60f;
//
//     [Tooltip("Minimum distance to destination before using smooth turning")]
//     public float smoothTurningMinDistance = 3f;
//
//     [Tooltip("How often to update the vehicle's heading during smooth movement (in seconds)")]
//     public float headingUpdateInterval = 0.1f;
//
//     [Tooltip("Distance threshold for switching to direct movement to avoid orbiting")]
//     public float directMovementThreshold = 1f;
//
//     private HexagonController hexController;
//     private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();
//
//     // Stores the currently running movement coroutine for a specific GameObject
//     private Dictionary<VehicleController, Coroutine> activeMoveCoroutines =
//         new Dictionary<VehicleController, Coroutine>();
//
//     // A* pathfinding data structures
//     private class HexNode
//     {
//         public Vector2Int gridPos;
//         public float gCost; // Distance from start
//         public float hCost; // Distance to target (heuristic)
//         public float fCost => gCost + hCost; // Total cost
//         public HexNode parent;
//         public bool isWalkable;
//         public float height;
//         public bool isRock;
//         public Vector2Int cameFromDirection;
//
//         public HexNode(Vector2Int pos, bool walkable, float h, bool rock, Vector2Int cameFromDir)
//         {
//             gridPos = pos;
//             isWalkable = walkable;
//             height = h;
//             isRock = rock;
//             cameFromDirection = cameFromDir;
//         }
//     }
//
//     /// <summary>
//     /// Simple movement data containing only the final destination
//     /// </summary>
//     public struct VehicleMovementData
//     {
//         public Vector3 finalDestination;
//         public Quaternion finalRotation;
//         public bool pathExists;
//         public bool useSmoothTurning;
//
//         public VehicleMovementData(Vector3 destination, Quaternion rotation, bool hasPath, bool smoothTurning = false)
//         {
//             finalDestination = destination;
//             finalRotation = rotation;
//             pathExists = hasPath;
//             useSmoothTurning = smoothTurning;
//         }
//     }
//
//     void Start()
//     {
//         hexController = FindObjectOfType<HexagonController>();
//         if (hexController == null)
//         {
//             Debug.LogError("VehiclePathFinderSimple requires a HexagonController component in the scene!");
//         }
//     }
//
//     /// <summary>
//     /// Calculate movement data for direct movement between two world positions
//     /// </summary>
//     public VehicleMovementData CalculateMovement(Vector3 startWorldPos, Vector3 targetWorldPos)
//     {
//         Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
//         Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);
//
//         // Early exit if target is out of max movement range
//         if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
//         {
//             Debug.LogWarning(
//                 $"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
//             return new VehicleMovementData(startWorldPos, Quaternion.identity, false);
//         }
//
//         return CalculateMovement(startGrid, targetGrid, startWorldPos);
//     }
//
//     /// <summary>
//     /// Calculate movement data for direct movement between two grid positions
//     /// </summary>
//     private VehicleMovementData CalculateMovement(Vector2Int startGrid, Vector2Int targetGrid,
//         Vector3 startWorldPosForRangeCheck)
//     {
//         if (hexController == null)
//         {
//             Debug.LogError("HexagonController not found. Cannot calculate movement.");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//
//         // Check if a valid path exists using A* pathfinding
//         bool pathExists = FindPathExists(startGrid, targetGrid, startWorldPosForRangeCheck);
//
//         if (!pathExists)
//         {
//             Debug.LogWarning($"No valid path found from {startGrid} to {targetGrid}");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//
//         // Calculate final destination world position
//         Vector3 finalDestination = hexController.GetHexWorldPosition(targetGrid.x, targetGrid.y);
//         finalDestination.y += hexController.hexMeshHeight * 0.5f;
//         finalDestination.y += heightOffset;
//
//         // Calculate rotation to face the destination
//         Vector3 directionToTarget = (finalDestination - startWorldPosForRangeCheck).normalized;
//         directionToTarget.y = 0; // Flatten for rotation
//
//         Quaternion finalRotation = Quaternion.identity;
//         if (directionToTarget.sqrMagnitude > 0.001f)
//         {
//             finalRotation = Quaternion.LookRotation(directionToTarget);
//         }
//
//         // Determine if we should use smooth turning based on distance
//         float distanceToTarget = Vector3.Distance(startWorldPosForRangeCheck, finalDestination);
//         bool useSmoothTurning = distanceToTarget >= smoothTurningMinDistance;
//
//         return new VehicleMovementData(finalDestination, finalRotation, true, useSmoothTurning);
//     }
//
//     /// <summary>
//     /// Move a vehicle directly to the destination (ignoring the path)
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, Vector3 startPos, Vector3 targetPos,
//         System.Action onComplete = null)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, targetPos);
//
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - no valid path exists");
//             onComplete?.Invoke();
//             return;
//         }
//
//         MoveToDestination(vehicle, movementData, onComplete);
//     }
//
//     /// <summary>
//     /// Move a vehicle directly to the destination using pre-calculated movement data
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, VehicleMovementData movementData,
//         System.Action onComplete = null)
//     {
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - movement data indicates no valid path");
//             onComplete?.Invoke();
//             return;
//         }
//
//         // Stop any existing movement coroutine for this vehicle
//         if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
//         {
//             StopCoroutine(activeMoveCoroutines[vehicle]);
//             activeMoveCoroutines.Remove(vehicle);
//         }
//
//         // Choose the appropriate movement coroutine based on whether we're using smooth turning
//         Coroutine newCoroutine;
//         if (movementData.useSmoothTurning)
//         {
//             newCoroutine = StartCoroutine(MoveSmoothContinuousCoroutine(vehicle, movementData, () =>
//             {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//
//                 onComplete?.Invoke();
//             }));
//         }
//         else
//         {
//             newCoroutine = StartCoroutine(MoveDirectlyCoroutine(vehicle, movementData, () =>
//             {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//
//                 onComplete?.Invoke();
//             }));
//         }
//
//         activeMoveCoroutines[vehicle] = newCoroutine;
//     }
//
//     private System.Collections.IEnumerator MoveDirectlyCoroutine(VehicleController vehicle,
//         VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 startPos = vehicleTransform.position;
//
//         // First, rotate to face the destination
//         while (Quaternion.Angle(vehicleTransform.rotation, movementData.finalRotation) > 0.1f)
//         {
//             vehicleTransform.rotation = Quaternion.RotateTowards(vehicleTransform.rotation, movementData.finalRotation,
//                 rotationSpeed * Time.deltaTime);
//             yield return null;
//         }
//
//         // Then move directly to the destination with terrain following
//         float journeyLength = Vector3.Distance(startPos, movementData.finalDestination);
//         float journeyTime = journeyLength / movementSpeed;
//         float elapsedTime = 0;
//
//         while (elapsedTime < journeyTime)
//         {
//             float t = elapsedTime / journeyTime;
//             Vector3 currentPos = Vector3.Lerp(startPos, movementData.finalDestination, t);
//
//             // Adjust height based on terrain at current position
//             currentPos.y = GetTerrainHeightAtPosition(currentPos) + heightOffset;
//
//             vehicleTransform.position = currentPos;
//             elapsedTime += Time.deltaTime;
//             yield return null;
//         }
//
//         // Ensure we end up exactly at the destination and set final rotation
//         vehicleTransform.position = movementData.finalDestination;
//         vehicleTransform.rotation = movementData.finalRotation;
//
//         onComplete?.Invoke();
//     }
//
//     private System.Collections.IEnumerator MoveSmoothContinuousCoroutine(VehicleController vehicle,
//         VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 currentPosition = vehicleTransform.position;
//         Vector3 startPosition = currentPosition; // Keep for reference if needed, but forwardMovementDistance is gone
//         Vector3 currentVelocity = vehicleTransform.forward * movementSpeed;
//
//         float lastHeadingUpdate = 0f;
//         // Removed: float totalDistanceTraveled = 0f;
//         // Removed: bool canTurn = false; // No longer needed as turning starts immediately
//
//         // Track the previous position to calculate distance traveled
//         Vector3 previousPosition = currentPosition;
//
//         while (Vector3.Distance(currentPosition, movementData.finalDestination) > 0.1f)
//         {
//             float deltaTime = Time.deltaTime;
//             float distanceToTarget = Vector3.Distance(currentPosition, movementData.finalDestination);
//
//             // Removed: Calculate how far we've moved from the start
//             // Removed: float distanceFromStart = Vector3.Distance(startPosition, currentPosition);
//
//             // Removed: Check if we've moved forward enough to start turning
//             // Removed: if (!canTurn && distanceFromStart >= forwardMovementDistance)
//             // Removed: {
//             // Removed:     canTurn = true;
//             // Removed:     Debug.Log($"Vehicle can now turn after moving {distanceFromStart:F2} units forward");
//             // Removed: }
//
//             // Switch to direct movement when very close to destination to avoid orbiting
//             if (distanceToTarget <= directMovementThreshold)
//             {
//                 // Direct movement to destination
//                 Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
//                 currentVelocity = directionToTarget * movementSpeed;
//
//                 // Move directly towards destination
//                 Vector3 movement = currentVelocity * deltaTime;
//
//                 // Don't overshoot the destination
//                 if (movement.magnitude >= distanceToTarget)
//                 {
//                     currentPosition = movementData.finalDestination;
//                 }
//                 else
//                 {
//                     currentPosition += movement;
//                 }
//             }
//             else
//             {
//                 // Normal smooth turning behavior
//
//                 // Update heading periodically
//                 if (Time.time - lastHeadingUpdate >= headingUpdateInterval)
//                 {
//                     Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
//                     directionToTarget.y = 0; // Keep on horizontal plane
//
//                     if (directionToTarget.sqrMagnitude > 0.001f)
//                     {
//                         // Calculate the desired velocity direction
//                         Vector3 desiredVelocity = directionToTarget * movementSpeed;
//
//                         // Smoothly adjust current velocity towards desired velocity
//                         // This creates the curved motion effect.
//                         // Now, turningRadius directly influences the maxTurnRate.
//                         // A larger turningRadius means a smaller maxTurnRate, leading to wider turns.
//                         float maxTurnRate = (headingAdjustmentSpeed / turningRadius) * Mathf.Deg2Rad;
//
//                         Vector3 velocityChange = Vector3.RotateTowards(
//                             currentVelocity.normalized,
//                             desiredVelocity.normalized,
//                             maxTurnRate * deltaTime,
//                             0f
//                         );
//
//                         currentVelocity = velocityChange * movementSpeed;
//                     }
//
//                     lastHeadingUpdate = Time.time;
//                 }
//
//                 // Removed: If we can't turn yet, maintain forward movement in current direction
//                 // Removed: if (!canTurn)
//                 // Removed: {
//                 // Removed:     currentVelocity = vehicleTransform.forward * movementSpeed;
//                 // Removed: }
//
//                 // Move the vehicle
//                 Vector3 movement = currentVelocity * deltaTime;
//                 currentPosition += movement;
//
//                 // Slow down as we approach the destination to avoid overshooting
//                 if (distanceToTarget < turningRadius) // This still uses turningRadius for deceleration
//                 {
//                     float speedMultiplier = Mathf.Clamp01(distanceToTarget / turningRadius);
//                     currentVelocity = currentVelocity.normalized * (movementSpeed * speedMultiplier);
//                 }
//             }
//
//             // Removed: Calculate distance traveled this frame
//             // Removed: float frameDistance = Vector3.Distance(previousPosition, currentPosition);
//             // Removed: totalDistanceTraveled += frameDistance;
//             previousPosition = currentPosition; // Still useful for general tracking if needed
//
//             // Adjust height based on terrain
//             currentPosition.y = GetTerrainHeightAtPosition(currentPosition) + heightOffset;
//
//             // Update vehicle position
//             vehicleTransform.position = currentPosition;
//
//             // Smoothly rotate to face the movement direction
//             // IMPORTANT: Only rotate if we are not yet at the final destination (or very close)
//             // This prevents any final "snapping" rotations as the vehicle reaches its target.
//             if (currentVelocity.sqrMagnitude > 0.001f && distanceToTarget > 0.1f)
//             {
//                 Vector3 lookDirection = currentVelocity.normalized;
//                 lookDirection.y = 0; // Keep rotation on horizontal plane
//
//                 if (lookDirection.sqrMagnitude > 0.001f)
//                 {
//                     Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
//                     vehicleTransform.rotation = Quaternion.RotateTowards(
//                         vehicleTransform.rotation,
//                         targetRotation,
//                         rotationSpeed * deltaTime
//                     );
//                 }
//             }
//
//             yield return null;
//         }
//
//         // Final positioning: Ensure the vehicle is exactly at the destination.
//         // The rotation should be whatever it was when the loop condition became false.
//         Vector3 finalDestination = movementData.finalDestination;
//         finalDestination.y = GetTerrainHeightAtPosition(finalDestination) + heightOffset;
//         vehicleTransform.position = finalDestination;
//
//         onComplete?.Invoke();
//     }
//
//     /// <summary>
//     /// Check if a valid path exists between two grid positions (using A* pathfinding)
//     /// </summary>
//     private bool FindPathExists(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
//     {
//         // Clear previous search data
//         nodeCache.Clear();
//
//         // Initialize nodes
//         HexNode startNode = GetNode(startGrid);
//         startNode.cameFromDirection = Vector2Int.zero;
//
//         HexNode targetNode = GetNode(targetGrid);
//
//         if (startNode == null || targetNode == null || !targetNode.isWalkable)
//         {
//             return false;
//         }
//
//         // A* algorithm (simplified - just checking if path exists)
//         List<HexNode> openSet = new List<HexNode>();
//         HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
//
//         openSet.Add(startNode);
//         startNode.gCost = 0;
//         startNode.hCost = GetDistance(startGrid, targetGrid);
//
//         while (openSet.Count > 0)
//         {
//             HexNode currentNode = openSet[0];
//             for (int i = 1; i < openSet.Count; i++)
//             {
//                 if (openSet[i].fCost < currentNode.fCost ||
//                     (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
//                 {
//                     currentNode = openSet[i];
//                 }
//             }
//
//             openSet.Remove(currentNode);
//             closedSet.Add(currentNode.gridPos);
//
//             if (currentNode.gridPos == targetGrid)
//             {
//                 return true; // Path found
//             }
//
//             Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
//
//             foreach (Vector2Int neighborPos in neighbors)
//             {
//                 if (closedSet.Contains(neighborPos)) continue;
//
//                 HexNode neighborNode = GetNode(neighborPos);
//                 if (neighborNode == null) continue;
//                 if (!neighborNode.isWalkable) continue;
//
//                 // Check if neighbor is within max movement range
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 if (Vector3.Distance(startWorldPosForRangeCheck, neighborWorldPos) > maxMovementRange)
//                 {
//                     continue;
//                 }
//
//                 neighborNode.cameFromDirection = neighborPos - currentNode.gridPos;
//                 if (!CanMoveBetween(currentNode, neighborNode)) continue;
//
//                 float moveCost = GetMovementCost(currentNode, neighborNode);
//                 float newGCost = currentNode.gCost + moveCost;
//
//                 if (newGCost < neighborNode.gCost || !openSet.Contains(neighborNode))
//                 {
//                     neighborNode.gCost = newGCost;
//                     neighborNode.hCost = GetDistance(neighborPos, targetGrid);
//                     neighborNode.parent = currentNode;
//
//                     if (!openSet.Contains(neighborNode))
//                     {
//                         openSet.Add(neighborNode);
//                     }
//                 }
//             }
//         }
//
//         return false; // No path found
//     }
//
//     private HexNode GetNode(Vector2Int gridPos)
//     {
//         if (nodeCache.ContainsKey(gridPos))
//         {
//             return nodeCache[gridPos];
//         }
//
//         if (hexController == null || gridPos.x < 0 || gridPos.x >= hexController.gridWidth ||
//             gridPos.y < 0 || gridPos.y >= hexController.gridHeight)
//         {
//             return null;
//         }
//
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         HexNode node = new HexNode(gridPos, hexData.isWalkable, hexData.height, hexData.isRock, Vector2Int.zero);
//         nodeCache[gridPos] = node;
//         return node;
//     }
//
//     private Vector2Int[] GetHexNeighbors(int x, int z)
//     {
//         if (x % 2 == 0)
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1), // Top
//                 new Vector2Int(x + 1, z - 1), // Top Right
//                 new Vector2Int(x + 1, z), // Bottom Right
//                 new Vector2Int(x, z + 1), // Bottom
//                 new Vector2Int(x - 1, z), // Bottom Left
//                 new Vector2Int(x - 1, z - 1) // Top Left
//             };
//         }
//         else
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1), // Top
//                 new Vector2Int(x + 1, z), // Top Right
//                 new Vector2Int(x + 1, z + 1), // Bottom Right
//                 new Vector2Int(x, z + 1), // Bottom
//                 new Vector2Int(x - 1, z + 1), // Bottom Left
//                 new Vector2Int(x - 1, z) // Top Left
//             };
//         }
//     }
//
//     private bool CanMoveBetween(HexNode from, HexNode to)
//     {
//         float heightDifference = to.height - from.height;
//         return heightDifference <= maxClimbHeight;
//     }
//
//     private float GetMovementCost(HexNode from, HexNode to)
//     {
//         float baseCost = 1f;
//         float heightDifference = to.height - from.height;
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
//         if (to.isRock)
//         {
//             baseCost *= rockTerrainCostMultiplier;
//         }
//
//         Vector2Int currentMoveDirection = to.gridPos - from.gridPos;
//         if (from.parent != null && from.cameFromDirection != Vector2Int.zero &&
//             from.cameFromDirection != currentMoveDirection)
//         {
//             baseCost += directionChangePenalty;
//         }
//
//         return baseCost;
//     }
//
//     private float GetDistance(Vector2Int a, Vector2Int b)
//     {
//         int dx = a.x - b.x;
//         int dy = a.y - b.y;
//
//         if (Mathf.Sign(dx) == Mathf.Sign(dy))
//         {
//             return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
//         }
//         else
//         {
//             return Mathf.Abs(dx) + Mathf.Abs(dy);
//         }
//     }
//
//     /// <summary>
//     /// Get the nearest walkable hex to a world position
//     /// </summary>
//     public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
//     {
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//
//         HexNode node = GetNode(gridPos);
//         if (node != null && node.isWalkable)
//         {
//             return gridPos;
//         }
//
//         for (int radius = 1; radius <= 10; radius++)
//         {
//             for (int x = -radius; x <= radius; x++)
//             {
//                 for (int y = -radius; y <= radius; y++)
//                 {
//                     Vector2Int checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
//                     HexNode checkNode = GetNode(checkPos);
//
//                     if (checkNode != null && checkNode.isWalkable)
//                     {
//                         return checkPos;
//                     }
//                 }
//             }
//         }
//
//         return gridPos;
//     }
//
//     /// <summary>
//     /// Check if a valid path exists between two positions
//     /// </summary>
//     public bool PathExists(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         return movementData.pathExists;
//     }
//
//     /// <summary>
//     /// Get the straight-line distance to the destination (since we move directly)
//     /// </summary>
//     public float GetMovementDistance(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         if (!movementData.pathExists) return float.MaxValue;
//
//         return Vector3.Distance(startPos, movementData.finalDestination);
//     }
//
//     /// <summary>
//     /// Get the terrain height at a specific world position by sampling the hexagon grid
//     /// </summary>
//     private float GetTerrainHeightAtPosition(Vector3 worldPos)
//     {
//         if (hexController == null) return worldPos.y;
//
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//
//         // Get the hex data at this position
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         if (hexController.IsValidGridPosition(gridPos.x, gridPos.y))
//         {
//             // Return the hex height plus half the mesh height (to get the surface level)
//             return hexData.height + hexController.hexMeshHeight * 0.5f;
//         }
//
//         // If we can't get hex data, try to interpolate from nearby hexes
//         return InterpolateTerrainHeight(worldPos);
//     }
//
//     /// <summary>
//     /// Interpolate terrain height from nearby hexagons for smoother movement
//     /// </summary>
//     private float InterpolateTerrainHeight(Vector3 worldPos)
//     {
//         Vector2Int centerGrid = hexController.WorldToGridPosition(worldPos);
//         Vector2Int[] neighbors = GetHexNeighbors(centerGrid.x, centerGrid.y);
//
//         float totalHeight = 0f;
//         float totalWeight = 0f;
//
//         // Include the center hex
//         Vector3 centerWorldPos = hexController.GetHexWorldPosition(centerGrid.x, centerGrid.y);
//         float centerDistance = Vector3.Distance(worldPos, centerWorldPos);
//         if (centerDistance < 0.1f) centerDistance = 0.1f; // Prevent division by zero
//
//         var centerHexData = hexController.GetHexData(centerGrid.x, centerGrid.y);
//         if (hexController.IsValidGridPosition(centerGrid.x, centerGrid.y))
//         {
//             float centerWeight = 1f / centerDistance;
//             totalHeight += (centerHexData.height + hexController.hexMeshHeight * 0.5f) * centerWeight;
//             totalWeight += centerWeight;
//         }
//
//         // Sample neighboring hexes
//         foreach (Vector2Int neighborPos in neighbors)
//         {
//             var neighborHexData = hexController.GetHexData(neighborPos.x, neighborPos.y);
//             if (hexController.IsValidGridPosition(neighborPos.x, neighborPos.y))
//             {
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 float distance = Vector3.Distance(worldPos, neighborWorldPos);
//                 if (distance < 0.1f) distance = 0.1f; // Prevent division by zero
//
//                 float weight = 1f / distance;
//                 totalHeight += (neighborHexData.height + hexController.hexMeshHeight * 0.5f) * weight;
//                 totalWeight += weight;
//             }
//         }
//
//         if (totalWeight > 0f)
//         {
//             return totalHeight / totalWeight;
//         }
//
//         // Fallback to current Y position if no valid hex data found
//         return worldPos.y;
//     }
// }



// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;
//
// public class VehiclePathFinderHybrid: MonoBehaviour
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
//     [Tooltip("Additional cost applied when the unit changes its movement direction.")]
//     public float directionChangePenalty = 0.5f; 
//
//     [Tooltip("Maximum world distance (radius) a unit can move from its starting point.")]
//     public float maxMovementRange = 10f;
//     
//     [Header("Movement Settings")]
//     [Tooltip("Speed of movement to the destination")]
//     public float movementSpeed = 5f;
//     
//     [Tooltip("How smoothly the object rotates to face movement direction")]
//     public float rotationSpeed = 180f;
//     
//     [Tooltip("Height offset above terrain for the moving object")]
//     public float heightOffset = 0.5f;
//     
//     [Header("Smooth Turning Settings")]
//     [Tooltip("Distance the vehicle must move forward before it can start turning")]
//     public float forwardMovementDistance = 2f;
//     
//     [Tooltip("How tightly the vehicle can turn (lower = tighter turns, higher = wider turns)")]
//     public float turningRadius = 2f;
//     
//     [Tooltip("How quickly the vehicle adjusts its heading towards the target")]
//     public float headingAdjustmentSpeed = 90f;
//     
//     [Tooltip("Minimum distance to destination before using smooth turning")]
//     public float smoothTurningMinDistance = 3f;
//     
//     [Tooltip("How often to update the vehicle's heading during smooth movement (in seconds)")]
//     public float headingUpdateInterval = 0.1f;
//     
//     [Tooltip("Distance threshold for switching to direct movement to avoid orbiting")]
//     public float directMovementThreshold = 1f;
//     
//     private HexagonController hexController;
//     private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();
//
//     // Stores the currently running movement coroutine for a specific GameObject
//     private Dictionary<VehicleController, Coroutine> activeMoveCoroutines = new Dictionary<VehicleController, Coroutine>();
//     
//     // A* pathfinding data structures
//     private class HexNode
//     {
//         public Vector2Int gridPos;
//         public float gCost; // Distance from start
//         public float hCost; // Distance to target (heuristic)
//         public float fCost => gCost + hCost; // Total cost
//         public HexNode parent;
//         public bool isWalkable;
//         public float height;
//         public bool isRock;
//         public Vector2Int cameFromDirection;
//
//         public HexNode(Vector2Int pos, bool walkable, float h, bool rock, Vector2Int cameFromDir)
//         {
//             gridPos = pos;
//             isWalkable = walkable;
//             height = h;
//             isRock = rock;
//             cameFromDirection = cameFromDir;
//         }
//     }
//
//     /// <summary>
//     /// Simple movement data containing only the final destination
//     /// </summary>
//     public struct VehicleMovementData
//     {
//         public Vector3 finalDestination;
//         public Quaternion finalRotation;
//         public bool pathExists;
//         public bool useSmoothTurning;
//
//         public VehicleMovementData(Vector3 destination, Quaternion rotation, bool hasPath, bool smoothTurning = false)
//         {
//             finalDestination = destination;
//             finalRotation = rotation;
//             pathExists = hasPath;
//             useSmoothTurning = smoothTurning;
//         }
//     }
//     
//     void Start()
//     {
//         hexController = FindObjectOfType<HexagonController>(); 
//         if (hexController == null)
//         {
//             Debug.LogError("VehiclePathFinderSimple requires a HexagonController component in the scene!");
//         }
//     }
//     
//     /// <summary>
//     /// Calculate movement data for direct movement between two world positions
//     /// </summary>
//     public VehicleMovementData CalculateMovement(Vector3 startWorldPos, Vector3 targetWorldPos)
//     {
//         Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
//         Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);
//
//         // Early exit if target is out of max movement range
//         if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
//         {
//             Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
//             return new VehicleMovementData(startWorldPos, Quaternion.identity, false);
//         }
//         
//         return CalculateMovement(startGrid, targetGrid, startWorldPos);
//     }
//     
//     /// <summary>
//     /// Calculate movement data for direct movement between two grid positions
//     /// </summary>
//     private VehicleMovementData CalculateMovement(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
//     {
//         if (hexController == null)
//         {
//             Debug.LogError("HexagonController not found. Cannot calculate movement.");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//         
//         // Check if a valid path exists using A* pathfinding
//         bool pathExists = FindPathExists(startGrid, targetGrid, startWorldPosForRangeCheck);
//         
//         if (!pathExists)
//         {
//             Debug.LogWarning($"No valid path found from {startGrid} to {targetGrid}");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//         
//         // Calculate final destination world position
//         Vector3 finalDestination = hexController.GetHexWorldPosition(targetGrid.x, targetGrid.y);
//         finalDestination.y += hexController.hexMeshHeight * 0.5f;
//         finalDestination.y += heightOffset;
//         
//         // Calculate rotation to face the destination
//         Vector3 directionToTarget = (finalDestination - startWorldPosForRangeCheck).normalized;
//         directionToTarget.y = 0; // Flatten for rotation
//         
//         Quaternion finalRotation = Quaternion.identity;
//         if (directionToTarget.sqrMagnitude > 0.001f)
//         {
//             finalRotation = Quaternion.LookRotation(directionToTarget);
//         }
//         
//         // Determine if we should use smooth turning based on distance
//         float distanceToTarget = Vector3.Distance(startWorldPosForRangeCheck, finalDestination);
//         bool useSmoothTurning = distanceToTarget >= smoothTurningMinDistance;
//         
//         return new VehicleMovementData(finalDestination, finalRotation, true, useSmoothTurning);
//     }
//     
//     /// <summary>
//     /// Move a vehicle directly to the destination (ignoring the path)
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, Vector3 startPos, Vector3 targetPos, System.Action onComplete = null)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, targetPos);
//         
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - no valid path exists");
//             onComplete?.Invoke();
//             return;
//         }
//         
//         MoveToDestination(vehicle, movementData, onComplete);
//     }
//     
//     /// <summary>
//     /// Move a vehicle directly to the destination using pre-calculated movement data
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete = null)
//     {
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - movement data indicates no valid path");
//             onComplete?.Invoke();
//             return;
//         }
//         
//         // Stop any existing movement coroutine for this vehicle
//         if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
//         {
//             StopCoroutine(activeMoveCoroutines[vehicle]);
//             activeMoveCoroutines.Remove(vehicle);
//         }
//
//         // Choose the appropriate movement coroutine based on whether we're using smooth turning
//         Coroutine newCoroutine;
//         if (movementData.useSmoothTurning)
//         {
//             newCoroutine = StartCoroutine(MoveSmoothContinuousCoroutine(vehicle, movementData, () => {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//                 onComplete?.Invoke();
//             }));
//         }
//         else
//         {
//             newCoroutine = StartCoroutine(MoveDirectlyCoroutine(vehicle, movementData, () => {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//                 onComplete?.Invoke();
//             }));
//         }
//         
//         activeMoveCoroutines[vehicle] = newCoroutine;
//     }
//     
//     private System.Collections.IEnumerator MoveDirectlyCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 startPos = vehicleTransform.position;
//         
//         // First, rotate to face the destination
//         while (Quaternion.Angle(vehicleTransform.rotation, movementData.finalRotation) > 0.1f)
//         {
//             vehicleTransform.rotation = Quaternion.RotateTowards(vehicleTransform.rotation, movementData.finalRotation, rotationSpeed * Time.deltaTime);
//             yield return null;
//         }
//         
//         // Then move directly to the destination with terrain following
//         float journeyLength = Vector3.Distance(startPos, movementData.finalDestination);
//         float journeyTime = journeyLength / movementSpeed;
//         float elapsedTime = 0;
//         
//         while (elapsedTime < journeyTime)
//         {
//             float t = elapsedTime / journeyTime;
//             Vector3 currentPos = Vector3.Lerp(startPos, movementData.finalDestination, t);
//             
//             // Adjust height based on terrain at current position
//             currentPos.y = GetTerrainHeightAtPosition(currentPos) + heightOffset;
//             
//             vehicleTransform.position = currentPos;
//             elapsedTime += Time.deltaTime;
//             yield return null;
//         }
//         
//         // Ensure we end up exactly at the destination and set final rotation
//         vehicleTransform.position = movementData.finalDestination;
//         vehicleTransform.rotation = movementData.finalRotation; // This line is intentionally kept for direct movement
//         
//         onComplete?.Invoke();
//     }
//     
//     private System.Collections.IEnumerator MoveSmoothContinuousCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 currentPosition = vehicleTransform.position;
//         Vector3 startPosition = currentPosition;
//         Vector3 currentVelocity = vehicleTransform.forward * movementSpeed;
//         
//         float lastHeadingUpdate = 0f;
//         float totalDistanceTraveled = 0f;
//         bool canTurn = false;
//         
//         // Track the previous position to calculate distance traveled
//         Vector3 previousPosition = currentPosition;
//         
//         while (Vector3.Distance(currentPosition, movementData.finalDestination) > 0.1f)
//         {
//             float deltaTime = Time.deltaTime;
//             float distanceToTarget = Vector3.Distance(currentPosition, movementData.finalDestination);
//             
//             // Calculate how far we've moved from the start
//             float distanceFromStart = Vector3.Distance(startPosition, currentPosition);
//             
//             // Check if we've moved forward enough to start turning
//             if (!canTurn && distanceFromStart >= forwardMovementDistance)
//             {
//                 canTurn = true;
//                 Debug.Log($"Vehicle can now turn after moving {distanceFromStart:F2} units forward");
//             }
//             
//             // Switch to direct movement when very close to destination to avoid orbiting
//             if (distanceToTarget <= directMovementThreshold)
//             {
//                 // Direct movement to destination
//                 Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
//                 currentVelocity = directionToTarget * movementSpeed;
//                 
//                 // Move directly towards destination
//                 Vector3 movement = currentVelocity * deltaTime;
//                 
//                 // Don't overshoot the destination
//                 if (movement.magnitude >= distanceToTarget)
//                 {
//                     currentPosition = movementData.finalDestination;
//                 }
//                 else
//                 {
//                     currentPosition += movement;
//                 }
//             }
//             else
//             {
//                 // Normal smooth turning behavior
//                 
//                 // Update heading periodically, but only if we're allowed to turn
//                 if (canTurn && Time.time - lastHeadingUpdate >= headingUpdateInterval)
//                 {
//                     Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
//                     directionToTarget.y = 0; // Keep on horizontal plane
//                     
//                     if (directionToTarget.sqrMagnitude > 0.001f)
//                     {
//                         // Calculate the desired velocity direction
//                         Vector3 desiredVelocity = directionToTarget * movementSpeed;
//                         
//                         // Smoothly adjust current velocity towards desired velocity
//                         // This creates the curved motion effect
//                         float maxTurnRate = headingAdjustmentSpeed * Mathf.Deg2Rad;
//                         Vector3 velocityChange = Vector3.RotateTowards(
//                             currentVelocity.normalized, 
//                             desiredVelocity.normalized, 
//                             maxTurnRate * deltaTime, 
//                             0f
//                         );
//                         
//                         currentVelocity = velocityChange * movementSpeed;
//                     }
//                     
//                     lastHeadingUpdate = Time.time;
//                 }
//                 
//                 // If we can't turn yet, maintain forward movement in current direction
//                 if (!canTurn)
//                 {
//                     currentVelocity = vehicleTransform.forward * movementSpeed;
//                 }
//                 
//                 // Move the vehicle
//                 Vector3 movement = currentVelocity * deltaTime;
//                 currentPosition += movement;
//                 
//                 // Slow down as we approach the destination to avoid overshooting
//                 if (distanceToTarget < turningRadius)
//                 {
//                     float speedMultiplier = Mathf.Clamp01(distanceToTarget / turningRadius);
//                     currentVelocity = currentVelocity.normalized * (movementSpeed * speedMultiplier);
//                 }
//             }
//             
//             // Calculate distance traveled this frame
//             float frameDistance = Vector3.Distance(previousPosition, currentPosition);
//             totalDistanceTraveled += frameDistance;
//             previousPosition = currentPosition;
//             
//             // Adjust height based on terrain
//             currentPosition.y = GetTerrainHeightAtPosition(currentPosition) + heightOffset;
//             
//             // Update vehicle position
//             vehicleTransform.position = currentPosition;
//             
//             // Smoothly rotate to face the movement direction
//             // IMPORTANT: Only rotate if we are not yet at the final destination (or very close)
//             // This prevents any final "snapping" rotations as the vehicle reaches its target.
//             if (currentVelocity.sqrMagnitude > 0.001f && distanceToTarget > 0.1f) 
//             {
//                 Vector3 lookDirection = currentVelocity.normalized;
//                 lookDirection.y = 0; // Keep rotation on horizontal plane
//                 
//                 if (lookDirection.sqrMagnitude > 0.001f)
//                 {
//                     Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
//                     vehicleTransform.rotation = Quaternion.RotateTowards(
//                         vehicleTransform.rotation, 
//                         targetRotation, 
//                         rotationSpeed * deltaTime
//                     );
//                 }
//             }
//             
//             yield return null;
//         }
//         
//         // Final positioning: Ensure the vehicle is exactly at the destination.
//         // The rotation should be whatever it was when the loop condition became false.
//         Vector3 finalDestination = movementData.finalDestination;
//         finalDestination.y = GetTerrainHeightAtPosition(finalDestination) + heightOffset;
//         vehicleTransform.position = finalDestination;
//         
//         onComplete?.Invoke();
//     }
//     
//     /// <summary>
//     /// Check if a valid path exists between two grid positions (using A* pathfinding)
//     /// </summary>
//     private bool FindPathExists(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
//     {
//         // Clear previous search data
//         nodeCache.Clear();
//         
//         // Initialize nodes
//         HexNode startNode = GetNode(startGrid);
//         startNode.cameFromDirection = Vector2Int.zero;
//         
//         HexNode targetNode = GetNode(targetGrid);
//         
//         if (startNode == null || targetNode == null || !targetNode.isWalkable)
//         {
//             return false;
//         }
//         
//         // A* algorithm (simplified - just checking if path exists)
//         List<HexNode> openSet = new List<HexNode>();
//         HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
//         
//         openSet.Add(startNode);
//         startNode.gCost = 0;
//         startNode.hCost = GetDistance(startGrid, targetGrid);
//         
//         while (openSet.Count > 0)
//         {
//             HexNode currentNode = openSet[0];
//             for (int i = 1; i < openSet.Count; i++)
//             {
//                 if (openSet[i].fCost < currentNode.fCost || 
//                     (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
//                 {
//                     currentNode = openSet[i];
//                 }
//             }
//             
//             openSet.Remove(currentNode);
//             closedSet.Add(currentNode.gridPos);
//             
//             if (currentNode.gridPos == targetGrid)
//             {
//                 return true; // Path found
//             }
//             
//             Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
//             
//             foreach (Vector2Int neighborPos in neighbors)
//             {
//                 if (closedSet.Contains(neighborPos)) continue;
//                 
//                 HexNode neighborNode = GetNode(neighborPos);
//                 if (neighborNode == null) continue;
//                 if (!neighborNode.isWalkable) continue;
//
//                 // Check if neighbor is within max movement range
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 if (Vector3.Distance(startWorldPosForRangeCheck, neighborWorldPos) > maxMovementRange)
//                 {
//                     continue;
//                 }
//                 
//                 neighborNode.cameFromDirection = neighborPos - currentNode.gridPos;
//                 if (!CanMoveBetween(currentNode, neighborNode)) continue;
//                 
//                 float moveCost = GetMovementCost(currentNode, neighborNode);
//                 float newGCost = currentNode.gCost + moveCost;
//                 
//                 if (newGCost < neighborNode.gCost || !openSet.Contains(neighborNode))
//                 {
//                     neighborNode.gCost = newGCost;
//                     neighborNode.hCost = GetDistance(neighborPos, targetGrid);
//                     neighborNode.parent = currentNode;
//                     
//                     if (!openSet.Contains(neighborNode))
//                     {
//                         openSet.Add(neighborNode);
//                     }
//                 }
//             }
//         }
//         
//         return false; // No path found
//     }
//     
//     private HexNode GetNode(Vector2Int gridPos)
//     {
//         if (nodeCache.ContainsKey(gridPos))
//         {
//             return nodeCache[gridPos];
//         }
//         
//         if (hexController == null || gridPos.x < 0 || gridPos.x >= hexController.gridWidth || 
//             gridPos.y < 0 || gridPos.y >= hexController.gridHeight)
//         {
//             return null;
//         }
//         
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         HexNode node = new HexNode(gridPos, hexData.isWalkable, hexData.height, hexData.isRock, Vector2Int.zero);
//         nodeCache[gridPos] = node;
//         return node;
//     }
//     
//     private Vector2Int[] GetHexNeighbors(int x, int z)
//     {
//         if (x % 2 == 0)
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z - 1), // Top Right
//                 new Vector2Int(x + 1, z),     // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z),     // Bottom Left
//                 new Vector2Int(x - 1, z - 1)  // Top Left
//             };
//         }
//         else
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z),     // Top Right
//                 new Vector2Int(x + 1, z + 1), // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z + 1), // Bottom Left
//                 new Vector2Int(x - 1, z)      // Top Left
//             };
//         }
//     }
//     
//     private bool CanMoveBetween(HexNode from, HexNode to)
//     {
//         float heightDifference = to.height - from.height;
//         return heightDifference <= maxClimbHeight;
//     }
//     
//     private float GetMovementCost(HexNode from, HexNode to)
//     {
//         float baseCost = 1f;
//         float heightDifference = to.height - from.height;
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
//         if (to.isRock)
//         {
//             baseCost *= rockTerrainCostMultiplier;
//         }
//
//         Vector2Int currentMoveDirection = to.gridPos - from.gridPos;
//         if (from.parent != null && from.cameFromDirection != Vector2Int.zero && from.cameFromDirection != currentMoveDirection)
//         {
//             baseCost += directionChangePenalty;
//         }
//         
//         return baseCost;
//     }
//     
//     private float GetDistance(Vector2Int a, Vector2Int b)
//     {
//         int dx = a.x - b.x;
//         int dy = a.y - b.y;
//         
//         if (Mathf.Sign(dx) == Mathf.Sign(dy))
//         {
//             return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
//         }
//         else
//         {
//             return Mathf.Abs(dx) + Mathf.Abs(dy);
//         }
//     }
//     
//     /// <summary>
//     /// Get the nearest walkable hex to a world position
//     /// </summary>
//     public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
//     {
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         HexNode node = GetNode(gridPos);
//         if (node != null && node.isWalkable)
//         {
//             return gridPos;
//         }
//         
//         for (int radius = 1; radius <= 10; radius++)
//         {
//             for (int x = -radius; x <= radius; x++)
//             {
//                 for (int y = -radius; y <= radius; y++)
//                 {
//                     Vector2Int checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
//                     HexNode checkNode = GetNode(checkPos);
//                     
//                     if (checkNode != null && checkNode.isWalkable)
//                     {
//                         return checkPos;
//                     }
//                 }
//             }
//         }
//         
//         return gridPos;
//     }
//     
//     /// <summary>
//     /// Check if a valid path exists between two positions
//     /// </summary>
//     public bool PathExists(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         return movementData.pathExists;
//     }
//     
//     /// <summary>
//     /// Get the straight-line distance to the destination (since we move directly)
//     /// </summary>
//     public float GetMovementDistance(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         if (!movementData.pathExists) return float.MaxValue;
//         
//         return Vector3.Distance(startPos, movementData.finalDestination);
//     }
//     
//     /// <summary>
//     /// Get the terrain height at a specific world position by sampling the hexagon grid
//     /// </summary>
//     private float GetTerrainHeightAtPosition(Vector3 worldPos)
//     {
//         if (hexController == null) return worldPos.y;
//         
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         // Get the hex data at this position
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         if (hexController.IsValidGridPosition(gridPos.x, gridPos.y))
//         {
//             // Return the hex height plus half the mesh height (to get the surface level)
//             return hexData.height + hexController.hexMeshHeight * 0.5f;
//         }
//         
//         // If we can't get hex data, try to interpolate from nearby hexes
//         return InterpolateTerrainHeight(worldPos);
//     }
//     
//     /// <summary>
//     /// Interpolate terrain height from nearby hexagons for smoother movement
//     /// </summary>
//     private float InterpolateTerrainHeight(Vector3 worldPos)
//     {
//         Vector2Int centerGrid = hexController.WorldToGridPosition(worldPos);
//         Vector2Int[] neighbors = GetHexNeighbors(centerGrid.x, centerGrid.y);
//         
//         float totalHeight = 0f;
//         float totalWeight = 0f;
//         
//         // Include the center hex
//         Vector3 centerWorldPos = hexController.GetHexWorldPosition(centerGrid.x, centerGrid.y);
//         float centerDistance = Vector3.Distance(worldPos, centerWorldPos);
//         if (centerDistance < 0.1f) centerDistance = 0.1f; // Prevent division by zero
//         
//         var centerHexData = hexController.GetHexData(centerGrid.x, centerGrid.y);
//         if (hexController.IsValidGridPosition(centerGrid.x, centerGrid.y))
//         {
//             float centerWeight = 1f / centerDistance;
//             totalHeight += (centerHexData.height + hexController.hexMeshHeight * 0.5f) * centerWeight;
//             totalWeight += centerWeight;
//         }
//         
//         // Sample neighboring hexes
//         foreach (Vector2Int neighborPos in neighbors)
//         {
//             var neighborHexData = hexController.GetHexData(neighborPos.x, neighborPos.y);
//             if (hexController.IsValidGridPosition(neighborPos.x, neighborPos.y))
//             {
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 float distance = Vector3.Distance(worldPos, neighborWorldPos);
//                 if (distance < 0.1f) distance = 0.1f; // Prevent division by zero
//                 
//                 float weight = 1f / distance;
//                 totalHeight += (neighborHexData.height + hexController.hexMeshHeight * 0.5f) * weight;
//                 totalWeight += weight;
//             }
//         }
//         
//         if (totalWeight > 0f)
//         {
//             return totalHeight / totalWeight;
//         }
//         
//         // Fallback to current Y position if no valid hex data found
//         return worldPos.y;
//     }
// }







// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;
//
// public class VehiclePathFinderHybrid: MonoBehaviour
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
//     [Tooltip("Additional cost applied when the unit changes its movement direction.")]
//     public float directionChangePenalty = 0.5f; 
//
//     [Tooltip("Maximum world distance (radius) a unit can move from its starting point.")]
//     public float maxMovementRange = 10f;
//     
//     [Header("Movement Settings")]
//     [Tooltip("Speed of movement to the destination")]
//     public float movementSpeed = 5f;
//     
//     [Tooltip("How smoothly the object rotates to face movement direction")]
//     public float rotationSpeed = 180f;
//     
//     [Tooltip("Height offset above terrain for the moving object")]
//     public float heightOffset = 0.5f;
//     
//     [Header("Smooth Turning Settings")]
//     [Tooltip("Distance the vehicle must move forward before it can rotate")]
//     public float forwardMovementDistance = 2f;
//     
//     [Tooltip("Maximum rotation angle per movement segment (in degrees)")]
//     public float maxRotationPerSegment = 45f;
//     
//     [Tooltip("Minimum distance to destination before using smooth turning (use direct movement for very close targets)")]
//     public float smoothTurningMinDistance = 3f;
//     
//     private HexagonController hexController;
//     private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();
//
//     // Stores the currently running movement coroutine for a specific GameObject
//     private Dictionary<VehicleController, Coroutine> activeMoveCoroutines = new Dictionary<VehicleController, Coroutine>();
//     
//     // A* pathfinding data structures
//     private class HexNode
//     {
//         public Vector2Int gridPos;
//         public float gCost; // Distance from start
//         public float hCost; // Distance to target (heuristic)
//         public float fCost => gCost + hCost; // Total cost
//         public HexNode parent;
//         public bool isWalkable;
//         public float height;
//         public bool isRock;
//         public Vector2Int cameFromDirection;
//
//         public HexNode(Vector2Int pos, bool walkable, float h, bool rock, Vector2Int cameFromDir)
//         {
//             gridPos = pos;
//             isWalkable = walkable;
//             height = h;
//             isRock = rock;
//             cameFromDirection = cameFromDir;
//         }
//     }
//
//     /// <summary>
//     /// Simple movement data containing only the final destination
//     /// </summary>
//     public struct VehicleMovementData
//     {
//         public Vector3 finalDestination;
//         public Quaternion finalRotation;
//         public bool pathExists;
//         public bool useSmoothTurning;
//
//         public VehicleMovementData(Vector3 destination, Quaternion rotation, bool hasPath, bool smoothTurning = false)
//         {
//             finalDestination = destination;
//             finalRotation = rotation;
//             pathExists = hasPath;
//             useSmoothTurning = smoothTurning;
//         }
//     }
//     
//     void Start()
//     {
//         hexController = FindObjectOfType<HexagonController>(); 
//         if (hexController == null)
//         {
//             Debug.LogError("VehiclePathFinderSimple requires a HexagonController component in the scene!");
//         }
//     }
//     
//     /// <summary>
//     /// Calculate movement data for direct movement between two world positions
//     /// </summary>
//     public VehicleMovementData CalculateMovement(Vector3 startWorldPos, Vector3 targetWorldPos)
//     {
//         Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
//         Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);
//
//         // Early exit if target is out of max movement range
//         if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
//         {
//             Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
//             return new VehicleMovementData(startWorldPos, Quaternion.identity, false);
//         }
//         
//         return CalculateMovement(startGrid, targetGrid, startWorldPos);
//     }
//     
//     /// <summary>
//     /// Calculate movement data for direct movement between two grid positions
//     /// </summary>
//     private VehicleMovementData CalculateMovement(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
//     {
//         if (hexController == null)
//         {
//             Debug.LogError("HexagonController not found. Cannot calculate movement.");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//         
//         // Check if a valid path exists using A* pathfinding
//         bool pathExists = FindPathExists(startGrid, targetGrid, startWorldPosForRangeCheck);
//         
//         if (!pathExists)
//         {
//             Debug.LogWarning($"No valid path found from {startGrid} to {targetGrid}");
//             return new VehicleMovementData(startWorldPosForRangeCheck, Quaternion.identity, false);
//         }
//         
//         // Calculate final destination world position
//         Vector3 finalDestination = hexController.GetHexWorldPosition(targetGrid.x, targetGrid.y);
//         finalDestination.y += hexController.hexMeshHeight * 0.5f;
//         finalDestination.y += heightOffset;
//         
//         // Calculate rotation to face the destination
//         Vector3 directionToTarget = (finalDestination - startWorldPosForRangeCheck).normalized;
//         directionToTarget.y = 0; // Flatten for rotation
//         
//         Quaternion finalRotation = Quaternion.identity;
//         if (directionToTarget.sqrMagnitude > 0.001f)
//         {
//             finalRotation = Quaternion.LookRotation(directionToTarget);
//         }
//         
//         // Determine if we should use smooth turning based on distance
//         float distanceToTarget = Vector3.Distance(startWorldPosForRangeCheck, finalDestination);
//         bool useSmoothTurning = distanceToTarget >= smoothTurningMinDistance;
//         
//         return new VehicleMovementData(finalDestination, finalRotation, true, useSmoothTurning);
//     }
//     
//     /// <summary>
//     /// Move a vehicle directly to the destination (ignoring the path)
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, Vector3 startPos, Vector3 targetPos, System.Action onComplete = null)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, targetPos);
//         
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - no valid path exists");
//             onComplete?.Invoke();
//             return;
//         }
//         
//         MoveToDestination(vehicle, movementData, onComplete);
//     }
//     
//     /// <summary>
//     /// Move a vehicle directly to the destination using pre-calculated movement data
//     /// </summary>
//     public void MoveToDestination(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete = null)
//     {
//         if (!movementData.pathExists)
//         {
//             Debug.LogWarning("Cannot move vehicle - movement data indicates no valid path");
//             onComplete?.Invoke();
//             return;
//         }
//         
//         // Stop any existing movement coroutine for this vehicle
//         if (activeMoveCoroutines.ContainsKey(vehicle) && activeMoveCoroutines[vehicle] != null)
//         {
//             StopCoroutine(activeMoveCoroutines[vehicle]);
//             activeMoveCoroutines.Remove(vehicle);
//         }
//
//         // Choose the appropriate movement coroutine based on whether we're using smooth turning
//         Coroutine newCoroutine;
//         if (movementData.useSmoothTurning)
//         {
//             newCoroutine = StartCoroutine(MoveSmoothTurningCoroutine(vehicle, movementData, () => {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//                 onComplete?.Invoke();
//             }));
//         }
//         else
//         {
//             newCoroutine = StartCoroutine(MoveDirectlyCoroutine(vehicle, movementData, () => {
//                 if (activeMoveCoroutines.ContainsKey(vehicle))
//                 {
//                     activeMoveCoroutines.Remove(vehicle);
//                 }
//                 onComplete?.Invoke();
//             }));
//         }
//         
//         activeMoveCoroutines[vehicle] = newCoroutine;
//     }
//     
//     private System.Collections.IEnumerator MoveDirectlyCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 startPos = vehicleTransform.position;
//         
//         // First, rotate to face the destination
//         while (Quaternion.Angle(vehicleTransform.rotation, movementData.finalRotation) > 0.1f)
//         {
//             vehicleTransform.rotation = Quaternion.RotateTowards(vehicleTransform.rotation, movementData.finalRotation, rotationSpeed * Time.deltaTime);
//             yield return null;
//         }
//         
//         // Then move directly to the destination with terrain following
//         float journeyLength = Vector3.Distance(startPos, movementData.finalDestination);
//         float journeyTime = journeyLength / movementSpeed;
//         float elapsedTime = 0;
//         
//         while (elapsedTime < journeyTime)
//         {
//             float t = elapsedTime / journeyTime;
//             Vector3 currentPos = Vector3.Lerp(startPos, movementData.finalDestination, t);
//             
//             // Adjust height based on terrain at current position
//             currentPos.y = GetTerrainHeightAtPosition(currentPos) + heightOffset;
//             
//             vehicleTransform.position = currentPos;
//             elapsedTime += Time.deltaTime;
//             yield return null;
//         }
//         
//         // Ensure we end up exactly at the destination
//         vehicleTransform.position = movementData.finalDestination;
//         vehicleTransform.rotation = movementData.finalRotation;
//         
//         onComplete?.Invoke();
//     }
//     
//     private System.Collections.IEnumerator MoveSmoothTurningCoroutine(VehicleController vehicle, VehicleMovementData movementData, System.Action onComplete)
//     {
//         Transform vehicleTransform = vehicle.transform;
//         Vector3 currentPosition = vehicleTransform.position;
//         Quaternion currentRotation = vehicleTransform.rotation;
//         
//         while (Vector3.Distance(currentPosition, movementData.finalDestination) > 0.1f)
//         {
//             // Phase 1: Move forward in current facing direction
//             Vector3 forwardDirection = currentRotation * Vector3.forward;
//             Vector3 targetPosition = currentPosition + forwardDirection * forwardMovementDistance;
//             
//             // Don't overshoot the final destination
//             float distanceToFinalDestination = Vector3.Distance(currentPosition, movementData.finalDestination);
//             if (distanceToFinalDestination < forwardMovementDistance)
//             {
//                 targetPosition = movementData.finalDestination;
//             }
//             
//             // Animate the forward movement
//             Vector3 segmentStartPos = currentPosition;
//             float segmentDistance = Vector3.Distance(segmentStartPos, targetPosition);
//             float segmentTime = segmentDistance / movementSpeed;
//             float elapsedTime = 0;
//             
//             while (elapsedTime < segmentTime)
//             {
//                 float t = elapsedTime / segmentTime;
//                 
//                 // Move forward in a straight line
//                 Vector3 currentPos = Vector3.Lerp(segmentStartPos, targetPosition, t);
//                 currentPos.y = GetTerrainHeightAtPosition(currentPos) + heightOffset;
//                 
//                 vehicleTransform.position = currentPos;
//                 
//                 elapsedTime += Time.deltaTime;
//                 yield return null;
//             }
//             
//             // Ensure we end up exactly at the target position
//             Vector3 finalSegmentPos = targetPosition;
//             finalSegmentPos.y = GetTerrainHeightAtPosition(finalSegmentPos) + heightOffset;
//             vehicleTransform.position = finalSegmentPos;
//             
//             // Update current position
//             currentPosition = finalSegmentPos;
//             
//             // Break if we've reached the destination
//             if (Vector3.Distance(currentPosition, movementData.finalDestination) <= 0.1f)
//             {
//                 break;
//             }
//             
//             // Phase 2: Rotate towards the target
//             Vector3 directionToTarget = (movementData.finalDestination - currentPosition).normalized;
//             directionToTarget.y = 0; // Keep rotation on horizontal plane
//             
//             if (directionToTarget.sqrMagnitude > 0.001f)
//             {
//                 Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
//                 float angleToTarget = Quaternion.Angle(currentRotation, targetRotation);
//                 
//                 if (angleToTarget > 0.1f)
//                 {
//                     // Calculate how much we can rotate this segment
//                     float rotationThisSegment = Mathf.Min(angleToTarget, maxRotationPerSegment);
//                     float rotationRatio = rotationThisSegment / angleToTarget;
//                     Quaternion newRotation = Quaternion.Slerp(currentRotation, targetRotation, rotationRatio);
//                     
//                     // Animate the rotation
//                     Quaternion segmentStartRot = currentRotation;
//                     float rotationTime = rotationThisSegment / rotationSpeed;
//                     elapsedTime = 0;
//                     
//                     while (elapsedTime < rotationTime)
//                     {
//                         float t = elapsedTime / rotationTime;
//                         vehicleTransform.rotation = Quaternion.Slerp(segmentStartRot, newRotation, t);
//                         
//                         elapsedTime += Time.deltaTime;
//                         yield return null;
//                     }
//                     
//                     // Ensure we end up exactly at the target rotation
//                     vehicleTransform.rotation = newRotation;
//                     currentRotation = newRotation;
//                 }
//             }
//         }
//         
//         // Final adjustment to ensure we're exactly at the destination
//         Vector3 finalDestination = movementData.finalDestination;
//         finalDestination.y = GetTerrainHeightAtPosition(finalDestination) + heightOffset;
//         vehicleTransform.position = finalDestination;
//         vehicleTransform.rotation = movementData.finalRotation;
//         
//         onComplete?.Invoke();
//     }
//     
//     /// <summary>
//     /// Check if a valid path exists between two grid positions (using A* pathfinding)
//     /// </summary>
//     private bool FindPathExists(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
//     {
//         // Clear previous search data
//         nodeCache.Clear();
//         
//         // Initialize nodes
//         HexNode startNode = GetNode(startGrid);
//         startNode.cameFromDirection = Vector2Int.zero;
//         
//         HexNode targetNode = GetNode(targetGrid);
//         
//         if (startNode == null || targetNode == null || !targetNode.isWalkable)
//         {
//             return false;
//         }
//         
//         // A* algorithm (simplified - just checking if path exists)
//         List<HexNode> openSet = new List<HexNode>();
//         HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
//         
//         openSet.Add(startNode);
//         startNode.gCost = 0;
//         startNode.hCost = GetDistance(startGrid, targetGrid);
//         
//         while (openSet.Count > 0)
//         {
//             HexNode currentNode = openSet[0];
//             for (int i = 1; i < openSet.Count; i++)
//             {
//                 if (openSet[i].fCost < currentNode.fCost || 
//                     (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
//                 {
//                     currentNode = openSet[i];
//                 }
//             }
//             
//             openSet.Remove(currentNode);
//             closedSet.Add(currentNode.gridPos);
//             
//             if (currentNode.gridPos == targetGrid)
//             {
//                 return true; // Path found
//             }
//             
//             Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
//             
//             foreach (Vector2Int neighborPos in neighbors)
//             {
//                 if (closedSet.Contains(neighborPos)) continue;
//                 
//                 HexNode neighborNode = GetNode(neighborPos);
//                 if (neighborNode == null) continue;
//                 if (!neighborNode.isWalkable) continue;
//
//                 // Check if neighbor is within max movement range
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 if (Vector3.Distance(startWorldPosForRangeCheck, neighborWorldPos) > maxMovementRange)
//                 {
//                     continue;
//                 }
//                 
//                 neighborNode.cameFromDirection = neighborPos - currentNode.gridPos;
//                 if (!CanMoveBetween(currentNode, neighborNode)) continue;
//                 
//                 float moveCost = GetMovementCost(currentNode, neighborNode);
//                 float newGCost = currentNode.gCost + moveCost;
//                 
//                 if (newGCost < neighborNode.gCost || !openSet.Contains(neighborNode))
//                 {
//                     neighborNode.gCost = newGCost;
//                     neighborNode.hCost = GetDistance(neighborPos, targetGrid);
//                     neighborNode.parent = currentNode;
//                     
//                     if (!openSet.Contains(neighborNode))
//                     {
//                         openSet.Add(neighborNode);
//                     }
//                 }
//             }
//         }
//         
//         return false; // No path found
//     }
//     
//     private HexNode GetNode(Vector2Int gridPos)
//     {
//         if (nodeCache.ContainsKey(gridPos))
//         {
//             return nodeCache[gridPos];
//         }
//         
//         if (hexController == null || gridPos.x < 0 || gridPos.x >= hexController.gridWidth || 
//             gridPos.y < 0 || gridPos.y >= hexController.gridHeight)
//         {
//             return null;
//         }
//         
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         HexNode node = new HexNode(gridPos, hexData.isWalkable, hexData.height, hexData.isRock, Vector2Int.zero);
//         nodeCache[gridPos] = node;
//         return node;
//     }
//     
//     private Vector2Int[] GetHexNeighbors(int x, int z)
//     {
//         if (x % 2 == 0)
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z - 1), // Top Right
//                 new Vector2Int(x + 1, z),     // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z),     // Bottom Left
//                 new Vector2Int(x - 1, z - 1)  // Top Left
//             };
//         }
//         else
//         {
//             return new Vector2Int[]
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z),     // Top Right
//                 new Vector2Int(x + 1, z + 1), // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z + 1), // Bottom Left
//                 new Vector2Int(x - 1, z)      // Top Left
//             };
//         }
//     }
//     
//     private bool CanMoveBetween(HexNode from, HexNode to)
//     {
//         float heightDifference = to.height - from.height;
//         return heightDifference <= maxClimbHeight;
//     }
//     
//     private float GetMovementCost(HexNode from, HexNode to)
//     {
//         float baseCost = 1f;
//         float heightDifference = to.height - from.height;
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
//         if (to.isRock)
//         {
//             baseCost *= rockTerrainCostMultiplier;
//         }
//
//         Vector2Int currentMoveDirection = to.gridPos - from.gridPos;
//         if (from.parent != null && from.cameFromDirection != Vector2Int.zero && from.cameFromDirection != currentMoveDirection)
//         {
//             baseCost += directionChangePenalty;
//         }
//         
//         return baseCost;
//     }
//     
//     private float GetDistance(Vector2Int a, Vector2Int b)
//     {
//         int dx = a.x - b.x;
//         int dy = a.y - b.y;
//         
//         if (Mathf.Sign(dx) == Mathf.Sign(dy))
//         {
//             return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
//         }
//         else
//         {
//             return Mathf.Abs(dx) + Mathf.Abs(dy);
//         }
//     }
//     
//     /// <summary>
//     /// Get the nearest walkable hex to a world position
//     /// </summary>
//     public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
//     {
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         HexNode node = GetNode(gridPos);
//         if (node != null && node.isWalkable)
//         {
//             return gridPos;
//         }
//         
//         for (int radius = 1; radius <= 10; radius++)
//         {
//             for (int x = -radius; x <= radius; x++)
//             {
//                 for (int y = -radius; y <= radius; y++)
//                 {
//                     Vector2Int checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
//                     HexNode checkNode = GetNode(checkPos);
//                     
//                     if (checkNode != null && checkNode.isWalkable)
//                     {
//                         return checkPos;
//                     }
//                 }
//             }
//         }
//         
//         return gridPos;
//     }
//     
//     /// <summary>
//     /// Check if a valid path exists between two positions
//     /// </summary>
//     public bool PathExists(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         return movementData.pathExists;
//     }
//     
//     /// <summary>
//     /// Get the straight-line distance to the destination (since we move directly)
//     /// </summary>
//     public float GetMovementDistance(Vector3 startPos, Vector3 endPos)
//     {
//         VehicleMovementData movementData = CalculateMovement(startPos, endPos);
//         if (!movementData.pathExists) return float.MaxValue;
//         
//         return Vector3.Distance(startPos, movementData.finalDestination);
//     }
//     
//     /// <summary>
//     /// Get the terrain height at a specific world position by sampling the hexagon grid
//     /// </summary>
//     private float GetTerrainHeightAtPosition(Vector3 worldPos)
//     {
//         if (hexController == null) return worldPos.y;
//         
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         // Get the hex data at this position
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         if (hexController.IsValidGridPosition(gridPos.x, gridPos.y))
//         {
//             // Return the hex height plus half the mesh height (to get the surface level)
//             return hexData.height + hexController.hexMeshHeight * 0.5f;
//         }
//         
//         // If we can't get hex data, try to interpolate from nearby hexes
//         return InterpolateTerrainHeight(worldPos);
//     }
//     
//     /// <summary>
//     /// Interpolate terrain height from nearby hexagons for smoother movement
//     /// </summary>
//     private float InterpolateTerrainHeight(Vector3 worldPos)
//     {
//         Vector2Int centerGrid = hexController.WorldToGridPosition(worldPos);
//         Vector2Int[] neighbors = GetHexNeighbors(centerGrid.x, centerGrid.y);
//         
//         float totalHeight = 0f;
//         float totalWeight = 0f;
//         
//         // Include the center hex
//         Vector3 centerWorldPos = hexController.GetHexWorldPosition(centerGrid.x, centerGrid.y);
//         float centerDistance = Vector3.Distance(worldPos, centerWorldPos);
//         if (centerDistance < 0.1f) centerDistance = 0.1f; // Prevent division by zero
//         
//         var centerHexData = hexController.GetHexData(centerGrid.x, centerGrid.y);
//         if (hexController.IsValidGridPosition(centerGrid.x, centerGrid.y))
//         {
//             float centerWeight = 1f / centerDistance;
//             totalHeight += (centerHexData.height + hexController.hexMeshHeight * 0.5f) * centerWeight;
//             totalWeight += centerWeight;
//         }
//         
//         // Sample neighboring hexes
//         foreach (Vector2Int neighborPos in neighbors)
//         {
//             var neighborHexData = hexController.GetHexData(neighborPos.x, neighborPos.y);
//             if (hexController.IsValidGridPosition(neighborPos.x, neighborPos.y))
//             {
//                 Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
//                 float distance = Vector3.Distance(worldPos, neighborWorldPos);
//                 if (distance < 0.1f) distance = 0.1f; // Prevent division by zero
//                 
//                 float weight = 1f / distance;
//                 totalHeight += (neighborHexData.height + hexController.hexMeshHeight * 0.5f) * weight;
//                 totalWeight += weight;
//             }
//         }
//         
//         if (totalWeight > 0f)
//         {
//             return totalHeight / totalWeight;
//         }
//         
//         // Fallback to current Y position if no valid hex data found
//         return worldPos.y;
//     }
// }