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

    [Header("Physics Settings")]
    public int chunkSize = 10; // Number of hexes per side per chunk
    private Dictionary<Vector2Int, GameObject> physicsChunks = new Dictionary<Vector2Int, GameObject>();
    
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
        
        foreach (var chunk in physicsChunks)
        {
            if (chunk.Value == null) continue;
            MeshCollider col = chunk.Value.GetComponent<MeshCollider>();
            col.enabled = false;
        }

        ApplySmoothingPass();
        
        GeneratePhysicsProxy();

        RegisterGridToSystem(true);
        
        AllNodes.Clear();
        foreach (var kvp in HexagonsInGrid)
        {
            AllNodes[kvp.Key] = new PathNode(kvp.Key, this);
        }
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

    public bool TryGetHexFromRay(Ray ray, out HexData foundData, float maxDistance) // 1. Add distance limit
    {
        foundData = default;
        float step = 0.5f; 
    
        // 2. Use the maxDistance parameter instead of hardcoded 200f
        for (float distance = 0; distance < maxDistance; distance += step) 
        {
            Vector3 pos = ray.GetPoint(distance);
        
            Vector2Int coords;
            if (TryGetHexCoordsFromWorld(pos, out coords))
            {
                if (HexagonsInGrid.TryGetValue(coords, out HexData data))
                {
                    float surfaceHeight = GetHexTopSurfaceY(coords);
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
        // Clean up old chunks if regenerating
        foreach (var chunk in physicsChunks.Values) Destroy(chunk);
        physicsChunks.Clear();

        // Group all hexes by their chunk ID
        Dictionary<Vector2Int, List<HexData>> chunkData = new Dictionary<Vector2Int, List<HexData>>();
        foreach(var hex in HexagonsInGrid.Values)
        {
            Vector2Int chunkID = new Vector2Int(hex.GridCoordinates.x / chunkSize, hex.GridCoordinates.y / chunkSize);
            if(!chunkData.ContainsKey(chunkID)) chunkData[chunkID] = new List<HexData>();
            chunkData[chunkID].Add(hex);
        }

        // Build a separate MeshCollider for every group
        foreach(var kvp in chunkData)
        {
            // 1. Generate the mesh
            Mesh mesh = BuildMeshForChunk(kvp.Value); 

            // 2. ONLY add the collider if the mesh is valid
            if (mesh.vertexCount >= 3)
            {
                GameObject chunkObj = new GameObject($"PhysicsChunk_{kvp.Key.x}_{kvp.Key.y}");
                chunkObj.transform.SetParent(HexagonsContainer.transform);
                chunkObj.transform.localPosition = Vector3.zero;
                chunkObj.layer = LayerMask.NameToLayer("HexagonCollider");
                
                MeshCollider col = chunkObj.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
    
                // Calculate and store the centroid for this specific chunk
                Vector3 sum = Vector3.zero;
                foreach(var hex in kvp.Value)
                {
                    sum += GetHexWorldPosition(hex.GridCoordinates, hex.Height);
                }
                Vector3 chunkWorldCenter = sum / kvp.Value.Count;
    
                // Store this position in a way we can access later. 
                // We can add a simple script to the chunk, or use a Dictionary to map the GameObject to its position.
                chunkObj.AddComponent<ChunkDataComponent>().worldCenter = chunkWorldCenter;
            
                physicsChunks[kvp.Key] = chunkObj;
            }
            else
            {
                Debug.LogWarning($"Skipping chunk {kvp.Key} due to insufficient vertices ({mesh.vertexCount}).");
            }
        }
    }
    
    private Mesh BuildMeshForChunk(List<HexData> chunkHexes)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();

        foreach (var hex in chunkHexes)
        {
            // 1. Get the base world position
            Vector3 basePos = GetHexWorldPosition(hex.GridCoordinates, hex.Height);
        
            // 2. Use the method you already wrote to get the top surface Y
            float surfaceY = GetHexTopSurfaceY(hex.GridCoordinates);
        
            // 3. Create a vertex at the top surface
            Vector3 topPos = new Vector3(basePos.x, surfaceY, basePos.z);
        
            coordToIndex[hex.GridCoordinates] = vertices.Count;
            vertices.Add(topPos);
        }
        
        // 2. Build local triangles
        foreach (var hex in chunkHexes)
        {
            List<Vector2Int> neighbors = GetHexNeighbors(hex.GridCoordinates);

            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int next = neighbors[(i + 1) % neighbors.Count];
            
                // Only add triangle if the neighbor is ALSO in this chunk
                // (This prevents seams from showing or gaps between chunks)
                if (coordToIndex.ContainsKey(neighbors[i]) && coordToIndex.ContainsKey(next))
                {
                    triangles.Add(coordToIndex[hex.GridCoordinates]);
                    triangles.Add(coordToIndex[neighbors[i]]);
                    triangles.Add(coordToIndex[next]);
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
    
    public void UpdateChunkVisibility(Vector3 cameraPosition, float activeDistance)
    {
        foreach (var chunk in physicsChunks)
        {
            ChunkDataComponent data = chunk.Value.GetComponent<ChunkDataComponent>();
            if (data == null) continue;

            // Use the cached worldCenter instead of transform.position
            float dist = Vector3.Distance(cameraPosition, data.worldCenter);
        
            MeshCollider col = chunk.Value.GetComponent<MeshCollider>();
            if (col != null)
            {
                bool shouldBeActive = dist <= activeDistance;
                
                if (col.enabled != shouldBeActive)
                {
                    col.enabled = shouldBeActive;
                }
            }
        }
    }
}