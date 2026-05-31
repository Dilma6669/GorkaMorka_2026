using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UnitPathFinder : MonoBehaviour
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
    public float maxMovementRange = 10f; // New: Max movement range in world units
    
    [Header("Movement Settings")]
    [Tooltip("Speed of movement along the path")]
    public float movementSpeed = 5f;
    
    [Tooltip("How smoothly the object rotates to face movement direction")]
    public float rotationSpeed = 180f;
    
    [Tooltip("Height offset above terrain for the moving object")]
    public float heightOffset = 0.5f;
    
    [Tooltip("Enable smooth height interpolation between hexagons")]
    public bool smoothHeightTransition = true;
    
    private HexagonController hexController;
    private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();

    // Stores the currently running movement coroutine for a specific GameObject
    private Dictionary<GameObject, Coroutine> activeMoveCoroutines = new Dictionary<GameObject, Coroutine>();
    
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
        public Vector2Int cameFromDirection; // New: Direction from parent to this node

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
    /// Represents a single step in the unit's path, including target position and rotation.
    /// </summary>
    public struct UnitPathStep
    {
        public Vector3 targetPosition;
        public Quaternion targetRotation;

        public UnitPathStep(Vector3 pos, Quaternion rot)
        {
            targetPosition = pos;
            targetRotation = rot;
        }
    }
    
    void Start()
    {
        hexController = FindObjectOfType<HexagonController>(); 
        if (hexController == null)
        {
            Debug.LogError("HexPathfinder requires a HexagonController component in the scene!");
        }
    }
    
    /// <summary>
    /// Find a path between two world positions
    /// </summary>
    public List<UnitPathStep> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos)
    {
        Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
        Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);

        // Early exit if target is out of max movement range
        if (Vector3.Distance(startWorldPos, targetWorldPos) > maxMovementRange)
        {
            Debug.LogWarning($"Target {targetWorldPos} is beyond max movement range ({maxMovementRange}) from start {startWorldPos}. Aborting pathfinding.");
            return new List<UnitPathStep>();
        }
        
        return FindPath(startGrid, targetGrid, startWorldPos); // Pass startWorldPos to the internal FindPath
    }
    
    /// <summary>
    /// Find a path between two grid positions
    /// </summary>
    private List<UnitPathStep> FindPath(Vector2Int startGrid, Vector2Int targetGrid, Vector3 startWorldPosForRangeCheck)
    {
        if (hexController == null)
        {
            Debug.LogError("HexagonController not found. Cannot find path.");
            return new List<UnitPathStep>();
        }
        
        // Clear previous search data
        nodeCache.Clear();
        
        // Initialize nodes
        HexNode startNode = GetNode(startGrid); 
        startNode.cameFromDirection = Vector2Int.zero; 
        
        HexNode targetNode = GetNode(targetGrid);
        
        if (startNode == null || targetNode == null || !targetNode.isWalkable)
        {
            Debug.LogWarning($"Cannot find path: Invalid start ({startGrid}) or target ({targetGrid}) position, or target is not walkable.");
            return new List<UnitPathStep>();
        }
        
        // A* algorithm
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
                return ReconstructPath(currentNode);
            }
            
            Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
            
            foreach (Vector2Int neighborPos in neighbors)
            {
                if (closedSet.Contains(neighborPos)) continue;
                
                HexNode neighborNode = GetNode(neighborPos);
                if (neighborNode == null) continue; 

                if (!neighborNode.isWalkable) continue;

                // New: Check if neighbor is within max movement range
                Vector3 neighborWorldPos = hexController.GetHexWorldPosition(neighborPos.x, neighborPos.y);
                if (Vector3.Distance(startWorldPosForRangeCheck, neighborWorldPos) > maxMovementRange)
                {
                    // Debug.Log($"Skipping neighbor {neighborPos} as it's out of range.");
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
        
        Debug.LogWarning("No path found!");
        return new List<UnitPathStep>();
    }
    
    /// <summary>
    /// Move a game object along a path
    /// </summary>
    public void MoveAlongPath(GameObject obj, List<UnitPathStep> path, System.Action onComplete = null)
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("MoveAlongPath called with an empty or null path.");
            onComplete?.Invoke(); 
            return;
        }
        
        if (activeMoveCoroutines.ContainsKey(obj) && activeMoveCoroutines[obj] != null)
        {
            StopCoroutine(activeMoveCoroutines[obj]);
            activeMoveCoroutines.Remove(obj);
        }

        Coroutine newCoroutine = StartCoroutine(MoveCoroutine(obj, path, () => {
            if (activeMoveCoroutines.ContainsKey(obj))
            {
                activeMoveCoroutines.Remove(obj);
            }
            onComplete?.Invoke();
        }));
        activeMoveCoroutines[obj] = newCoroutine;
    }
    
    private System.Collections.IEnumerator MoveCoroutine(GameObject obj, List<UnitPathStep> path, System.Action onComplete)
    {
        Transform objTransform = obj.transform;
        
        for (int i = 0; i < path.Count; i++) 
        {
            UnitPathStep currentStep = path[i];

            while (Quaternion.Angle(objTransform.rotation, currentStep.targetRotation) > 0.1f)
            {
                objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, currentStep.targetRotation, rotationSpeed * Time.deltaTime);
                yield return null; 
            }

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
            objTransform.rotation = currentStep.targetRotation;
        }
        
        onComplete?.Invoke();
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
    
    private List<UnitPathStep> ReconstructPath(HexNode endNode)
    {
        List<HexNode> nodePath = new List<HexNode>();
        HexNode currentNode = endNode;
        while (currentNode != null)
        {
            nodePath.Add(currentNode);
            currentNode = currentNode.parent;
        }
        nodePath.Reverse(); 

        List<UnitPathStep> pathSteps = new List<UnitPathStep>();

        for (int i = 0; i < nodePath.Count - 1; i++) 
        {
            HexNode fromNode = nodePath[i];
            HexNode toNode = nodePath[i+1];

            Vector3 stepTargetWorldPos = hexController.GetHexWorldPosition(toNode.gridPos.x, toNode.gridPos.y);
            stepTargetWorldPos.y += hexController.hexMeshHeight * 0.5f; 
            stepTargetWorldPos.y += heightOffset; 

            Vector3 directionToNext = (hexController.GetHexWorldPosition(toNode.gridPos.x, toNode.gridPos.y) - hexController.GetHexWorldPosition(fromNode.gridPos.x, fromNode.gridPos.y));
            directionToNext.y = 0; 

            Quaternion stepTargetRotation = Quaternion.identity; 

            if (directionToNext.sqrMagnitude > 0.001f) 
            {
                stepTargetRotation = Quaternion.LookRotation(directionToNext.normalized);
            }
            
            pathSteps.Add(new UnitPathStep(stepTargetWorldPos, stepTargetRotation));
        }

        return pathSteps;
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
    /// Check if a path exists between two positions
    /// </summary>
    public bool PathExists(Vector3 startPos, Vector3 endPos)
    {
        List<UnitPathStep> path = FindPath(startPos, endPos);
        return path.Count > 0;
    }
    
    /// <summary>
    /// Get the distance of a path (useful for AI decision making)
    /// </summary>
    public float GetPathDistance(Vector3 startPos, Vector3 endPos)
    {
        List<UnitPathStep> path = FindPath(startPos, endPos);
        if (path.Count == 0) return float.MaxValue;
        
        float totalDistance = 0f;
        for (int i = 0; i < path.Count - 1; i++) 
        {
            totalDistance += Vector3.Distance(path[i].targetPosition, path[i + 1].targetPosition);
        }
        return totalDistance;
    }
}




// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;
//
// public class UnitPathfinder : MonoBehaviour
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
//     public float directionChangePenalty = 0.5f; // New: Penalty for changing direction
//     
//     [Header("Movement Settings")]
//     [Tooltip("Speed of movement along the path")]
//     public float movementSpeed = 5f;
//     
//     [Tooltip("How smoothly the object rotates to face movement direction")]
//     public float rotationSpeed = 180f;
//     
//     [Tooltip("Height offset above terrain for the moving object")]
//     public float heightOffset = 0.5f;
//     
//     [Tooltip("Enable smooth height interpolation between hexagons")]
//     public bool smoothHeightTransition = true;
//     
//     private HexagonController hexController;
//     private Dictionary<Vector2Int, HexNode> nodeCache = new Dictionary<Vector2Int, HexNode>();
//
//     // Stores the currently running movement coroutine for a specific GameObject
//     private Dictionary<GameObject, Coroutine> activeMoveCoroutines = new Dictionary<GameObject, Coroutine>();
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
//         public Vector2Int cameFromDirection; // New: Direction from parent to this node
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
//     /// Represents a single step in the unit's path, including target position and rotation.
//     /// </summary>
//     public struct UnitPathStep
//     {
//         public Vector3 targetPosition;
//         public Quaternion targetRotation;
//
//         public UnitPathStep(Vector3 pos, Quaternion rot)
//         {
//             targetPosition = pos;
//             targetRotation = rot;
//         }
//     }
//     
//     void Start()
//     {
//         hexController = FindObjectOfType<HexagonController>(); // Use FindObjectOfType for HexagonController
//         if (hexController == null)
//         {
//             Debug.LogError("HexPathfinder requires a HexagonController component in the scene!");
//         }
//     }
//     
//     /// <summary>
//     /// Find a path between two world positions
//     /// </summary>
//     public List<UnitPathStep> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos)
//     {
//         Vector2Int startGrid = hexController.WorldToGridPosition(startWorldPos);
//         Vector2Int targetGrid = hexController.WorldToGridPosition(targetWorldPos);
//         
//         return FindPath(startGrid, targetGrid);
//     }
//     
//     /// <summary>
//     /// Find a path between two grid positions
//     /// </summary>
//     public List<UnitPathStep> FindPath(Vector2Int startGrid, Vector2Int targetGrid)
//     {
//         if (hexController == null)
//         {
//             Debug.LogError("HexagonController not found. Cannot find path.");
//             return new List<UnitPathStep>();
//         }
//         
//         // Clear previous search data
//         nodeCache.Clear();
//         
//         // Initialize nodes
//         // For the start node, cameFromDirection is arbitrary or zero as there's no parent
//         HexNode startNode = GetNode(startGrid); // GetNode will now initialize cameFromDirection
//         startNode.cameFromDirection = Vector2Int.zero; // Explicitly set for start node
//         
//         HexNode targetNode = GetNode(targetGrid);
//         
//         if (startNode == null || targetNode == null || !targetNode.isWalkable)
//         {
//             Debug.LogWarning($"Cannot find path: Invalid start ({startGrid}) or target ({targetGrid}) position, or target is not walkable.");
//             return new List<UnitPathStep>();
//         }
//         
//         // A* algorithm
//         List<HexNode> openSet = new List<HexNode>();
//         HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
//         
//         openSet.Add(startNode);
//         startNode.gCost = 0;
//         startNode.hCost = GetDistance(startGrid, targetGrid);
//         
//         while (openSet.Count > 0)
//         {
//             // Find node with lowest fCost
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
//             // Check if we reached the target
//             if (currentNode.gridPos == targetGrid)
//             {
//                 return ReconstructPath(currentNode);
//             }
//             
//             // Check all neighbors
//             Vector2Int[] neighbors = GetHexNeighbors(currentNode.gridPos.x, currentNode.gridPos.y);
//             
//             foreach (Vector2Int neighborPos in neighbors)
//             {
//                 if (closedSet.Contains(neighborPos)) continue;
//                 
//                 HexNode neighborNode = GetNode(neighborPos);
//                 // IMPORTANT: Add null check here! If GetNode returns null (out of bounds), skip this neighbor.
//                 if (neighborNode == null) continue; 
//
//                 if (!neighborNode.isWalkable) continue;
//                 
//                 // Set the cameFromDirection for the neighbor
//                 neighborNode.cameFromDirection = neighborPos - currentNode.gridPos;
//
//                 // Check if movement is possible (height difference)
//                 if (!CanMoveBetween(currentNode, neighborNode)) continue;
//                 
//                 // Calculate movement cost, now including direction change penalty
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
//         Debug.LogWarning("No path found!");
//         return new List<UnitPathStep>();
//     }
//     
//     /// <summary>
//     /// Move a game object along a path
//     /// </summary>
//     public void MoveAlongPath(GameObject obj, List<UnitPathStep> path, System.Action onComplete = null)
//     {
//         if (path == null || path.Count == 0)
//         {
//             Debug.LogWarning("MoveAlongPath called with an empty or null path.");
//             onComplete?.Invoke(); // Call complete action even if no path
//             return;
//         }
//         
//         // Stop any existing movement coroutine for this object
//         if (activeMoveCoroutines.ContainsKey(obj) && activeMoveCoroutines[obj] != null)
//         {
//             StopCoroutine(activeMoveCoroutines[obj]);
//             activeMoveCoroutines.Remove(obj);
//         }
//
//         // Start the new movement coroutine and store its reference
//         Coroutine newCoroutine = StartCoroutine(MoveCoroutine(obj, path, () => {
//             // Remove from active coroutines when complete
//             if (activeMoveCoroutines.ContainsKey(obj))
//             {
//                 activeMoveCoroutines.Remove(obj);
//             }
//             onComplete?.Invoke();
//         }));
//         activeMoveCoroutines[obj] = newCoroutine;
//     }
//     
//     private System.Collections.IEnumerator MoveCoroutine(GameObject obj, List<UnitPathStep> path, System.Action onComplete)
//     {
//         Transform objTransform = obj.transform;
//         
//         for (int i = 0; i < path.Count; i++) // Iterate through all points, including the last one
//         {
//             UnitPathStep currentStep = path[i];
//
//             // 1. Rotate to the target rotation for this step
//             while (Quaternion.Angle(objTransform.rotation, currentStep.targetRotation) > 0.1f)
//             {
//                 objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, currentStep.targetRotation, rotationSpeed * Time.deltaTime);
//                 yield return null; // Wait for next frame
//             }
//
//             // 2. Move to the target position for this step (only if position changes)
//             if (Vector3.Distance(objTransform.position, currentStep.targetPosition) > 0.01f)
//             {
//                 Vector3 startPos = objTransform.position;
//                 float journeyLength = Vector3.Distance(startPos, currentStep.targetPosition);
//                 float journeyTime = journeyLength / movementSpeed;
//                 float elapsedTime = 0;
//                 
//                 while (elapsedTime < journeyTime)
//                 {
//                     float t = elapsedTime / journeyTime;
//                     objTransform.position = Vector3.Lerp(startPos, currentStep.targetPosition, t);
//                     elapsedTime += Time.deltaTime;
//                     yield return null;
//                 }
//             }
//             
//             // Ensure we end up exactly at the target position/rotation for this step
//             objTransform.position = currentStep.targetPosition;
//             objTransform.rotation = currentStep.targetRotation;
//         }
//         
//         onComplete?.Invoke();
//     }
//     
//     private HexNode GetNode(Vector2Int gridPos)
//     {
//         if (nodeCache.ContainsKey(gridPos))
//         {
//             return nodeCache[gridPos];
//         }
//         
//         // Check if position is valid
//         if (hexController == null || gridPos.x < 0 || gridPos.x >= hexController.gridWidth || 
//             gridPos.y < 0 || gridPos.y >= hexController.gridHeight)
//         {
//             return null; // Return null if out of bounds or hexController is not set
//         }
//         
//         var hexData = hexController.GetHexData(gridPos.x, gridPos.y);
//         // Initialize cameFromDirection to zero or default here; it will be set properly when a parent is assigned.
//         HexNode node = new HexNode(gridPos, hexData.isWalkable, hexData.height, hexData.isRock, Vector2Int.zero); 
//         nodeCache[gridPos] = node;
//         return node;
//     }
//     
//     private Vector2Int[] GetHexNeighbors(int x, int z)
//     {
//         // Use the same neighbor calculation as HexagonController
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
//         // Apply height-based cost modifiers
//         if (heightDifference > 0)
//         {
//             baseCost *= uphillCostMultiplier;
//         }
//         else if (heightDifference < 0)
//         {
//             baseCost *= downhillCostMultiplier;
//         }
//         
//         // Apply terrain type cost modifier
//         if (to.isRock)
//         {
//             baseCost *= rockTerrainCostMultiplier;
//         }
//
//         // New: Apply penalty for changing direction
//         Vector2Int currentMoveDirection = to.gridPos - from.gridPos;
//         if (from.parent != null && from.cameFromDirection != Vector2Int.zero && from.cameFromDirection != currentMoveDirection)
//         {
//             baseCost += directionChangePenalty;
//             // Debug.Log($"Direction change from {from.cameFromDirection} to {currentMoveDirection} at {from.gridPos}. Added penalty: {directionChangePenalty}. New cost: {baseCost}");
//         }
//         
//         return baseCost;
//     }
//     
//     private float GetDistance(Vector2Int a, Vector2Int b)
//     {
//         // Hexagonal distance calculation
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
//     private List<UnitPathStep> ReconstructPath(HexNode endNode)
//     {
//         List<HexNode> nodePath = new List<HexNode>();
//         HexNode currentNode = endNode;
//         while (currentNode != null)
//         {
//             nodePath.Add(currentNode);
//             currentNode = currentNode.parent;
//         }
//         nodePath.Reverse(); // Path is now from start to end
//
//         List<UnitPathStep> pathSteps = new List<UnitPathStep>();
//
//         // Generate steps for movements between nodes (from N_i to N_{i+1})
//         // The last node in nodePath is the final destination, so we iterate up to the second-to-last node.
//         for (int i = 0; i < nodePath.Count - 1; i++) 
//         {
//             HexNode fromNode = nodePath[i];
//             HexNode toNode = nodePath[i+1];
//
//             // The target position for this step is the 'toNode's world position.
//             Vector3 stepTargetWorldPos = hexController.GetHexWorldPosition(toNode.gridPos.x, toNode.gridPos.y);
//             stepTargetWorldPos.y += hexController.hexMeshHeight * 0.5f; // Adjust to top surface
//             stepTargetWorldPos.y += heightOffset; // Add the unit's height offset
//
//             // The target rotation for this step is what the unit needs to face *before* moving to 'toNode'.
//             // This rotation is calculated based on the direction from 'fromNode' to 'toNode'.
//             Vector3 directionToNext = (hexController.GetHexWorldPosition(toNode.gridPos.x, toNode.gridPos.y) - hexController.GetHexWorldPosition(fromNode.gridPos.x, fromNode.gridPos.y));
//             directionToNext.y = 0; // Flatten for rotation
//
//             Quaternion stepTargetRotation = Quaternion.identity; 
//
//             if (directionToNext.sqrMagnitude > 0.001f) // Ensure valid direction for LookRotation
//             {
//                 stepTargetRotation = Quaternion.LookRotation(directionToNext.normalized);
//             }
//             
//             pathSteps.Add(new UnitPathStep(stepTargetWorldPos, stepTargetRotation));
//         }
//
//         return pathSteps;
//     }
//     
//     /// <summary>
//     /// Get the nearest walkable hex to a world position
//     /// </summary>
//     public Vector2Int GetNearestWalkableHex(Vector3 worldPos)
//     {
//         Vector2Int gridPos = hexController.WorldToGridPosition(worldPos);
//         
//         // Check if the position is already walkable
//         HexNode node = GetNode(gridPos);
//         if (node != null && node.isWalkable)
//         {
//             return gridPos;
//         }
//         
//         // Search in expanding rings for a walkable hex
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
//         return gridPos; // Return original if no walkable hex found
//     }
//     
//     /// <summary>
//     /// Check if a path exists between two positions
//     /// </summary>
//     public bool PathExists(Vector3 startPos, Vector3 endPos)
//     {
//         List<UnitPathStep> path = FindPath(startPos, endPos);
//         return path.Count > 0;
//     }
//     
//     /// <summary>
//     /// Get the distance of a path (useful for AI decision making)
//     /// </summary>
//     public float GetPathDistance(Vector3 startPos, Vector3 endPos)
//     {
//         List<UnitPathStep> path = FindPath(startPos, endPos);
//         if (path.Count == 0) return float.MaxValue;
//         
//         float totalDistance = 0f;
//         for (int i = 0; i < path.Count - 1; i++) // Iterate path.Count-1 times for distances between points
//         {
//             totalDistance += Vector3.Distance(path[i].targetPosition, path[i + 1].targetPosition);
//         }
//         return totalDistance;
//     }
// }
