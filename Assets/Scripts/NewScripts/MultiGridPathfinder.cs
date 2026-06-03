using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for OrderBy (for simplicity in Open Set for now)

// Phase 4.1 & 5.1: MultiGridPathfinder Class
// Purpose: Implements the core A* pathfinding algorithm across multiple (potentially) SimpleHexGrids.
// Now includes logic for inter-grid jumps.
public class MultiGridPathfinder : MonoBehaviour
{
    [Header("Pathfinding Start/End")]
    [Tooltip("The SimpleHexGrid where the path should start.")]
    public SimpleHexGrid startGrid;
    [Tooltip("The (x,z) coordinates of the starting hexagon within its grid.")]
    public Vector2Int startCoords;

    [Tooltip("The SimpleHexGrid where the path should end.")]
    public SimpleHexGrid targetGrid;
    [Tooltip("The (x,z) coordinates of the target hexagon within its grid.")]
    public Vector2Int targetCoords;

    [Header("Movement Costs")]
    [Tooltip("The movement cost for moving between adjacent hexes within the same grid.")]
    public float defaultMovementCost = 1f;
    [Tooltip("The additional cost incurred when jumping from one grid to another. - How likely to jump rather than stay on current grid. Good for jumping onto cars from side.")]
    public float jumpCost = 5f; // Cost for inter-grid jump

    [Header("Inter-Grid Jump Settings")]
    [Tooltip("The maximum horizontal distance between two hexes on different grids for a jump to be possible.")]
    public float connectionRange = 1.5f; // e.g., 1.5 times hexSize
    [Tooltip("The maximum vertical (Y-axis) difference between two hexes on different grids for a jump to be possible.")]
    public float maxVerticalDifference = 2f;

    // Add this variable to your script, in the "Inter-Grid Jump Settings" section.
    [Tooltip("A penalty applied for each unit of vertical distance for a jump.")]
    public float heightDifferencePenalty = 1.0f; 
    

    // --- Internal A* Data Structures ---
    private List<PathNode> openSet;   // Nodes to be evaluated
    private HashSet<PathNode> closedSet;  // Nodes already evaluated

    private EntityCommander entityCommander;
    
    private void Awake()
    {
        entityCommander = GetComponent<EntityCommander>();
    }

    
    /// <summary>
    /// Implements the A* pathfinding algorithm.
    /// </summary>
    /// <param name="startNode">The starting PathNode.</param>
    /// <param name="targetNode">The target PathNode.</param>
    /// <returns>A List of PathNodes representing the path, or null if no path is found.</returns>
    public List<PathNode> FindPath(PathNode startNode, PathNode targetNode)
    {
        openSet = new List<PathNode> { startNode };
        closedSet = new HashSet<PathNode>();

        startNode.GCost = 0;
        startNode.HCost = CalculateHeuristic(startNode, targetNode);

        while (openSet.Count > 0)
        {
            // Get node with the lowest FCost from openSet
            // Using OrderBy for simplicity, consider a custom Priority Queue for performance in larger grids.
            PathNode currentNode = openSet.OrderBy(node => node.FCost).First();

            if (currentNode.Equals(targetNode))
            {
                // Target found! Reconstruct and return the path.
                return ReconstructPath(currentNode);
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // Get all neighbors (both intra-grid and inter-grid jumps)
            foreach (PathNode neighbor in GetNeighbors(currentNode))
            {
                if (closedSet.Contains(neighbor))
                {
                    continue; // Skip already evaluated nodes
                }

                // Calculate tentative GCost from start to neighbor through currentNode
                float tentativeGCost = currentNode.GCost + CalculateDistanceCost(currentNode, neighbor);

                // If this new path to neighbor is shorter, or neighbor not in openSet
                // The '!openSet.Contains(neighbor)' check is important for correctly re-evaluating nodes
                // that are already in the openSet but now found a cheaper path.
                bool isInOpenSet = openSet.Contains(neighbor);
                if (tentativeGCost < neighbor.GCost || !isInOpenSet)
                {
                    neighbor.GCost = tentativeGCost;
                    neighbor.HCost = CalculateHeuristic(neighbor, targetNode);
                    neighbor.Parent = currentNode;

                    if (!isInOpenSet)
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null; // No path found
    }

// MultiGridPathfinder.cs
    /// <summary>
    /// Calculates the estimated cost from the current node to the end node (Heuristic).
    /// </summary>
    private float CalculateHeuristic(PathNode fromNode, PathNode toNode)
    {
        // Use the axial distance for the heuristic. This is consistent across all grids.
        int dx = Mathf.Abs(fromNode.GridCoordinates.x - toNode.GridCoordinates.x);
        int dy = Mathf.Abs(fromNode.GridCoordinates.y - toNode.GridCoordinates.y);
        int dz = Mathf.Abs(fromNode.GridCoordinates.x + fromNode.GridCoordinates.y - toNode.GridCoordinates.x - toNode.GridCoordinates.y);
        float heuristicDistance = (dx + dy + dz) / 2f;

        HexData fromHexData = fromNode.GridReference.GetHexData(fromNode.GridCoordinates);
        HexData toHexData = toNode.GridReference.GetHexData(toNode.GridCoordinates);
        
        // We can also add a penalty for vertical distance to make the pathfinder prefer
        // shallower jumps if there are multiple valid jump points.
        Vector3 fromPos = fromNode.GridReference.GetHexWorldPosition(fromNode.GridCoordinates, fromHexData.Height);
        Vector3 toPos = toNode.GridReference.GetHexWorldPosition(toNode.GridCoordinates, toHexData.Height);
        float verticalPenalty = Mathf.Abs(fromPos.y - toPos.y);

        // If the hexes are on different grids, add a penalty to the heuristic.
        // This correctly guides the pathfinder to prefer same-grid paths unless necessary.
       // if (fromNode.GridReference != toNode.GridReference)
       // {
            heuristicDistance += jumpCost + verticalPenalty;
            
            // Add a massive penalty if the 'to' node is NOT an edge hex on its grid.
            // This strongly discourages the algorithm from choosing paths that "jump through the floor".
            if (!toNode.GridReference.IsEdgeHex(toNode.GridCoordinates))
            {
                heuristicDistance += 9999f; // A very high cost to make this path prohibitively expensive.
            }
       // }

        return heuristicDistance;
    }

    /// <summary>
    /// Calculates the actual movement cost between two adjacent PathNodes.
    /// Distinguishes between intra-grid movement and inter-grid jumps.
    /// </summary>
    private float CalculateDistanceCost(PathNode fromNode, PathNode toNode)
    {
        // Get the height of the hexes for the cost calculation.
        HexData fromHexData = fromNode.GridReference.GetHexData(fromNode.GridCoordinates);
        HexData toHexData = toNode.GridReference.GetHexData(toNode.GridCoordinates);
        
        // CRITICAL FIX: The cost is now based on the height difference, not the absolute height.
        float verticalDifference = Mathf.Abs(fromHexData.Height - toHexData.Height);
        float heightCost = verticalDifference * heightDifferencePenalty;
        
        return defaultMovementCost + heightCost;
    }
    
    /// <summary>
    /// Gets all valid neighbors for a given PathNode, including intra-grid hexes
    /// and potential jump points to other grids.
    /// </summary>
    private List<PathNode> GetNeighbors(PathNode currentNode)
    {
        List<PathNode> neighbors = new List<PathNode>();
        
        HexData currentHexData = currentNode.GridReference.GetHexData(currentNode.GridCoordinates);
        Vector3 currentWorldPos =
            currentNode.GridReference.GetHexWorldPosition(currentNode.GridCoordinates, currentHexData.Height);

        // --- 1. Intra-Grid Neighbors (SAME GRID AS ENTITY) ---
        List<Vector2Int> localNeighborCoords = currentNode.GridReference.GetHexNeighbors(currentNode.GridCoordinates);
        foreach (Vector2Int coords in localNeighborCoords)
        {
            HexData neighbourHexData = currentNode.GridReference.GetHexData(coords);
            Vector3 neighbourWorldPos = currentNode.GridReference.GetHexWorldPosition(coords, neighbourHexData.Height);

            if (entityCommander.entityToCommand.EntityType == EntitySpawner.EntityType.Vehicle)
            {
                if (currentNode.GridReference == entityCommander.entityToCommand.EntityGrid)
                {
                    continue;
                }
                if (!neighbourHexData.GetIsWalkable()) continue;
                if (neighbourHexData.GetIsOccupied() && neighbourHexData.GetOccupier() != entityCommander.entityToCommand.EntityGUID) continue;
            }
            else if (entityCommander.entityToCommand.EntityType == EntitySpawner.EntityType.Unit)
            {
                // Check if otherHex is walkable
                if (!neighbourHexData.GetIsWalkable() || neighbourHexData.GetIsOccupied())
                {
                    continue;
                }
            }
            
            // Calculate horizontal and vertical distance
            float horizontalDist = Vector2.Distance(new Vector2(currentWorldPos.x, currentWorldPos.z),
                new Vector2(neighbourWorldPos.x, neighbourWorldPos.z));
            float verticalDist = Mathf.Abs(currentHexData.Height - neighbourHexData.Height);

            if (horizontalDist <= connectionRange && verticalDist <= maxVerticalDifference)
            {
                neighbors.Add(new PathNode(coords, currentNode.GridReference));
                
                if (currentWorldPos.y > neighbourWorldPos.y) // If jumping DOWN
                {
                    if (currentNode.GridReference.IsEdgeHex(currentNode.GridCoordinates))
                    {
                        neighbors.Add(new PathNode(neighbourHexData.GridCoordinates, currentNode.GridReference));
                    }
                }
                else // If jumping UP or staying at the same height
                {
                    neighbors.Add(new PathNode(neighbourHexData.GridCoordinates, currentNode.GridReference));
                }
            }
        }

        // --- 2. Inter-Grid Neighbors (JUMPING TO DIFFERENT GRID) ---
        foreach (SimpleHexGrid otherGrid in HexGridManager.Instance.GetAllGrids())
        {
            // If its a vehicle we want to not allow the vehicle to pathfind over itself 
            if (entityCommander.entityToCommand.EntityType == EntitySpawner.EntityType.Vehicle)
            {
                if (entityCommander.entityToCommand.currentGrid == otherGrid)
                {
                 //   Debug.Log("fuck here");
                    continue;
                }
            }

            if (otherGrid == currentNode.GridReference)
            {
                continue;
            }

            foreach (HexData otherHexData in otherGrid.HexagonsInGrid.Values)
            {
                if (!otherHexData.GetIsWalkable()) continue;
                if (!otherHexData.GetIsClimbable()) continue;
                if (otherHexData.GetIsOccupied()) continue;


                Vector3 otherHexWorldPos =
                    otherGrid.GetHexWorldPosition(otherHexData.GridCoordinates, otherHexData.Height);

                // Calculate horizontal and vertical distance
                float horizontalDist = Vector2.Distance(new Vector2(currentWorldPos.x, currentWorldPos.z),
                    new Vector2(otherHexWorldPos.x, otherHexWorldPos.z));
                float verticalDist = Mathf.Abs(currentWorldPos.y - otherHexWorldPos.y);

                // Check if within jump range
                if (horizontalDist <= connectionRange && verticalDist <= maxVerticalDifference)
                {
                    // Found a valid jump point!
                    if (currentWorldPos.y > otherHexWorldPos.y) // If jumping DOWN
                    {
                        if (currentNode.GridReference.IsEdgeHex(currentNode.GridCoordinates))
                        {
                            neighbors.Add(new PathNode(otherHexData.GridCoordinates, otherGrid));
                        }
                    }
                    else // If jumping UP or staying at the same height
                    {
                        neighbors.Add(new PathNode(otherHexData.GridCoordinates, otherGrid));
                    }
                }
            }
        }

        return neighbors;
    }
    

    /// <summary>
    /// Reconstructs the path from the target node back to the start node.
    /// </summary>
    private List<PathNode> ReconstructPath(PathNode targetNode)
    {
        List<PathNode> path = new List<PathNode>();
        PathNode currentNode = targetNode;
        while (currentNode != null)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }
        path.Reverse(); // Reverse to get path from start to end
        return path;
    }
    
    private bool IsTileBlockedByMe(Vector2Int coords, SimpleHexGrid grid)
    {
        var vehicle = entityCommander.entityToCommand.GetComponentInChildren<HexagonCollider>();
        if (vehicle == null) return false;

        // Check if any tile in the vehicle's list matches the coordinates we are checking
        foreach (HexVisualTile blockedTile in vehicle.currentlyBlockedTiles)
        {
            if (blockedTile.GridCoordinates == coords && blockedTile.GridReference == grid)
            {
                return true;
            }
        }
        return false;
    }
}