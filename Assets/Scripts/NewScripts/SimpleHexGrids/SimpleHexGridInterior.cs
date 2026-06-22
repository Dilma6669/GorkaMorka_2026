using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

// Phase 1.2 (Revised for Dynamic World Positions): SimpleHexGrid Class
// Purpose: Manages a single, simple hexagonal grid. Calculates hex world positions dynamically.
// Provides access to hex data and intra-grid neighbors. Generates a circular grid pattern.
public class SimpleHexGridInterior : SimpleHexGridBase
{
    public HexGridShape customGridShape;

    HexGridVisualizerInteroior visualizer;
    
    private new void Awake()
    {
        base.Awake();
        visualizer = GetComponent<HexGridVisualizerInteroior>();
    }

    private new void Start()
    {
        base.Start();
    }

    public override void GenerateGrid()
    {
        if (customGridShape != null)
        {
            GenerateCustomGrid();
        }
        else
        {
            GenerateDefaultGrid();
        }
        
        RegisterGridToSystem(true);
        
        AllNodes.Clear();
        foreach (var kvp in HexagonsInGrid)
        {
            AllNodes[kvp.Key] = new PathNode(kvp.Key, this);
        }
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
                float hexHeight = tile.Height * activeMapSettingsBase.singleHexHeightAdjustment;
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
    protected void GenerateDefaultGrid()
    {
        if (HexagonsInGrid == null)
        {
            HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        }
        HexagonsInGrid.Clear(); // Clears the dictionary instead of creating a new one.

        for (int q = -activeMapSettingsBase.gridRadius; q <= activeMapSettingsBase.gridRadius; q++)
        {
            int r1 = Mathf.Max(-activeMapSettingsBase.gridRadius, -q - activeMapSettingsBase.gridRadius);
            int r2 = Mathf.Min(activeMapSettingsBase.gridRadius, -q + activeMapSettingsBase.gridRadius);

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
    
    
    public override float GetHexVisualHeight()
    {
        // If you have the visualizer on the same object
        return (visualizer != null) ? visualizer.hexVisualHeight : 0.1f;
    }
    


    public override Vector3 GetHexTopSurfacePosition(Vector2Int coords, float height)
    {
        if (visualizer != null && visualizer.TryGetVisualTile(coords, out HexVisualTile tile))
        {
            Vector3 pos = tile.gridBaseReference.GetHexWorldPosition(coords, height);
            float surfaceY = tile.gridBaseReference.GetHexTopSurfaceY(coords);
            
            return new Vector3(pos.x, surfaceY, pos.z);

        }
        
        return GetHexWorldPosition(coords, height);
    }
    
    public override float GetHexTopSurfaceY(Vector2Int coords)
    {
        // 1. If we have visual tiles (GameObjects), use the high-precision renderer bounds
        if (visualizer != null && visualizer.TryGetVisualTile(coords, out HexVisualTile tile))
        { 
            return tile.hexRenderer.bounds.max.y;
        }

        float baseHexHeight = GetHexWorldPosition(coords, GetHexData(coords).Height).y;

        return baseHexHeight;
    }

    public override Vector2Int GetChunkID(Vector2Int gridCoords)
    {
        return new Vector2Int(0, 0); // TODO: This might need to be figured out
    }
}