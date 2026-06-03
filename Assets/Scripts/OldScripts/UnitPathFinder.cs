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
               // return ReconstructPath(currentNode);
                
                List<UnitPathStep> rawPath = ReconstructPath(currentNode);
                return SmoothPath(rawPath);
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
        int currentIndex = 0;

        while (currentIndex < path.Count)
        {
            // 1. Look-Ahead: Can we skip the next node?
            // If we are at index i, and we can see i+2, skip i+1.
            int nextTargetIndex = currentIndex + 1;
        
            // This 'Skip' logic creates the direct route
            if (nextTargetIndex + 1 < path.Count)
            {
                // Check if the angle between the current path and the skip-path is small
                Vector3 v1 = (path[nextTargetIndex].targetPosition - objTransform.position).normalized;
                Vector3 v2 = (path[nextTargetIndex + 1].targetPosition - objTransform.position).normalized;
            
                // If the angle is very direct, skip the middle node
                if (Vector3.Dot(v1, v2) > 0.95f) 
                {
                    nextTargetIndex++; 
                }
            }

            Vector3 targetPos = path[nextTargetIndex].targetPosition;

            // 2. Movement & Rotation: Combined
            // We move toward the target while rotating toward it
            while (Vector3.Distance(objTransform.position, targetPos) > 0.1f)
            {
                // Rotate towards the target
                Vector3 dir = (targetPos - objTransform.position).normalized;
                if (dir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    objTransform.rotation = Quaternion.RotateTowards(objTransform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }

                // Move towards target
                objTransform.position = Vector3.MoveTowards(objTransform.position, targetPos, movementSpeed * Time.deltaTime);
            
                yield return null;
            }

            // Move to the node we reached
            currentIndex = nextTargetIndex;
        }
    
        onComplete?.Invoke();
    }
    
    private List<UnitPathStep> SmoothPath(List<UnitPathStep> rawPath)
    {
        if (rawPath.Count <= 2) return rawPath;

        List<UnitPathStep> smoothed = new List<UnitPathStep>();
        smoothed.Add(rawPath[0]);

        int i = 0;
        while (i < rawPath.Count - 2)
        {
            // Check if we can skip the next node
            // In your case, a simple check: is the distance "straight enough"?
            Vector3 dir1 = (rawPath[i+1].targetPosition - rawPath[i].targetPosition).normalized;
            Vector3 dir2 = (rawPath[i+2].targetPosition - rawPath[i+1].targetPosition).normalized;

            // If the angle between segments is small, skip the middle node
            if (Vector3.Dot(dir1, dir2) > 0.98f) 
            {
                // Skip rawPath[i+1]
                i++; 
            }
            else
            {
                smoothed.Add(rawPath[i+1]);
                i++;
            }
        }
        smoothed.Add(rawPath[rawPath.Count - 1]);
        return smoothed;
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
