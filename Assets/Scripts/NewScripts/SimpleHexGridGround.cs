using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

public class SimpleHexGridGround : SimpleHexGridBase
{
    TerrainGenerator terrainGenerator;
    public HexGridVisualizerGround visualizer; // Made public for access
    private WorldPopulator worldPopulator;

    public float baseHeight = 0f;
    public float entityPlacementHeightOffset = 0.05f;

    [Header("Physics Settings")] public int chunkSize = 10;
    private Dictionary<Vector2Int, GameObject> physicsChunks = new Dictionary<Vector2Int, GameObject>();
    
    // Set these to whatever you want
    public float terrainVisualRadius = 1000f; 
    public float cameraTerrainSpreadRadius = 150f; 
    public float meshSpreadRadius = 100f; // Adjust this if chunks look like they are cutting off too early
    // Stores matrices grouped by the exact same chunkID as the physics system
    public Dictionary<Vector2Int, Matrix4x4[]> chunkVisualData = new Dictionary<Vector2Int, Matrix4x4[]>();
    public Dictionary<Vector2Int, Bounds> chunkBounds = new Dictionary<Vector2Int, Bounds>();

    // Throttle the physics update to every 15 frames for maximum efficiency
    private Vector3 lastUpdatePosition;
    private float updateThreshold = 5.0f; // Only update if camera moved 5 meters
    
    private Camera camera;
    
    private new void Awake()
    {
        base.Awake();
        worldPopulator = GetComponent<WorldPopulator>();
        visualizer = GetComponent<HexGridVisualizerGround>();
        terrainGenerator = GetComponent<TerrainGenerator>();
        camera = Camera.main;
    }
    

    void Update()
    {
        if (physicsChunks == null) return;

        Vector3 camPos = camera.transform.position;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var kvp in physicsChunks)
        {
            ChunkDataComponent data = kvp.Value.GetComponent<ChunkDataComponent>();
            float dist = Vector3.Distance(camPos, data.worldCenter);

            // 1. Logic State: Should objects be active?
            bool isLoaded = dist < terrainVisualRadius;

            // 2. Visual State: Should the mesh be rendering?
            // (Must be in logical radius AND within camera view OR very close)
            bool isVisible = isLoaded && (dist < cameraTerrainSpreadRadius ||
                                          GeometryUtility.TestPlanesAABB(planes,
                                              new Bounds(data.worldCenter, Vector3.one * meshSpreadRadius)));

            // --- UPDATE MESH/VISUALIZER ---
            if (isVisible != data.visible)
            {
                data.visible = isVisible;
    
                // Update Collider
                MeshCollider col = kvp.Value.GetComponent<MeshCollider>();
                if (col != null) 
                {
                    col.enabled = isVisible;
        
                    // SYNC OBJECTS HERE: The moment the collider changes, update the objects
                    worldPopulator.SetVisibilityOfObjectsInChunk(kvp.Key, isVisible);
                }
    
                visualizer.SetChunkVisibility(kvp.Key, isVisible);
            }
        }

        // Check if camera moved enough to warrant an update
        if (Vector3.Distance(camera.transform.position, lastUpdatePosition) > updateThreshold)
        {
            UpdateChunkVisibility(camera.transform.position, MultiGridPathfinder.MaxRaycastPathDistance + 20f);
            lastUpdatePosition = camera.transform.position;
        }

    }

    public override void GenerateGrid()
    {
        if (HexagonsInGrid == null) HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        HexagonsInGrid.Clear();

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Abs(q + r) <= gridRadius)
                {
                    Vector2Int coords = new Vector2Int(q, r);
                    float offsetX = (terrainGenerator.seed * terrainGenerator.seedOffsetMultiplier) + 10000f;
                    float offsetY = (terrainGenerator.seed * terrainGenerator.seedOffsetMultiplier) + 20000f;
                    bool isRocky;
                    float finalHeight = terrainGenerator.GenerateDesertHeight(coords.x + offsetX, coords.y + offsetY, out isRocky);
                    HexData hex = new HexData(coords, finalHeight + baseHeight, true, true, false);
                    hex.isRocky = isRocky;
                    HexagonsInGrid[coords] = hex;
                }
            }
        }

        GeneratePhysicsProxy();
        visualizer.GenerateVisualGrid(this);
        RegisterGridToSystem(true);
        worldPopulator.PopulateWorld(terrainGenerator.seed);
        
        AllNodes.Clear();
        foreach (var kvp in HexagonsInGrid)
        {
            AllNodes[kvp.Key] = new PathNode(kvp.Key, this);
        }
        
        // Need to set the initial worldObjects to active in the beginning meshes.
        StartCoroutine(SyncInitialState());
    }
    
    private IEnumerator SyncInitialState()
    {
        // Wait for the end of the frame so the camera has initialized
        yield return new WaitForEndOfFrame();

        foreach (var chunk in physicsChunks)
        {
            ChunkDataComponent data = chunk.Value.GetComponent<ChunkDataComponent>();
            if (data == null) continue;
    
            MeshCollider col = chunk.Value.GetComponent<MeshCollider>();
            if (col != null)
            {
                // Now the radius is a perfect vertical cylinder, not a sphere
                bool shouldBeActive = data.visible;
    
                if (shouldBeActive)
                {
                    worldPopulator.SetVisibilityOfObjectsInChunk(chunk.Key, shouldBeActive);
                }
            }
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
        foreach (var chunk in physicsChunks.Values) Destroy(chunk);
        physicsChunks.Clear();
        chunkVisualData.Clear();
        chunkBounds.Clear();

        Dictionary<Vector2Int, List<HexData>> chunkData = new Dictionary<Vector2Int, List<HexData>>();
        foreach (var hex in HexagonsInGrid.Values)
        {
            Vector2Int chunkID = GetChunkID(hex.GridCoordinates);
            if (!chunkData.ContainsKey(chunkID)) chunkData[chunkID] = new List<HexData>();
            chunkData[chunkID].Add(hex);
        }

        foreach (var kvp in chunkData)
        {
            Mesh mesh = BuildMeshForChunk(kvp.Value);
            if (mesh.vertexCount >= 3)
            {
                GameObject chunkObj = new GameObject($"PhysicsChunk_{kvp.Key.x}_{kvp.Key.y}");
                chunkObj.transform.SetParent(HexagonsContainer.transform);
                chunkObj.transform.localPosition = Vector3.zero;
                chunkObj.layer = LayerMask.NameToLayer("HexagonCollider");
                
                MeshCollider col = chunkObj.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
    
                ChunkDataComponent data = chunkObj.AddComponent<ChunkDataComponent>();
                data.chunkID = kvp.Key;
                
                Vector3 sum = Vector3.zero;
                List<Matrix4x4> matrices = new List<Matrix4x4>();
                float scaleFactor = hexSize * 2f;
                Vector3 scale = new Vector3(scaleFactor, visualizer.hexVisualHeight, scaleFactor);

                foreach(var hex in kvp.Value)
                {
                    Vector3 pos = GetHexWorldPosition(hex.GridCoordinates, hex.Height);
                    sum += pos;
                    matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                }
                
                data.worldCenter = sum / kvp.Value.Count;
                chunkVisualData[kvp.Key] = matrices.ToArray();
                
                // Store bounds for frustum testing
                chunkBounds[kvp.Key] = new Bounds(data.worldCenter, Vector3.one * 50f);
                
                physicsChunks[kvp.Key] = chunkObj;
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
            Vector3 basePos = GetHexWorldPosition(hex.GridCoordinates, hex.Height);
            float surfaceY = GetHexTopSurfaceY(hex.GridCoordinates);

            // 3. Create a vertex at the top surface
            Vector3 topPos = new Vector3(basePos.x, surfaceY, basePos.z);

            coordToIndex[hex.GridCoordinates] = vertices.Count;
            vertices.Add(topPos);
        }

        foreach (var hex in chunkHexes)
        {
            List<Vector2Int> neighbors = GetHexNeighbors(hex.GridCoordinates);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int next = neighbors[(i + 1) % neighbors.Count];
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
        Vector2 camPos2D = new Vector2(cameraPosition.x, cameraPosition.z);

        foreach (var chunk in physicsChunks)
        {
            ChunkDataComponent data = chunk.Value.GetComponent<ChunkDataComponent>();
            if (data == null) continue;

            Vector2 chunkPos2D = new Vector2(data.worldCenter.x, data.worldCenter.z);
            float dist2D = Vector2.Distance(camPos2D, chunkPos2D);

            // A chunk should be active ONLY IF it is close enough AND visible to the camera
            bool targetVisibility = (dist2D <= activeDistance);

            MeshCollider col = chunk.Value.GetComponent<MeshCollider>();
            if (col != null)
            {
                // 1. Update the Mesh
                if (col.enabled != targetVisibility)
                {
                    col.enabled = targetVisibility;
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