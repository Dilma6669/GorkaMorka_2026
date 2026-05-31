using UnityEngine; // Required for Vector2Int
using System;      // Required for Math.Abs for cubic distance and HashCode.Combine

// Phase 3.1: PathNode Class
// Purpose: Represents a single node in the A* pathfinding search.
// It uniquely identifies a hexagon across potentially multiple grids and stores A* specific costs.
public class PathNode : IComparable<PathNode> // Implement IComparable for potential use in sorted collections (e.g., Priority Queue)
{
    // --- Node Identity ---
    public Vector2Int GridCoordinates; // The (x, z) coordinates of the hex within its grid.
    public SimpleHexGrid GridReference; // Reference to the specific SimpleHexGrid this hex belongs to.

    // --- A* Specific Costs ---
    public float GCost;   // Cost from the starting node to this node.
    public float HCost;   // Heuristic cost (estimated cost) from this node to the target node.
    public PathNode Parent; // The node that immediately preceded this node on the cheapest path found so far.

    // Calculated total cost for A* (G + H)
    public float FCost => GCost + HCost;

    // --- Constructor ---
    public PathNode(Vector2Int gridCoords, SimpleHexGrid gridRef)
    {
        GridCoordinates = gridCoords;
        GridReference = gridRef;
        // Costs and Parent will be set by the A* algorithm
        GCost = float.MaxValue; // Initialize GCost to a very large number
        HCost = 0;              // Heuristic cost will be calculated later
        Parent = null;
    }

    // --- Identity and Comparison Overrides ---
    // These are CRUCIAL for PathNode to work correctly in collections like HashSet (for closedSet)
    // and Dictionary (for efficient node lookup by coordinates+grid).
    // They define what makes two PathNodes "equal".

    public override bool Equals(object obj)
    {
        // If the object is not a PathNode or is null, they are not equal.
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        PathNode other = (PathNode)obj;
        // Two PathNodes are equal if they refer to the same hex coordinates on the same grid.
        return GridCoordinates.Equals(other.GridCoordinates) &&
               GridReference.Equals(other.GridReference);
    }

    public override int GetHashCode()
    {
        // Combine the hash codes of the identifying properties.
        // This ensures that two equal PathNodes produce the same hash code.
        return HashCode.Combine(GridCoordinates, GridReference);
    }

    // --- For Sorting (e.g., in a Priority Queue) ---
    // This allows us to sort PathNodes by their FCost, then by HCost as a tie-breaker.
    public int CompareTo(PathNode other)
    {
        int compare = FCost.CompareTo(other.FCost);
        if (compare == 0)
        {
            // If FCosts are equal, prefer nodes closer to target (lower HCost)
            compare = HCost.CompareTo(other.HCost);
        }
        return compare;
    }

    public override string ToString()
    {
        return $"Node: Grid({GridReference.name}), Coords({GridCoordinates.x},{GridCoordinates.y}), F:{FCost:F1}";
    }
}