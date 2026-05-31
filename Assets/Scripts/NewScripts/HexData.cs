using UnityEngine; // Required for Vector2Int

// Phase 1.1 (Revised): HexData Struct
// Purpose: A pure data container for a single hexagon's grid coordinates and walkability.
// WorldPosition is now retrieved dynamically from the SimpleHexGrid.
public struct HexData
{
    // Input: These values are set when a HexData instance is created.
    public Vector2Int GridCoordinates; // The (x, z) coordinates of the hex within its grid.
   // public Vector3 WorldPosition;
    public float Height; // Add this new field to store the height
    public bool isWalkable;            // A flag indicating if this hex can be traversed.
    public bool isClimbable;

    public HexData(Vector2Int gridCoords, float height, bool walkable, bool climbable)
    {
        GridCoordinates = gridCoords;
        Height = height;
        isWalkable = walkable;
        isClimbable = climbable;
    }

    
    /*public void SetWorldPosition(Vector3 worldPosition)
    {
        WorldPosition = worldPosition;
    }*/
    
    public bool IsClimbable()
    {
        return isClimbable;
    }

    public void SetIsWalkable(bool walkable)
    {
        isWalkable = walkable;
    }
    
    public bool GetIsWalkable()
    {
        return isWalkable;
    }

    // Overriding Equals and GetHashCode is crucial for using HexData correctly
    // in collections like HashSets or Dictionaries later.
    // Equality is now based ONLY on GridCoordinates, as WorldPosition is dynamic.
    public override bool Equals(object obj)
    {
        if (obj is HexData other)
        {
            // Only GridCoordinates are used for identity.
            return GridCoordinates == other.GridCoordinates;
        }
        return false;
    }

    public override int GetHashCode()
    {
        // Use GridCoordinates for hash code.
        return GridCoordinates.GetHashCode();
    }

    public override string ToString()
    {
        return $"Hex({GridCoordinates.x}, {GridCoordinates.y}) - Walkable: {isWalkable}";
    }
}