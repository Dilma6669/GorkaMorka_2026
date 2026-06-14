using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

// Phase 1.2 (Revised for Dynamic World Positions): SimpleHexGrid Class
// Purpose: Manages a single, simple hexagonal grid. Calculates hex world positions dynamically.
// Provides access to hex data and intra-grid neighbors. Generates a circular grid pattern.
public class SimpleHexGrid : MonoBehaviour
{
    [HideInInspector]
    public HexGridVisualizerFloating HexGridVisualiser;
    [HideInInspector]
    public Entity griEntity;

    [Header("Grid Settings")] 
    public HexGridManager.GridType GridType;

    public GameObject HexagonsContainer;
    public GameObject EntityContainer;
    
    public HexGridShape customGridShape;
    
    [Tooltip(
        "The radius of the hexagonal grid. A radius of 0 is just the center hex. A radius of 1 includes the 6 direct neighbors.")]
    public int gridRadius = 5;

    [Tooltip("The size (radius) of each hexagon.")]
    public float hexSize = 1f;

    [Tooltip("An optional vertical offset for the entire grid relative to its GameObject's Y position.")]
    public float entireGridHeightOffset = 0f;
    public float singleHexHeightAdjustment = 1f; // Add this new field
    // Internal storage for all hex data in this grid.
    // HexData no longer stores WorldPosition directly, it's calculated on demand.
    public Dictionary<Vector2Int, HexData> HexagonsInGrid;

    // Corrected axial directions for flat-top hexagons (standard reference)
    // From: https://www.redblobgames.com/grids/hexagons/
    private static readonly Vector2Int[] axialDirections = new Vector2Int[]
    {
        new Vector2Int(1, 0), // Right (q+1, r)
        new Vector2Int(1, -1), // Up-Right (q+1, r-1)
        new Vector2Int(0, -1), // Up-Left (q, r-1)
        new Vector2Int(-1, 0), // Left (q-1, r)
        new Vector2Int(-1, 1), // Down-Left (q-1, r+1)
        new Vector2Int(0, 1) // Down-Right (q, r+1)
    };

    public event Action<SimpleHexGrid> OnGridReady;

    private void Awake()
    {
        HexGridVisualiser = GetComponent<HexGridVisualizerFloating>();

        griEntity = GetComponent<Entity>() ??
                 GetComponentInParent<Entity>() ??
                 GetComponentInChildren<Entity>();
        
        // This is a more robust way to set the grid's initial position.
        transform.position = new Vector3(transform.position.x, entireGridHeightOffset, transform.position.z);
        
        // Initialize the dictionary once in Awake.
        HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        
    }

    private void Start()
    {
        transform.position = new Vector3(transform.position.x, entireGridHeightOffset, transform.position.z);
        
        GenerateDataGrid();
    }
    
    
    void OnDestroy()
    {
        RegisterGridToSystem(false);
    }

    public virtual void GenerateDataGrid()
    {
        Debug.Log($"fuck simple hex generate");

        if (customGridShape != null)
        {
            GenerateCustomGrid();
        }
        else
        {
            GenerateDefaultGrid();
        }
        
        RegisterGridToSystem(true);
    }

    protected void RegisterGridToSystem(bool register)
    {
        if (register)
        {
            if (HexGridManager.Instance != null)
            {
                HexGridManager.Instance.RegisterGrid(this);
            }

            OnGridFinish();
        }
        else
        {
            if (HexGridManager.Instance != null)
            {
                HexGridManager.Instance.UnregisterGrid(this);
            }
        }
    }

    private void OnGridFinish()
    {
        Debug.Log($"fuck invoke ya cunt");
        OnGridReady?.Invoke(this);
    }

    // --- Public Access Methods ---
    
    /// <summary>
    /// Retrieves the HexData (coordinates and walkability) for a specific grid coordinate.
    /// Does NOT return WorldPosition, as it's dynamic.
    /// </summary>
    /// <param name="coords">The (x,z) grid coordinates.</param>
    /// <returns>The HexData struct, or a default/invalid HexData if coordinates are out of bounds.</returns>
    public HexData GetHexData(Vector2Int coords)
    {
        HexagonsInGrid.TryGetValue(coords, out HexData data);
        return data;
    }
    
    /// <summary>
    /// Calculates and returns the 3D world position for a given grid coordinate within this grid.
    /// This method is now the single source of truth for world positions, making them dynamic.
    /// </summary>
    /// <param name="coords">The (x,z) grid coordinates.</param>
    /// <returns>The Vector3 world position.</returns>
    public Vector3 GetHexWorldPosition(Vector2Int coords, float height)
    {
        // Calculate the hex's position in the grid's local space
        float localX = hexSize * (3f / 2f) * coords.x;
        float localZ = hexSize * (Mathf.Sqrt(3f) / 2f * coords.x + Mathf.Sqrt(3f) * coords.y);
        Vector3 localPos = new Vector3(localX, height, localZ);

        // Convert the local position to a world position using the grid's transform
        return transform.TransformPoint(localPos);
    }

    /// <summary>
    /// Returns a list of valid adjacent hex coordinates within this grid,
    /// using standard flat-top axial neighbor logic.
    /// </summary>
    /// <param name="coords">The (x,z) grid coordinates of the central hex.</param>
    /// <returns>A List of Vector2Int representing valid neighbor coordinates.</returns>
    public List<Vector2Int> GetHexNeighbors(Vector2Int coords)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        foreach (Vector2Int dir in axialDirections)
        {
            Vector2Int neighborCoords = new Vector2Int(coords.x + dir.x, coords.y + dir.y);
            if (IsValidCoordinates(neighborCoords))
            {
                neighbors.Add(neighborCoords);
            }
        }

        return neighbors;
    }
    
    /// <summary>
    /// Checks if a hex at the given coordinates is an 'edge hex', meaning it has at least one neighbor that is not on this grid.
    /// </summary>
    public bool IsEdgeHex(Vector2Int coords)
    {
        if (!HexagonsInGrid.ContainsKey(coords))
        {
            return false;
        }

        int[] qNeighbors = { 1, 0, -1, -1, 0, 1 };
        int[] rNeighbors = { 0, 1, 1, 0, -1, -1 };

        for (int i = 0; i < 6; i++)
        {
            Vector2Int neighborCoords = new Vector2Int(coords.x + qNeighbors[i], coords.y + rNeighbors[i]);
            if (!HexagonsInGrid.ContainsKey(neighborCoords))
            {
              //  Debug.Log("fuck edge hexagon for grid = " + gameObject.name);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the given grid coordinates exist within this grid's generated hexes.
    /// </summary>
    /// <param name="coords">The (x,z) grid coordinates.</param>
    /// <returns>True if coordinates are valid and exist, false otherwise.</returns>
    public bool IsValidCoordinates(Vector2Int coords)
    {
        if (HexagonsInGrid == null || HexagonsInGrid.Count == 0)
            return false;

        return HexagonsInGrid.ContainsKey(coords);
    }
    

    /// <summary>
    protected void GenerateDefaultGrid()
    {
        if (HexagonsInGrid == null)
        {
            HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        }
        HexagonsInGrid.Clear(); // Clears the dictionary instead of creating a new one.

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);

            for (int r = r1; r <= r2; r++)
            {
                Vector2Int gridCoords = new Vector2Int(q, r);
                // Now, GetHexWorldPosition requires the height.
                Vector3 worldPosition = GetHexWorldPosition(gridCoords, 0);
                
                HexagonsInGrid.Add(gridCoords, new HexData(gridCoords,0, true, true, false));
            }
        }
        
        CenterGridOnTransform();

        UpdateHexWorldPositions();
       // Debug.Log($"Generated SimpleHexGrid: DEFAULT Circular with radius {gridRadius}. Total hexes: {HexagonsInGrid.Count} at {transform.position}");
    }
    
    public void GenerateCustomGrid()
    {
        if (HexagonsInGrid == null)
        {
            HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        }
        HexagonsInGrid.Clear();

        // Check against the new 'Rows' list
        if (customGridShape == null || customGridShape.Rows == null || customGridShape.Rows.Count == 0)
        {
            Debug.LogError("Grid shape is null or empty. Cannot generate grid.");
           // GenerateDefaultGrid();
            return;
        }

        for (int q = 0; q < customGridShape.Rows.Count; q++)
        {
            int rowOffset = q / 2;
            var currentRow = customGridShape.Rows[q];

            for (int r = 0; r < currentRow.Tiles.Count; r++)
            {
                var tile = currentRow.Tiles[r];

                // THE SKIP LOGIC:
                if (!tile.IsEnabled) 
                {
                    continue; // Skips adding this coordinate to the dictionary
                }
                
                // Use the new tile properties
                float hexHeight = tile.Height * singleHexHeightAdjustment;
                bool isWalkable = tile.IsWalkable;
                bool isClimbable = tile.IsClimbable;
                bool isCommandSeat = tile.IsCommandSeat;

                // Only create the hex if it's 'walkable' or has a type (or keep it if you want to allow gaps)
                // If you want to skip empty tiles, check for a 'none' state here.
            
                Vector2Int hexCoords = new Vector2Int(q, r - rowOffset);
            
                // Pass the data to your HexData constructor
                HexagonsInGrid.Add(hexCoords, new HexData(hexCoords, hexHeight, isWalkable, isClimbable, isCommandSeat));
                Debug.Log($"Row(q): {q} | Col(r): {r} | Calculated Offset: {rowOffset} | Final Coords: {q}, {r - rowOffset}");
            }
        }
    
        CenterGridOnTransform();
    }
    
    /// <summary>
    /// Centers the grid around the parent GameObject's world position.
    /// This should be called after the grid has been fully generated.
    /// </summary>
    public void CenterGridOnTransform()
    {
        if (HexagonsInGrid == null || HexagonsInGrid.Count == 0)
        {
            return;
        }
        
        transform.localPosition = Vector3.zero;

        // Step 1: Find the bounding box of the generated hexes
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (var hex in HexagonsInGrid.Values)
        {
            Vector3 worldPos = GetHexWorldPosition(hex.GridCoordinates, hex.Height);
            if (worldPos.x < minX) minX = worldPos.x;
            if (worldPos.x > maxX) maxX = worldPos.x;
            if (worldPos.z < minZ) minZ = worldPos.z;
            if (worldPos.z > maxZ) maxZ = worldPos.z;
        }

        // Step 2: Calculate the center of the bounding box
        float centerX = (minX + maxX) / 2f;
        float centerZ = (minZ + maxZ) / 2f;
        Vector3 currentCenter = new Vector3(centerX, transform.position.y, centerZ);

        // Step 3: Apply the offset
        Vector3 offset = transform.position - currentCenter;
        transform.position += offset;

        // After moving the grid's transform, we must update all hex world positions.
        UpdateHexWorldPositions();
    }

    
    
    /// <summary>
    /// Updates the WorldPosition field for all hexagons in this grid.
    /// This should be called whenever the grid's transform changes (e.g., when the grid moves).
    /// </summary>
    public void UpdateHexWorldPositions()
    {
        var hexKeys = new List<Vector2Int>(HexagonsInGrid.Keys);
        foreach (var hexCoords in hexKeys)
        {
            HexData hexData = HexagonsInGrid[hexCoords];
            HexagonsInGrid[hexCoords] = hexData;
        }
    }
    

// Inside SimpleHexGrid.cs, after GetHexData or GetNeighbors

    /// <summary>
    /// Attempts to find the HexData and SimpleHexGrid reference for a given world position.
    /// </summary>
    /// <param name="worldPosition">The world coordinates (e.g., from a raycast hit).</param>
    /// <param name="foundHexData">Outs the HexData if a hex is found at the position.</param>
    /// <returns>True if a hex is found within this grid at the given world position, false otherwise.</returns>
    public bool GetHexAtWorldPosition(Vector3 worldPosition, out HexData foundHexData)
    {
        // First, convert the world position to a local position relative to the grid
        Vector3 relativePos = transform.InverseTransformPoint(worldPosition);

        // Remove the height offset for inverse calculation
        relativePos.y -= singleHexHeightAdjustment;

        float q_f = (2f / 3f * relativePos.x) / hexSize;
        float r_f = (-1f / 3f * relativePos.x + Mathf.Sqrt(3f) / 3f * relativePos.z) / hexSize;

        float x = q_f;
        float z = r_f;
        float y = -x - z;

        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);

        float x_diff = Mathf.Abs(rx - x);
        float y_diff = Mathf.Abs(ry - y);
        float z_diff = Mathf.Abs(rz - z);

        if (x_diff > y_diff && x_diff > z_diff)
        {
            rx = -ry - rz;
        }
        else if (y_diff > z_diff)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        Vector2Int roundedCoords = new Vector2Int(rx, rz);

        if (HexagonsInGrid.TryGetValue(roundedCoords, out foundHexData))
        {
            return true;
        }

        foundHexData = default;
        return false;
    }
    
    public float GetHexVisualHeight()
    {
        // If you have the visualizer on the same object
        var visualizer = GetComponent<HexGridVisualizerFloating>();
        return (visualizer != null) ? visualizer.hexVisualHeight : 0.1f;
    }
    
    /// <summary>
    /// Finds the HexData of the hexagon closest to a given world position.
    /// </summary>
    public bool TryGetClosestHexagon(Vector3 worldPosition, out HexData closestHexData)
    {
        closestHexData = default;
        float minDistance = float.MaxValue;
        bool found = false;

        foreach (var hexEntry in HexagonsInGrid)
        {
            // Get the accurate, up-to-date world position for the current hex.
            Vector3 hexWorldPosition = GetHexWorldPosition(hexEntry.Value.GridCoordinates, hexEntry.Value.Height);
        
            float distance = Vector3.Distance(worldPosition, hexWorldPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestHexData = hexEntry.Value;
                found = true;
            }
        }
        return found;
    }

    public Vector3 GetHexTopSurfacePosition(Vector2Int coords, float height)
    {
        if (HexGridVisualiser != null && HexGridVisualiser.TryGetVisualTile(coords, out HexVisualTile tile))
        {
            Vector3 pos = tile.GridReference.GetHexWorldPosition(coords, height);
            float surfaceY = tile.GridReference.GetHexTopSurfaceY(coords);
            
            return new Vector3(pos.x, surfaceY, pos.z);

        }
        
        return GetHexWorldPosition(coords, height);
    }
    
    public float GetHexTopSurfaceY(Vector2Int coords)
    {
        if (HexGridVisualiser != null && HexGridVisualiser.TryGetVisualTile(coords, out HexVisualTile tile))
        {
            // Use the mesh bounds to find the exact top
            Renderer rend = tile.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                return rend.bounds.max.y; // The absolute highest Y point of the mesh
            }
        }
    
        // Fallback if visualizer isn't ready
        return GetHexWorldPosition(coords, GetHexData(coords).Height).y;
    }
}