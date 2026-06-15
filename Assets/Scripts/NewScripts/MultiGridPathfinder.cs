using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization; // Required for OrderBy (for simplicity in Open Set for now)

// Phase 4.1 & 5.1: MultiGridPathfinder Class
// Purpose: Implements the core A* pathfinding algorithm across multiple (potentially) SimpleHexGrids.
// Now includes logic for inter-grid jumps.
public class MultiGridPathfinder : MonoBehaviour
{
    [FormerlySerializedAs("startGrid")]
    [Header("Pathfinding Start/End")]
    [Tooltip("The SimpleHexGrid where the path should start.")]
    public SimpleHexGridBase startGridBase;
    [Tooltip("The (x,z) coordinates of the starting hexagon within its grid.")]
    public Vector2Int startCoords;

    [FormerlySerializedAs("targetGrid")] [Tooltip("The SimpleHexGrid where the path should end.")]
    public SimpleHexGridBase targetGridBase;
    [Tooltip("The (x,z) coordinates of the target hexagon within its grid.")]
    public Vector2Int targetCoords;

    [Header("Movement Costs")]
    [Tooltip("The movement cost for moving between adjacent hexes within the same grid.")]
    public float defaultMovementCost = 1f;
    [Tooltip("The additional cost incurred when jumping from one grid to another. - How likely to jump rather than stay on current grid. Good for jumping onto cars from side.")]
    public float jumpCost = 1f; // Cost for inter-grid jump

    [Header("Inter-Grid Jump Settings")]
    [Tooltip("The maximum horizontal distance between two hexes on different grids for a jump to be possible.")]
    public float connectionRange = 3; // e.g., 1.5 times hexSize
    [Tooltip("The maximum vertical (Y-axis) difference between two hexes on different grids for a jump to be possible.")]
    public float maxVerticalDifference = 1f;

    // Add this variable to your script, in the "Inter-Grid Jump Settings" section.
    [Tooltip("A penalty applied for each unit of vertical distance for a jump.")]
    public float heightDifferencePenalty = 10.0f; 
    
    [Header("Jump Constraints")]
    public float maxClimbHeight = 1.0f; // Limit for jumping UP onto things
    public float maxDropHeight = 3.0f;  // Limit for jumping DOWN off things

    // --- Internal A* Data Structures ---
    private List<PathNode> openSet;   // Nodes to be evaluated
    private HashSet<PathNode> closedSet;  // Nodes already evaluated
    
    public static MultiGridPathfinder Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
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
                float tentativeGCost = currentNode.GCost + CalculateDistanceCost(currentNode, neighbor, targetNode);

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

        HexData fromHexData = fromNode.GridBaseReference.GetHexData(fromNode.GridCoordinates);
        HexData toHexData = toNode.GridBaseReference.GetHexData(toNode.GridCoordinates);
        
        // We can also add a penalty for vertical distance to make the pathfinder prefer
        // shallower jumps if there are multiple valid jump points.
        Vector3 fromPos = fromNode.GridBaseReference.GetHexWorldPosition(fromNode.GridCoordinates, fromHexData.Height);
        Vector3 toPos = toNode.GridBaseReference.GetHexWorldPosition(toNode.GridCoordinates, toHexData.Height);
        float verticalPenalty = Mathf.Abs(fromPos.y - toPos.y);

        // If the hexes are on different grids, add a penalty to the heuristic.
        // This correctly guides the pathfinder to prefer same-grid paths unless necessary.
       // if (fromNode.GridReference != toNode.GridReference)
       // {
            heuristicDistance += jumpCost + verticalPenalty;
            
            // Add a massive penalty if the 'to' node is NOT an edge hex on its grid.
            // This strongly discourages the algorithm from choosing paths that "jump through the floor".
            if (!toNode.GridBaseReference.IsEdgeHex(toNode.GridCoordinates))
            {
                heuristicDistance += 9999f; // A very high cost to make this path prohibitively expensive.
            }
       // }

        return heuristicDistance * 1.001f;
    }

    /// <summary>
    /// Calculates the actual movement cost between two adjacent PathNodes.
    /// Distinguishes between intra-grid movement and inter-grid jumps.
    /// </summary>
    private float CalculateDistanceCost(PathNode fromNode, PathNode toNode, PathNode goalNode) // Add goalNode
    {
        HexData fromHexData = fromNode.GridBaseReference.GetHexData(fromNode.GridCoordinates);
        HexData toHexData = toNode.GridBaseReference.GetHexData(toNode.GridCoordinates);
    
        float verticalDifference = Mathf.Abs(fromHexData.Height - toHexData.Height);
        float heightCost = verticalDifference * heightDifferencePenalty;
    
        // Use the goalNode reference instead of class-level variables
        Vector3 toPos = toNode.GridBaseReference.GetHexWorldPosition(toNode.GridCoordinates, toHexData.Height);
        Vector3 targetWorldPos = goalNode.GridBaseReference.GetHexWorldPosition(goalNode.GridCoordinates, goalNode.GridBaseReference.GetHexData(goalNode.GridCoordinates).Height);
    
        float distanceToTarget = Vector3.Distance(toPos, targetWorldPos);
        float directionalBias = distanceToTarget * 10f; 
    
        return defaultMovementCost + heightCost + directionalBias;
    }
    
    /// <summary>
    /// Gets all valid neighbors for a given PathNode, including intra-grid hexes
    /// and potential jump points to other grids.
    /// </summary>
    private List<PathNode> GetNeighbors(PathNode currentNode)
    {
        List<PathNode> neighbors = new List<PathNode>();
        
        HexData currentHexData = currentNode.GridBaseReference.GetHexData(currentNode.GridCoordinates);
        Vector3 currentWorldPos =
            currentNode.GridBaseReference.GetHexWorldPosition(currentNode.GridCoordinates, currentHexData.Height);

        // Need to check vehicles internal grid here so caching retrieval of vehicle for performance
        VehicleEntity vehicleAlreadySelected = null;
        if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
        {
            vehicleAlreadySelected = EntityCommander.GetEntityInCommand() as VehicleEntity;
        }
        
        // Dont need this yet but may do it future
        UnitEntity unitAlreadySelected = null;
        if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
        {
            unitAlreadySelected = EntityCommander.GetEntityInCommand() as UnitEntity;
        }
        
        //******************************************************************************
        // --- 1. Intra-Grid Neighbors (SAME GRID AS ENTITY) ---
        List<Vector2Int> localNeighborCoords = currentNode.GridBaseReference.GetHexNeighbors(currentNode.GridCoordinates);
        foreach (Vector2Int coords in localNeighborCoords)
        {
            HexData neighbourHexData = currentNode.GridBaseReference.GetHexData(coords);
            Vector3 neighbourWorldPos = currentNode.GridBaseReference.GetHexWorldPosition(coords, neighbourHexData.Height);
            
            if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Vehicle)
            {
                if (currentNode.GridBaseReference == vehicleAlreadySelected.vehicleInteriorGridBase)
                {
                    continue;
                }
                if (!neighbourHexData.GetIsWalkable()) continue;
                // I think catch to allow vehicle driving over its own tiles 
                if (neighbourHexData.GetIsOccupied() && neighbourHexData.GetOccupier() != EntityCommander.GetEntityInCommand().EntityGUID)
                {
                    continue;
                }
            }
            else if (EntityCommander.GetEntityInCommand().EntityType == EntitySpawner.EntityType.Unit)
            {
                // Check if otherHex is walkable
                if (!neighbourHexData.GetIsWalkable() || neighbourHexData.GetIsOccupied())
                {
                    continue;
                }
            }
            
            // Get positions using your new helper method
            Vector3 currentSurfacePos = currentNode.GridBaseReference.GetHexTopSurfacePosition(currentNode.GridCoordinates, currentNode.GridBaseReference.GetHexData(currentNode.GridCoordinates).Height);
            Vector3 neighbourSurfacePos = currentNode.GridBaseReference.GetHexTopSurfacePosition(coords, neighbourHexData.Height);

            // Calculate horizontal distance using X and Z
            float horizontalDist = Vector2.Distance(
                new Vector2(currentSurfacePos.x, currentSurfacePos.z),
                new Vector2(neighbourSurfacePos.x, neighbourSurfacePos.z)
            );

            // Calculate vertical distance using Y
            float verticalDist = Mathf.Abs(currentSurfacePos.y - neighbourSurfacePos.y);
            
            if (verticalDist <= maxVerticalDifference)
            {
              //  neighbors.Add(new PathNode(coords, currentNode.GridReference));

                if (currentWorldPos.y > neighbourWorldPos.y) // If jumping DOWN
                {
                   // if (currentNode.GridReference.IsEdgeHex(currentNode.GridCoordinates))
                    {
                        neighbors.Add(new PathNode(neighbourHexData.GridCoordinates, currentNode.GridBaseReference));
                    }
                }
                else // If jumping UP or staying at the same height
                {
                    neighbors.Add(new PathNode(neighbourHexData.GridCoordinates, currentNode.GridBaseReference));
                }
            }
        }
        //******************************************************************************
        
        
        //******************************************************************************
        // --- 2. Inter-Grid Neighbors (JUMPING TO DIFFERENT GRID) ---
        foreach (SimpleHexGridBase otherGrid in HexGridManager.Instance.GetAllGrids())
        {
            // If its a vehicle we want to not allow the vehicle to pathfind over itself 
            if (vehicleAlreadySelected != null)
            {
                if (vehicleAlreadySelected.vehicleInteriorGridBase == otherGrid)
                {
                    continue;
                }
            }
            
            // Needs to be here coz This is meant for stop looking for nodes in the IntRA grid section
            if (otherGrid == currentNode.GridBaseReference)
            {
                continue;
            }
            
            // --- Inside your loop ---
            foreach (HexData otherHexData in otherGrid.HexagonsInGrid.Values)
            {
                if (!otherHexData.GetIsWalkable()) continue;
                if (!otherHexData.GetIsClimbable()) continue;
                if (otherHexData.GetIsOccupied()) continue;
                
                Vector3 currentSurfacePos = currentNode.GridBaseReference.GetHexTopSurfacePosition(currentNode.GridCoordinates, currentNode.GridBaseReference.GetHexData(currentNode.GridCoordinates).Height);
                Vector3 otherSurfacePos = otherGrid.GetHexTopSurfacePosition(otherHexData.GridCoordinates, otherHexData.Height);

                // Calculate distance
                float horizontalDist = Vector2.Distance(
                    new Vector2(currentSurfacePos.x, currentSurfacePos.z),
                    new Vector2(otherSurfacePos.x, otherSurfacePos.z)
                );

                if (horizontalDist > connectionRange) continue;

                // Calculate TRUE height difference based on top surfaces
                float heightDiff = currentSurfacePos.y - otherSurfacePos.y;

                // Determine if the jump is valid
                bool canJump = false;
                if (horizontalDist <= connectionRange)
                {
                    if (heightDiff < 0) // We are jumping UP
                    {
                        if (Mathf.Abs(heightDiff) <= maxClimbHeight) canJump = true;
                    }
                    else // We are jumping DOWN (heightDiff is positive or zero)
                    {
                        if (heightDiff <= maxDropHeight) canJump = true;
                    }
                }

                if (canJump)
                {
                    neighbors.Add(new PathNode(otherHexData.GridCoordinates, otherGrid));
                }
                else 
                {
                    // ADD THIS:
                    Debug.Log($"Pathfinder rejected jump: Grid={otherGrid.name}, Dist={horizontalDist}, HeightDiff={heightDiff}");
                }
            }
            //******************************************************************************
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
}