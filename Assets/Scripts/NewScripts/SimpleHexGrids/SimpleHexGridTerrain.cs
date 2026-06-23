using UnityEngine;
using System.Collections.Generic;

public class SimpleHexGridTerrain : SimpleHexGridBase
{
    private HexGeneratorTerrain _hexGeneratorTerrain;
    private HexGridVisualizerTerrain visualizer; // Made public for access
    private PopulaterTerrain _populaterTerrain;
    private WaterController waterController;
    
    public MapSettingsTerrain TerrainSettings => activeMapSettingsBase as MapSettingsTerrain;

    public float baseHeight = 0f;
    public float entityPlacementHeightOffset = 0.05f;

    [Header("Physics Settings")] 
    public int chunkSize = 10;
    public Dictionary<Vector2Int, ChunkDataComponent> physicsChunks = new Dictionary<Vector2Int, ChunkDataComponent>();
    
    [Tooltip("The arc spread distance from the camera to show terrain meshes.")]
    public float meshVisualRadius = 1000f; 
    [Tooltip("The circular spread around the camera to show terrain meshes.")]
    public float cameraMeshSpreadRadius = 100f; 
    [Tooltip("The circular spread around the camera to enable mesh colliders")]
    public float colliderSpreadRadius = 100f;
    [Tooltip("The circular spread around the camera to enable mesh colliders")]
    
    // Stores matrices grouped by the exact same chunkID as the physics system
    public Dictionary<Vector2Int, Matrix4x4[]> chunkVisualData = new Dictionary<Vector2Int, Matrix4x4[]>();
    public Dictionary<Vector2Int, Bounds> chunkBounds = new Dictionary<Vector2Int, Bounds>();

    // Throttle the physics update to every 15 frames for maximum efficiency
    private Vector3 lastUpdatePosition;
    private Quaternion lastUpdateRotation;
    [SerializeField] private float moveThreshold = 5.0f; // How far to move
    [SerializeField] private float rotationThreshold = 5.0f; // How many degrees to rotate
    
    private Camera camera;
    
    private new void Awake()
    {
        base.Awake();
        _populaterTerrain = GetComponent<PopulaterTerrain>();
        visualizer = GetComponent<HexGridVisualizerTerrain>();
        _hexGeneratorTerrain = GetComponent<HexGeneratorTerrain>();
        waterController = GetComponent<WaterController>();
        camera = Camera.main;

        activeMapSettingsBase = activeMapSettingsBase as MapSettingsTerrain;
    }
    

    void Update()
    {
        if (physicsChunks == null) return;

        // Check if position moved enough OR rotation changed enough
        float distanceMoved = Vector3.Distance(camera.transform.position, lastUpdatePosition);
        float degreesRotated = Quaternion.Angle(camera.transform.rotation, lastUpdateRotation);

        if (distanceMoved > moveThreshold || degreesRotated > rotationThreshold)
        {
            Vector3 camPos = camera.transform.position;

            UpdateMeshVisibility(camPos);
            UpdateColliderVisibility(camPos);
            UpdateWaterVisibility(camPos);

            // Update our "last known" states
            lastUpdatePosition = camera.transform.position;
            lastUpdateRotation = camera.transform.rotation;
        }
    }

    public override void GenerateGrid()
    {
        if (HexagonsInGrid == null) HexagonsInGrid = new Dictionary<Vector2Int, HexData>();
        HexagonsInGrid.Clear();

        for (int q = -activeMapSettingsBase.gridRadius; q <= activeMapSettingsBase.gridRadius; q++)
        {
            for (int r = -activeMapSettingsBase.gridRadius; r <= activeMapSettingsBase.gridRadius; r++)
            {
                if (Mathf.Abs(q + r) <= activeMapSettingsBase.gridRadius)
                {
                    Vector2Int coords = new Vector2Int(q, r);
            
                    // PASS RAW COORDINATES. Do not add 10000f here.
                    float finalHeight = _hexGeneratorTerrain.GenerateHeight(coords.x, coords.y);
            
                    HexData hex = new HexData(coords, finalHeight + baseHeight)
                    {
                        IsClimbable = true
                    };
            
                    HexagonsInGrid[coords] = hex;
                }
            }
        }

        GeneratePhysicsProxy();
        visualizer.GenerateVisualGrid(this);
        RegisterGridToSystem(true);
        _populaterTerrain.PopulateWorld(_hexGeneratorTerrain.seed);
        
        AllNodes.Clear();
        foreach (var kvp in HexagonsInGrid)
        {
            AllNodes[kvp.Key] = new PathNode(kvp.Key, this);
        }
        
        // 2. ADD THE REGISTRATION LOOP HERE
        foreach (var hex in HexagonsInGrid.Values)
        {
            if (hex.IsPortal) 
            {
                // If the portal doesn't have a seed yet, assign one and register it
                int portalSeed = Random.Range(0, 999999); 
                LevelPortalManager.Instance.RegisterPortal(this, hex.GridCoordinates, portalSeed);
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
        float q = (2.0f / 3.0f * worldPos.x) / activeMapSettingsBase.hexSize;
        float r = (-1.0f / 3.0f * worldPos.x + Mathf.Sqrt(3.0f) / 3.0f * worldPos.z) / activeMapSettingsBase.hexSize;

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

    public override bool TryGetHexFromRay(Ray ray, out HexData foundData, float maxDistance)
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
            Mesh mesh = BuildMeshForChunk(kvp.Value, activeMapSettingsBase.worldLevel);
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
                float scaleFactor = activeMapSettingsBase.hexSize * 2f;
                Vector3 scale = new Vector3(scaleFactor, visualizer.hexVisualHeight, scaleFactor);

                foreach(var hex in kvp.Value)
                {
                    Vector3 pos = GetHexWorldPosition(hex.GridCoordinates, hex.Height);
                    sum += pos;
                    matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                    
                    //Debug.Log($"Hex Height: {hex.Height}");
                }
                
                
                
                data.worldCenter = sum / kvp.Value.Count;
                chunkVisualData[kvp.Key] = matrices.ToArray();
                
                // Store bounds for frustum testing
                chunkBounds[kvp.Key] = new Bounds(data.worldCenter, Vector3.one * 50f);
                
                physicsChunks[kvp.Key] = data;
                col.enabled = false;

                if (TerrainSettings.waterLevel > 0)
                {
                    waterController.CreateWaterForChunk(kvp.Key, kvp.Value, chunkObj.transform);
                }

            }
        }
    }

    private Mesh BuildMeshForChunk(List<HexData> chunkHexes, int worldLayer = 0)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();

        // 1. Identify all hexes needed: current chunk + immediate neighbors
        HashSet<Vector2Int> allRequiredCoords = new HashSet<Vector2Int>();
        foreach (var hex in chunkHexes)
        {
            allRequiredCoords.Add(hex.GridCoordinates);
            foreach (var neighbor in GetHexNeighbors(hex.GridCoordinates))
                allRequiredCoords.Add(neighbor);
        }

        // 2. Add all these to our dictionary FIRST
        foreach (var coords in allRequiredCoords)
        {
            if (HexagonsInGrid.TryGetValue(coords, out HexData hex))
            {
                coordToIndex[coords] = vertices.Count;
                vertices.Add(new Vector3(
                    GetHexWorldPosition(hex.GridCoordinates, hex.Height).x,
                    GetHexTopSurfaceY(hex.GridCoordinates),
                    GetHexWorldPosition(hex.GridCoordinates, hex.Height).z
                ));
            }
        }

        // 3. Now build triangles for the current chunk ONLY
        foreach (var hex in chunkHexes)
        {
            List<Vector2Int> neighbors = GetHexNeighbors(hex.GridCoordinates);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int neighborA = neighbors[i];
                Vector2Int neighborB = neighbors[(i + 1) % neighbors.Count];

                // Because all neighbors are now in the dictionary, 
                // these triangles will bridge the gap perfectly.
                if (coordToIndex.ContainsKey(hex.GridCoordinates) && 
                    coordToIndex.ContainsKey(neighborA) && 
                    coordToIndex.ContainsKey(neighborB))
                {
                    triangles.Add(coordToIndex[hex.GridCoordinates]);
                    triangles.Add(coordToIndex[neighborA]);
                    triangles.Add(coordToIndex[neighborB]);
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
        
    public void UpdateMeshVisibility(Vector3 cameraPosition)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var kvp in physicsChunks)
        {
            float dist = Vector3.Distance(cameraPosition, kvp.Value.worldCenter);

            // 1. Logic State: Should objects be active?
            bool isLoaded = dist < meshVisualRadius;

            // 2. Visual State: Should the mesh be rendering?
            // (Must be in logical radius AND within camera view OR very close)
            bool isVisible = isLoaded && (dist < cameraMeshSpreadRadius ||
                                          GeometryUtility.TestPlanesAABB(planes,
                                              new Bounds(kvp.Value.worldCenter, Vector3.one * cameraMeshSpreadRadius)));

            // --- UPDATE MESH/VISUALIZER ---
            if (isVisible != kvp.Value.visible)
            {
                kvp.Value.visible = isVisible;
    
                visualizer.SetChunkVisibility(kvp.Key, isVisible);
            }
        }
    }

    // LEAVE THIS
    public void UpdateColliderVisibility(Vector3 cameraPosition)
    {
        Vector2 camPos2D = new Vector2(cameraPosition.x, cameraPosition.z);

        foreach (var chunk in physicsChunks)
        {
            if (chunk.Value == null) continue;

            Vector2 chunkPos2D = new Vector2(chunk.Value.worldCenter.x, chunk.Value.worldCenter.z);
            float dist2D = Vector2.Distance(camPos2D, chunkPos2D);

            // A chunk should be active ONLY IF it is close enough AND visible to the camera
            bool targetVisibility = (dist2D <= colliderSpreadRadius && chunk.Value.visible);

            MeshCollider col = chunk.Value.GetComponent<MeshCollider>();
            if (col != null)
            {
                // 1. Update the Mesh
                if (col.enabled != targetVisibility)
                {
                    col.enabled = targetVisibility;
                    _populaterTerrain.SetVisibilityOfObjectsInChunk(chunk.Key, targetVisibility);
                    waterController.SetVisibilityOfWaterInChunk(chunk.Key, targetVisibility);
                }
            }
        }
    }
    
    public void UpdateWaterVisibility(Vector3 cameraPosition)
    {
        Vector2 camPos2D = new Vector3(cameraPosition.x, cameraPosition.z);

        foreach (var chunk in physicsChunks)
        {
            Vector2 chunkPos2D = new Vector2(chunk.Value.worldCenter.x, chunk.Value.worldCenter.z);
            float dist2D = Vector2.Distance(camPos2D, chunkPos2D);

            // Visibility logic
            bool targetVisibility = (dist2D <= colliderSpreadRadius && chunk.Value.visible);

            // Toggle the water plane specifically
            waterController.SetVisibilityOfWaterInChunk(chunk.Key, targetVisibility);
        }
    }
    
    public override Vector2Int GetChunkID(Vector2Int gridCoords)
    {
        return new Vector2Int(
            Mathf.FloorToInt((float)gridCoords.x / chunkSize),
            Mathf.FloorToInt((float)gridCoords.y / chunkSize)
        );
    }
    
    public override void SetSeed(int newSeed)
    {
        // Ensure your generator updates its internal seed value
        _hexGeneratorTerrain.seed = newSeed;
    }
    
    public override void ResetGrid()
    {
        // 1. Clear logic
        HexagonsInGrid.Clear();
        
        chunkVisualData.Clear(); 
        chunkBounds.Clear();
        physicsChunks.Clear(); // If you have physics
    
        visualizer.Clear();
        
        // 3. Clear existing objects
        _populaterTerrain.ClearAll();
    }
}