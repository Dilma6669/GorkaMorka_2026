using JetBrains.Annotations;
using UnityEngine; // Required for Vector2Int

// Phase 1.1 (Revised): HexData Struct
// Purpose: A pure data container for a single hexagon's grid coordinates and walkability.
// WorldPosition is now retrieved dynamically from the SimpleHexGrid.
public struct HexData
{
    public int HexGUID { get; set; }
    public Vector2Int GridCoordinates { get; set; }
    public float Height { get; set; }
    
    public bool IsWalkable { get; set; }
    public bool IsOccupied { get; set; }
    public string HexOccupier { get; set; }
    public bool IsClimbable { get; set; }
    public bool IsCommandSeat { get; set; }
    
    public int SeedForChildLevel { get; set; }
    public HexGridManager.GridType DestinationLevelType { get; set; }

    // This constructor handles the basic coordinate/height data
    public HexData(int Level, Vector2Int gridCoords, float height)
    {
        HexGUID = int.Parse($"{Level}{Random.Range(0, 999999)}");
        // 1. Set the mandatory fields
        GridCoordinates = gridCoords;
        Height = height;

        // 2. Set the defaults for everything else
        IsWalkable = true; // Default
        IsOccupied = false;
        HexOccupier = null;
        IsClimbable = false;
        IsCommandSeat = false;
        SeedForChildLevel = 0;
        DestinationLevelType = HexGridManager.GridType.None;
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
        return $"Hex({GridCoordinates.x}, {GridCoordinates.y}) - Walkable: {IsWalkable}";
    }
}