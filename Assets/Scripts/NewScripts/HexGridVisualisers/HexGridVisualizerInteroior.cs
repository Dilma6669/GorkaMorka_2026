using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic; // Required for List and Dictionary

// Phase 2.1 (Revised for GameObject Hexes and Visual Control): HexGridVisualizer Class
// Purpose: Instantiates visual hexagon GameObjects for a SimpleHexGrid.
// Now also manages references to individual HexVisualTiles for color manipulation.
public class HexGridVisualizerInteroior : HexGridVisualizerBase
{
    [Header("Visuals Settings")]
    [Tooltip("The prefab GameObject to use for each hexagon (must have a HexVisualTile component and a Renderer).")]
    public GameObject hexPrefab;

    [Header("Tile Colors")] 
    public Color unwalkableColor = Color.red;
    public Color climbableColor = Color.blue;
    public Color occupiedColour = Color.red;
    public Color commandSeatColour = Color.green;

    // A dictionary to quickly find a HexVisualTile by its axial coordinates
    private Dictionary<Vector2Int, HexVisualTile> visualTiles;

    private bool edgeHexagonsVisible;

    protected override void Awake()
    {
        base.Awake();
    }
    
    /// <summary>
    /// Instantiates visual hex GameObjects for the target SimpleHexGrid.
    /// Now initializes and stores HexVisualTile components.
    /// </summary>
    public override void GenerateVisualGrid(SimpleHexGridBase hexGridBase)
    {
        // Clear any existing visual hexes before generating new ones
        ClearVisualGrid();

        if (hexPrefab == null)
        {
            Debug.LogError("HexGridVisualizer: Hex Prefab is not assigned!");
            return;
        }

        if (_targetGridBase == null)
        {
            Debug.LogError("HexGridVisualizer: Target Grid is not assigned!");
            return;
        }

        if (_targetGridBase.HexagonsInGrid.Count == 0)
        {
            Debug.LogError("Visualizer cannot generate: Grid data has not been initialized yet!");
            return;
        }


        visualTiles = new Dictionary<Vector2Int, HexVisualTile>(); // Initialize the dictionary

        // Iterate through all hexes in the data grid and create their visual counterparts
        foreach (KeyValuePair<Vector2Int, HexData> hexDataPair in _targetGridBase.HexagonsInGrid)
        {
            Vector2Int coords = hexDataPair.Key;
            Vector3 worldPos = _targetGridBase.GetHexWorldPosition(coords, hexDataPair.Value.Height);

            GameObject hexInstance = Instantiate(hexPrefab, worldPos, Quaternion.identity);

            hexInstance.transform.SetParent(HexagonsContainer.transform); // Parent to visualizer for organization
            hexInstance.name = $"Hex_{coords.x},{coords.y}"; // Name for easier debugging

            // Automatically resize the hex based on the targetGrid's hexSize
            // Unity's default Cylinder primitive (at scale 1,1,1) has a radius of 0.5.
            // Our hexSize is the desired outer radius (e.g., 1.0).
            // So, we need to scale the prefab by (hexSize / 0.5) in X and Z, which simplifies to hexSize * 2.
            float scaleFactorXZ = _targetGridBase.activeMapSettingsBase.hexSize * 2f;
            hexInstance.transform.localScale = new Vector3(scaleFactorXZ, hexVisualHeight, scaleFactorXZ);

            // --- NEW: Get and Initialize HexVisualTile ---
            HexVisualTile visualTile = hexInstance.GetComponent<HexVisualTile>();

            // Debug.Log($"Tile {coords}: Walkable={hexDataPair.Value.isWalkable}, Climbable={hexDataPair.Value.isClimbable}");

            if (visualTile != null)
            {
                visualTile.Initialize(_targetGridBase, coords, hexDataPair.Value.Height,
                    hexDataPair.Value.GetIsWalkable(),
                    hexDataPair.Value.GetIsClimbable(), hexDataPair.Value.GetIsOccupied(),
                    hexDataPair.Value.GetIsCommandSeat());
                visualTiles.Add(coords, visualTile); // Store the reference
            }
            else
            {
                //  Debug.LogWarning($"HexGridVisualizer: Hex Prefab '{hexPrefab.name}' is missing a HexVisualTile component! Cannot control its visuals.", hexPrefab);
            }

            if (!hexDataPair.Value.GetIsWalkable())
            {
                visualTile.ColourLocked = false; // Ensure we can change it
                visualTile.SetBaseColor(unwalkableColor);
                visualTile.ColourLocked = true;
            }
            else if (hexDataPair.Value.GetIsClimbable())
            {
                visualTile.ColourLocked = false;
                visualTile.SetBaseColor(climbableColor);
                // visualTile.ColourLocked = true;
            }
            else if (hexDataPair.Value.GetIsOccupied())
            {
                // visualTile.ColourLocked = false;
                visualTile.SetBaseColor(occupiedColour);
                //visualTile.ColourLocked = true;
            }
            else if (hexDataPair.Value.GetIsCommandSeat())
            {
                // visualTile.ColourLocked = false;
                visualTile.SetBaseColor(commandSeatColour);
                //visualTile.ColourLocked = true;
            }
            else
            {
                // Explicitly reset the color for normal tiles
                visualTile.ColourLocked = false;
                visualTile.ResetBaseColor();
            }
        }
    }

    public bool TryGetVisualTile(Vector2Int coords, out HexVisualTile tile)
    {
        return visualTiles.TryGetValue(coords, out tile);
    }
    
    [ContextMenu("Generate Visual Grid")]
    public void GenerateVisualGridEditor()
    {
        // Ensure we have a reference to the grid
        if (_targetGridBase == null)
        {
            _targetGridBase = GetComponent<SimpleHexGridInterior>();
        }

        _targetGridBase.GenerateGrid();

        // Call the main generation method
        GenerateVisualGrid(_targetGridBase);
    }

    /// <summary>
    /// Destroys all currently instantiated visual hex GameObjects.
    /// </summary>
    [ContextMenu("Clear Visual Grid")]
    public void ClearVisualGrid()
    {
        if (visualTiles != null)
        {
            foreach (HexVisualTile tile in visualTiles.Values)
            {
                if (tile != null && tile.gameObject != null)
                {
                    DestroyImmediate(tile.gameObject); // Use DestroyImmediate for editor context menu
                }
            }

            visualTiles.Clear();
            //  Debug.Log($"Cleared visual grid for '{targetGrid.name}'.");
        }
    }

    [ContextMenu("Refresh Grid Data")]
    public virtual void RefreshGridData()
    {
        if (_targetGridBase == null) return;

        // 1. Force the data to update
        _targetGridBase.GenerateGrid();

        // 2. Clear then Rebuild
        ClearVisualGrid();
        GenerateVisualGrid(_targetGridBase);

        //   Debug.Log("HexGridVisualizer: Grid data and visuals refreshed.");
    }

    public void VisualizeEdgeHexes()
    {
        bool showEdges = !edgeHexagonsVisible;

        // Get the hex grid data
        var hexGridData = _targetGridBase.HexagonsInGrid;

        // A distinct color for the edges, for example, yellow
        Color edgeColor = Color.yellow;

        foreach (var entry in hexGridData)
        {
            Vector2Int coords = entry.Key;
            // Check if this hex is an edge hex using the method we added
            if (_targetGridBase.IsEdgeHex(coords))
            {
                if (visualTiles.ContainsKey(coords))
                {
                    HexVisualTile tile = visualTiles[coords];
                    if (showEdges)
                    {
                        tile.SetBaseColor(edgeColor);
                    }
                    else
                    {
                        // If we're not showing edges, reset to the original color
                        tile.ResetBaseColor();
                    }
                }
            }
            else
            {
                // Reset non-edge tiles if we are in 'showEdges' mode to avoid old colors
                if (showEdges)
                {
                    if (visualTiles.ContainsKey(coords))
                    {
                        visualTiles[coords].ResetBaseColor();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets the color of a specific hexagon to its original color.
    /// </summary>
    /// <param name="coords">The axial coordinates of the hex to reset.</param>
    public void ResetHexBaseColor(Vector2Int coords)
    {
        if (visualTiles != null && visualTiles.TryGetValue(coords, out HexVisualTile tile))
        {
            tile.ResetBaseColor();

            //  currentColouredTiles.Remove(coords);
        }
    }

    /// <summary>
    /// Resets the color of all hexagons in this visual grid to their original colors.
    /// </summary>
    public void ResetAllHexBaseColors()
    {
        if (visualTiles != null)
        {
            foreach (HexVisualTile tile in visualTiles.Values)
            {
                if (tile != null) // Ensure tile wasn't destroyed mid-loop
                {
                    tile.ResetBaseColor();
                }
            }
        }
    }
}