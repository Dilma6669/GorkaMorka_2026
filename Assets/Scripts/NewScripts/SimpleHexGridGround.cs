using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SimpleHexGridGround : SimpleHexGridBase
{
    TerrainGenerator terrainGenerator;
    HexGridVisualizerGround visualizer;
    private WorldPopulator populator;

    public float baseHeight = 0f;

    public float entityPlacementHeightOffset = 0.05f;

    [Header("Physics Settings")] public int chunkSize = 10; // Number of hexes per side per chunk
    private Dictionary<Vector2Int, GameObject> physicsChunks = new Dictionary<Vector2Int, GameObject>();

    private new void Awake()
    {
        base.Awake();

        populator = GetComponent<WorldPopulator>();
        visualizer = GetComponent<HexGridVisualizerGround>();
        terrainGenerator = GetComponent<TerrainGenerator>();
    }
    
    void Update()
    {
        if (physicsChunks == null) return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        foreach (var kvp in physicsChunks)
        {
            ChunkDataComponent data = kvp.Value.GetComponent<ChunkDataComponent>();
            Bounds bounds = new Bounds(data.worldCenter, Vector3.one * 50f); 

            // 1. Calculate current visibility
            bool isVisible = GeometryUtility.TestPlanesAABB(planes, bounds);

            // 2. Check if the state has changed
            if (isVisible != data.visible)
            {
                WorldPopulator.SetVisibilityOfObjectsInChunk(kvp.Key, isVisible);

                // 3. Update the state
                data.visible = isVisible;
            }
        }
    }

    public override void GenerateGrid()
    {
        Debug.Log($"Generating grid: {gameObject.name} | Radius: {gridRadius}");
        
        if (HexagonsInGrid == null) HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        HexagonsInGrid.Clear();

        int count = 0;
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Abs(q + r) <= gridRadius)
                {
                    Vector2Int coords = new Vector2Int(q, r);

                    // Calculate the offsets exactly like the old script did
                    float offsetX = (terrainGenerator.seed * terrainGenerator.seedOffsetMultiplier) + 10000f;
                    float offsetY = (terrainGenerator.seed * terrainGenerator.seedOffsetMultiplier) + 20000f;

                    // CALL THE GENERATOR:
                    // We pass the coords + offsets to the generator, just like the old controller
                    bool isRocky;
                    float finalHeight = terrainGenerator.GenerateDesertHeight(coords.x + offsetX, coords.y + offsetY, out isRocky);

                    // Create the hex data
                    HexData hex = new HexData(coords, finalHeight + baseHeight, true, true, false);
                    hex.isRocky = isRocky; // Store the rock status returned by the generator

                    HexagonsInGrid[coords] = hex;
                }
            }
        }

        Debug.Log($"Grid Generation Complete. Processed {count} hexes.");

        GeneratePhysicsProxy();
        RegisterGridToSystem(true);

        AllNodes.Clear();
        foreach (var kvp in HexagonsInGrid)
        {
            AllNodes[kvp.Key] = new PathNode(kvp.Key, this);
        }
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
        foreach (var hex in HexagonsInGrid.Values)
        {
            Vector2Int chunkID = GetChunkID(hex.GridCoordinates);
            if (!chunkData.ContainsKey(chunkID)) chunkData[chunkID] = new List<HexData>();
            chunkData[chunkID].Add(hex);
        }

        foreach (var kvp in chunkData)
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
    
    public Vector2Int GetChunkID(Vector2Int gridCoords)
    {
        return new Vector2Int(
            Mathf.FloorToInt((float)gridCoords.x / chunkSize),
            Mathf.FloorToInt((float)gridCoords.y / chunkSize)
        );
    }
}