using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class GroundHexGrid : SimpleHexGrid
{
    [Header("Procedural Terrain Settings")]
    public float noiseScale = 0.15f;
    public float heightMultiplier = 2.0f;

    [Header("Desert Height/Noise Settings")]
    [Tooltip("Enter a value here to get a different terrain layout. Changing this value will generate a new environment.")]
    public int seed = 0; 
    public float baseHeight = 0f;

    [Tooltip("A large multiplier for the seed to create distinct noise patterns.")]
    public float seedOffsetMultiplier = 1000f; 

    [Header("Sand Dunes (Smooth Hills)")]
    public float duneScale = 0.05f;
    public float duneHeight = 8f;
    public float duneOctaves = 3;
    public float dunePersistence = 0.5f;

    [Header("Rocky Outcroppings (Sharp Peaks)")]
    public float rockScale = 0.08f;
    public float rockHeight = 15f;
    public float rockThreshold = 0.6f;
    public float rockSharpness = 2f;

    [Header("Noise Mixing")]
    public float terrainSmoothness = 0.8f;
    [Range(0.1f, 2.0f)]
    public float neighborHeightLimit = 0.8f;
    
    private new void Start()
    {
        GenerateDataGrid();
    }

    protected override void GenerateDataGrid()
    {
       GenerateDefaultGrid(); // geMINI here!!! Do I need to create a default grid fot eh ground grid to spawn its tiles properly?
        
        List<Vector2Int> keys = new List<Vector2Int>(HexagonsInGrid.Keys);
    
        Debug.Log($"fuck ground hex generate keys.Count = {keys.Count}");
        
        foreach (Vector2Int coords in keys)
        {
            // Remove the RoundToInt to keep the smooth float value
            float height = CalculatePerlinHeight(coords); 
    
            HexData data = HexagonsInGrid[coords];
            data.Height = height + baseHeight; // Store as float
            HexagonsInGrid[coords] = data;
        }

        ApplySmoothingPass();
        
        RegisterGridToSystem(true);
    }

    private float CalculatePerlinHeight(Vector2Int coords)
    {
        // Apply seed offset
        float x = coords.x + (seed * seedOffsetMultiplier);
        float z = coords.y + (seed * seedOffsetMultiplier);

        // 1. Generate Sand Dunes
        float duneNoise = GenerateOctaveNoise(x, z, duneScale, (int)duneOctaves, dunePersistence);
        float dHeight = duneNoise * duneHeight;

        // 2. Generate Rocky Outcroppings
        float rockNoise = Mathf.PerlinNoise(x * rockScale, z * rockScale);
        float rHeight = 0f;
        if (rockNoise > rockThreshold)
        {
            float rockIntensity = Mathf.Pow((rockNoise - rockThreshold) / (1f - rockThreshold), rockSharpness);
            rHeight = rockIntensity * rockHeight;
        }

        // 3. Blend them
        float blendNoise = Mathf.PerlinNoise((x + 50000f) * 0.1f, (z + 60000f) * 0.1f);
        float blendFactor = Mathf.Lerp(0.3f, 1f, terrainSmoothness);
        float finalBlend = Mathf.Clamp01(blendNoise + blendFactor - 0.5f);

        return Mathf.Lerp(dHeight + rHeight, dHeight, finalBlend);
    }

    private float GenerateOctaveNoise(float x, float z, float scale, int octaves, float persistence)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = scale;
        float maxValue = 0f;
        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }
        return value / maxValue;
    }
    
    private void ApplySmoothingPass(int iterations = 1)
    {
        for (int i = 0; i < iterations; i++)
        {
            // 1. Create a temporary dictionary to store the CALCULATED heights
            Dictionary<Vector2Int, float> newHeights = new Dictionary<Vector2Int, float>();

            // 2. Create a snapshot of the keys to avoid "Collection was modified" errors
            List<Vector2Int> allCoords = new List<Vector2Int>(HexagonsInGrid.Keys);

            foreach (var coords in allCoords)
            {
                float averageHeight = GetNeighborAverageHeight(coords);
                float current = HexagonsInGrid[coords].Height;

                // Only smooth if the gap is larger than your threshold
                if (Mathf.Abs(current - averageHeight) > neighborHeightLimit)
                {
                    newHeights[coords] = Mathf.Lerp(current, averageHeight, 0.5f);
                }
                else
                {
                    newHeights[coords] = current;
                }
            }

            // 3. Apply the changes AFTER we have finished the calculations
            foreach (var coords in allCoords)
            {
                HexData data = HexagonsInGrid[coords];
                data.Height = newHeights[coords];
                HexagonsInGrid[coords] = data;
            }
        }
    }

    private float GetNeighborAverageHeight(Vector2Int coords)
    {
        float total = 0;
        int count = 0;
    
        // Check all 6 hex directions
        Vector2Int[] directions = { 
            new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), 
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1) 
        };

        foreach (var dir in directions)
        {
            if (HexagonsInGrid.TryGetValue(coords + dir, out HexData neighbor))
            {
                total += neighbor.Height;
                count++;
            }
        }
        return count > 0 ? total / count : HexagonsInGrid[coords].Height;
    }
    
    public float GetHexHeight(Vector2Int coords)
    {
        if (HexagonsInGrid.TryGetValue(coords, out HexData data))
        {
            return data.Height;
        }
        return 0f;
    }
}