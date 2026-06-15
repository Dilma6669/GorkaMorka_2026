using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class SimpleHexGridGround : SimpleHexGridBase
{
    [Header("Procedural Terrain Settings")]
    public float noiseScale = 0.15f;

    public float heightMultiplier = 2.0f;

    [Header("Desert Height/Noise Settings")]
    [Tooltip(
        "Enter a value here to get a different terrain layout. Changing this value will generate a new environment.")]
    public int seed = 0;

    public float baseHeight = 0f;

    [Tooltip("A large multiplier for the seed to create distinct noise patterns.")]
    public float seedOffsetMultiplier = 1000f;

    [Header("Sand Dunes (Smooth Hills)")] public float duneScale = 0.05f;
    public float duneHeight = 8f;
    public float duneOctaves = 3;
    public float dunePersistence = 0.5f;

    [Header("Rocky Outcroppings (Sharp Peaks)")]
    public float rockScale = 0.08f;

    public float rockHeight = 15f;
    public float rockThreshold = 0.6f;
    public float rockSharpness = 2f;

    [Header("Noise Mixing")] public float terrainSmoothness = 0.8f;
    [Range(0.1f, 2.0f)] public float neighborHeightLimit = 0.8f;

    HexGridVisualizerGround visualizer;
    public float entityPlacementHeightOffset = 0.05f;

    private new void Awake()
    {
        base.Awake();

        visualizer = GetComponent<HexGridVisualizerGround>();
    }

    private new void Start()
    {
        base.Start();
    }

    public override void GenerateGrid()
    {
        // 1. Initialize the dictionary
        if (HexagonsInGrid == null) HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        HexagonsInGrid.Clear();

        // 2. Generate the circular pattern (the same logic used for default grids)
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Abs(q + r) <= gridRadius)
                {
                    Vector2Int coords = new Vector2Int(q, r);

                    // Start with base data (default height 0)
                    HexData data = new HexData(coords, 0f, true, true, false);
                    HexagonsInGrid[coords] = data;
                }
            }
        }

        foreach (var coords in new List<Vector2Int>(HexagonsInGrid.Keys))
        {
            // Remove the RoundToInt to keep the smooth float value
            float height = CalculatePerlinHeight(coords);

            HexData data = HexagonsInGrid[coords];
            data.Height = height + baseHeight; // Store as float
            HexagonsInGrid[coords] = data;
        }

        ApplySmoothingPass();
        
        GeneratePhysicsProxy();

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
        Vector2Int[] directions =
        {
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

    public override Vector3 GetHexTopSurfacePosition(Vector2Int coords, float height)
    {
        // 1. Get the X and Z from the standard math
        Vector3 basePos = GetHexWorldPosition(coords, height);

        // 2. Get the specific Y surface height from our override
        float surfaceY = GetHexTopSurfaceY(coords);

        // 3. Return the corrected vector
        return new Vector3(basePos.x, surfaceY, basePos.z);
    }

    public override float GetHexTopSurfaceY(Vector2Int coords)
    {
        float hexThickness = visualizer.hexVisualHeight;

        // Get the base world position Y
        float baseHexHeight = GetHexWorldPosition(coords, GetHexData(coords).Height).y;

        // Return the top of the mesh
        return baseHexHeight + (hexThickness * entityPlacementHeightOffset);
    }

    public bool TryGetHexCoordsFromWorld(Vector3 worldPos, out Vector2Int coords)
    {
        // 1. Convert World XZ to Grid Axial Q, R coordinates
        // This is the inverse of your GetHexWorldPosition math
        float q = (2.0f / 3.0f * worldPos.x) / hexSize;
        float r = (-1.0f / 3.0f * worldPos.x + Mathf.Sqrt(3.0f) / 3.0f * worldPos.z) / hexSize;

        // 2. Round axial coordinates to get the nearest valid hex
        coords = RoundAxial(q, r);

        // 3. Check if that coordinate actually exists in our grid
        return HexagonsInGrid.ContainsKey(coords);
    }

    private Vector2Int RoundAxial(float q, float r)
    {
        float x = q;
        float z = r;
        float y = -x - z;

        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);

        float xDiff = Mathf.Abs(rx - x);
        float yDiff = Mathf.Abs(ry - y);
        float zDiff = Mathf.Abs(rz - z);

        if (xDiff > yDiff && xDiff > zDiff) rx = -ry - rz;
        else if (yDiff > zDiff) ry = -rx - rz;
        else rz = -rx - ry;

        return new Vector2Int(rx, rz);
    }

    public bool TryGetHexFromRay(Ray ray, out HexData foundData)
    {
        foundData = default;
    
        // We step along the ray in short increments
        float step = 0.5f; 
        for (float distance = 0; distance < 200f; distance += step)
        {
            Vector3 pos = ray.GetPoint(distance);
        
            Vector2Int coords;
            if (TryGetHexCoordsFromWorld(pos, out coords))
            {
                if (HexagonsInGrid.TryGetValue(coords, out HexData data))
                {
                    // CHANGE: Use your existing TopSurfaceY method 
                    // This accounts for the baseHeight + thickness + offset
                    float surfaceHeight = GetHexTopSurfaceY(coords);
                
                    // If the ray's Y is close to the surface height, we hit it!
                    // Using a small buffer (e.g., 2.0f) helps because rays 
                    // rarely land EXACTLY on the surface.
                    if (Mathf.Abs(pos.y - surfaceHeight) < 0.1f)
                    {
                        foundData = data;
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    private void GeneratePhysicsProxy()
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();

        // 1. Build Vertices
        foreach (var kvp in HexagonsInGrid)
        {
            Vector3 worldPos = GetHexWorldPosition(kvp.Key, kvp.Value.Height);
            coordToIndex[kvp.Key] = vertices.Count;
            vertices.Add(worldPos);
        }

        // 2. Build Triangles
        foreach (var kvp in HexagonsInGrid)
        {
            Vector2Int current = kvp.Key;
            List<Vector2Int> neighbors = GetHexNeighbors(current);

            // Connect this center to neighbors to form triangles
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int next = neighbors[(i + 1) % neighbors.Count];
            
                if (coordToIndex.ContainsKey(neighbors[i]) && coordToIndex.ContainsKey(next))
                {
                    triangles.Add(coordToIndex[current]);
                    triangles.Add(coordToIndex[neighbors[i]]);
                    triangles.Add(coordToIndex[next]);
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        // 3. Apply to GameObject
        GameObject physicsObj = new GameObject("PhysicsGround");
        physicsObj.transform.SetParent(this.transform);
        physicsObj.transform.localPosition = new Vector3(0, 1.5f, 0);
        physicsObj.layer = LayerMask.NameToLayer("HexagonCollider");

        MeshCollider col = physicsObj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
    }
}