using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for .Any() in VehiclePathFinder, good to have here too

public class HexagonController : MonoBehaviour 
{
    [Header("Grid Settings")]
    public int gridWidth = 100;
    public int gridHeight = 100;
    public float hexSize = 1f;
    [Tooltip("Adjusts the vertical height of the hexagon mesh. Increase to make hexagons appear taller.")]
    public float hexMeshHeight = 1.0f; 
    
    [Header("Desert Height/Noise Settings")]
    [Tooltip("Enter a value here to get a different terrain layout. Changing this value will generate a new environment.")]
    public int seed = 0; // The noise seed
    public float baseHeight = 0f;
    
    [Tooltip("A large multiplier for the seed to create distinct noise patterns. Increase for more drastic changes between seeds.")]
    public float seedOffsetMultiplier = 1000f; 
    
    [Header("Sand Dunes (Smooth Hills)")]
    public float duneScale = 0.05f;         // Large scale for rolling hills
    public float duneHeight = 8f;          // Maximum dune height
    public float duneOctaves = 3;          // Number of noise layers for dunes
    public float dunePersistence = 0.5f;   // How much each octave contributes
    
    [Header("Rocky Outcroppings (Sharp Peaks)")]
    public float rockScale = 0.08f;        // Larger scale for bigger rock clusters
    public float rockHeight = 15f;         // Maximum rock height
    public float rockThreshold = 0.6f;     // Only heights above this become rocks
    public float rockSharpness = 2f;       // How sharp the rock transitions are
    
    [Header("Noise Mixing")]
    public float terrainSmoothness = 0.8f; // 0 = all rocks, 1 = all dunes
    public float neighborHeightLimit = 0.8f; // How much height difference is allowed between neighbors
    
    [Header("Optimization")]
    public int chunkSize = 20; // Size of each mesh chunk for rendering (if not instanced)
    public Material sandMaterial;  // Material for sand dunes
    public Material rockMaterial;  // Material for rocky areas
    public bool useInstancedRendering = true;
    
    [Header("LOD Settings")]
    public bool useLOD = true;
    public float lodDistance1 = 50f;
    public float lodDistance2 = 100f;

    [Header("Collider Settings")]
    [Tooltip("The layer to assign to the generated hexagon colliders.")]
    public LayerMask colliderLayer; 
    [Tooltip("Size of the square chunks for collider generation. Larger values reduce collider count but make chunks less precise.")]
    public int colliderChunkSize = 10; // New: Size for collider chunks
    
    private HexData[,] hexGrid;
    private List<GameObject> meshChunks = new List<GameObject>();
    private List<GameObject> hexColliders = new List<GameObject>(); 
    private Camera playerCamera;
    
    // --- REMOVED: The fixed hexDirections array is no longer a public property ---
    // Instead, GetHexDirectionsForColumn will provide the correct array based on parity.
    // private Vector2Int[] hexDirections = new Vector2Int[] { ... };
    // public Vector2Int[] HexDirections => hexDirections;

    // --- NEW: Define hex directions for even columns ---
    private static readonly Vector2Int[] evenColumnHexDirections = new Vector2Int[]
    {
        new Vector2Int(0, 1),     // 0: Top (North)
        new Vector2Int(1, 0),     // 1: Top Right (East)
        new Vector2Int(1, -1),    // 2: Bottom Right (South-East)
        new Vector2Int(0, -1),    // 3: Bottom (South)
        new Vector2Int(-1, -1),   // 4: Bottom Left (South-West)
        new Vector2Int(-1, 0)     // 5: Top Left (North-West)
    };

    // --- NEW: Define hex directions for odd columns ---
    private static readonly Vector2Int[] oddColumnHexDirections = new Vector2Int[]
    {
        new Vector2Int(0, 1),     // 0: Top (North)
        new Vector2Int(1, 1),     // 1: Top Right (East)
        new Vector2Int(1, 0),     // 2: Bottom Right (South-East)
        new Vector2Int(0, -1),    // 3: Bottom (South)
        new Vector2Int(-1, 0),    // 4: Bottom Left (South-West)
        new Vector2Int(-1, 1)     // 5: Top Left (North-West)
    };

    // --- NEW: Public method to get the correct hex directions based on column parity ---
    public Vector2Int[] GetHexDirectionsForColumn(int x)
    {
        if (x % 2 == 0)
        {
            return evenColumnHexDirections;
        }
        else
        {
            return oddColumnHexDirections;
        }
    }


    // Data structure to store hex information without GameObjects
    [System.Serializable]
    public struct HexData 
    {
        public Vector3 position;
        public float height;
        public float rawHeight;  // Height before neighbor limiting
        public int gridX, gridZ;
        public bool isWalkable;
        public bool isRock;      // True if this hex is rocky terrain
        
        public HexData(Vector3 pos, float h, float rawH, int x, int z, bool rock) 
        {
            position = pos;
            height = h;
            rawHeight = rawH;
            gridX = x;
            gridZ = z;
            isWalkable = true;
            isRock = rock;
        }
    }
    
    void Start() 
    {
        playerCamera = Camera.main;
        
        Debug.Log($"Generating hex grid: {gridWidth}x{gridHeight}");
        Debug.Log($"Using instanced rendering: {useInstancedRendering}");
        
        GenerateOptimizedGrid();
        
        Debug.Log($"Grid generation complete. Created {meshChunks.Count} chunks.");
    }

    // Call this method to regenerate the grid during runtime when the seed changes
    public void RegenerateGrid()
    {
        ClearMeshChunks(); // Clear existing chunks before regenerating
        ClearHexColliders(); // Clear existing colliders before regenerating

        if (useInstancedRendering)
        {
            // Destroy existing instanced renderers if they exist
            InstancedHexRenderer[] existingRenderers = GetComponentsInChildren<InstancedHexRenderer>();
            foreach (var renderer in existingRenderers)
            {
                Destroy(renderer.gameObject);
            }
        }
        GenerateOptimizedGrid();
    }
    
    void GenerateOptimizedGrid() 
    {
        // First, generate all hex data
        GenerateHexData();
        
        // Check if we have materials
        if (sandMaterial == null || rockMaterial == null) 
        {
            Debug.LogError("Please assign both sand and rock materials in the inspector!");
            return;
        }
        
        if (useInstancedRendering) 
        {
            SetupInstancedRendering();
            CreateHexagonColliders(); // Create optimized colliders for instanced rendering
        }
        else 
        {
            // Generate mesh chunks (which will have colliders if added in CreateMaterialChunk)
            GenerateMeshChunks();
        }
    }
    
    void GenerateHexData() 
    {
        hexGrid = new HexData[gridWidth, gridHeight];
        
        // Calculate a large, unique offset based on the seed
        // This makes sure different seeds sample vastly different parts of the Perlin noise map.
        // Adding a large number to ensure some initial offset even if seed is 0.
        float offsetX = (seed * seedOffsetMultiplier) + 10000f; 
        float offsetY = (seed * seedOffsetMultiplier) + 20000f; // Use a different large offset for Y/Z

        // First pass: Generate raw heights and determine terrain types
        for (int x = 0; x < gridWidth; x++) 
        {
            for (int z = 0; z < gridHeight; z++) 
            {
                Vector3 worldPos = HexToWorldPos(x, z);
                
                // Pass the combined (x + offsetX, z + offsetY) to the noise functions
                float rawHeight = GenerateDesertHeight(x + offsetX, z + offsetY, out bool isRock);
                
                worldPos.y = baseHeight + rawHeight;
                hexGrid[x, z] = new HexData(worldPos, rawHeight, rawHeight, x, z, isRock);
            }
        }
        
        // Second pass: Apply neighbor height limiting
        ApplyNeighborHeightLimit();
    }
    
    void ApplyNeighborHeightLimit() 
    {
        HexData[,] adjustedGrid = new HexData[gridWidth, gridHeight];
        
        for (int x = 0; x < gridWidth; x++) 
        {
            for (int z = 0; z < gridHeight; z++) 
            {
                HexData currentHex = hexGrid[x, z];
                float maxAllowedHeight = GetMaxAllowedHeight(x, z);
                
                // Limit height but keep the terrain type
                float adjustedHeight = Mathf.Min(currentHex.rawHeight, maxAllowedHeight);
                
                Vector3 adjustedPos = currentHex.position;
                adjustedPos.y = baseHeight + adjustedHeight;
                
                adjustedGrid[x, z] = new HexData(adjustedPos, adjustedHeight, currentHex.rawHeight, x, z, currentHex.isRock);
            }
        }
        
        hexGrid = adjustedGrid;
    }
    
    float GetMaxAllowedHeight(int x, int z) 
    {
        float currentRawHeight = hexGrid[x, z].rawHeight;
        float maxNeighborHeight = 0f;
        
        // Check all 6 hexagon neighbors using the consistent hexDirections array
        // Use GetHexDirectionsForColumn to get the correct offsets for this hex's parity
        Vector2Int[] currentHexDirections = GetHexDirectionsForColumn(x);

        foreach (Vector2Int dir in currentHexDirections)
        {
            int neighborX = x + dir.x;
            int neighborZ = z + dir.y; // Note: using 'y' for Z coordinate in Vector2Int for consistency

            if (IsValidGridPosition(neighborX, neighborZ)) 
            {
                float neighborHeight = hexGrid[neighborX, neighborZ].rawHeight;
                maxNeighborHeight = Mathf.Max(maxNeighborHeight, neighborHeight);
            }
        }
        
        // If no valid neighbors were found (e.g., on edge of grid), return current height
        if (maxNeighborHeight == 0f && (x > 0 || x < gridWidth -1 || z > 0 || z < gridHeight -1)) return currentRawHeight;
        
        // Allow some height difference but cap extreme variations
        float heightDifference = currentRawHeight - maxNeighborHeight;
        float maxAllowedDifference = neighborHeightLimit * (rockHeight + duneHeight);
        
        if (heightDifference > maxAllowedDifference) 
        {
            return maxNeighborHeight + maxAllowedDifference;
        }
        
        return currentRawHeight;
    }
    
    // --- MODIFIED: Use the consistent hexDirections array for neighbor lookup ---
    // This method now returns HexData directly, which is more useful for pathfinding.
    public List<HexData> GetHexNeighbors(int x, int z) 
    {
        List<HexData> neighbors = new List<HexData>();
        
        // Hexagon neighbors depend on whether we're on an even or odd row (x-coordinate)
        // The hexDirections array defines the offsets from the current hex.
        // We need to adjust these offsets based on the column parity for axial coordinates.
        
        // Use the new method to get the correct directions for this column
        Vector2Int[] currentColumnHexDirections = GetHexDirectionsForColumn(x);

        foreach (Vector2Int dir in currentColumnHexDirections) 
        {
            int neighborX = x + dir.x;
            int neighborZ = z + dir.y; // Using 'y' for Z-coordinate in Vector2Int

            if (IsValidGridPosition(neighborX, neighborZ)) 
            {
                neighbors.Add(hexGrid[neighborX, neighborZ]);
            }
        }
        return neighbors;
    }
    
    public bool IsValidGridPosition(int x, int z) 
    {
        return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
    }
    
    // The xCoord and zCoord here are already offset by the seed
    float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock) 
    {
        // Generate smooth sand dunes using multiple octaves of Perlin noise
        float duneNoise = GenerateOctaveNoise(xCoord, zCoord, duneScale, duneOctaves, dunePersistence);
        float duneHeight = duneNoise * this.duneHeight;
        
        // Generate rocky outcropping noise
        float rockNoise = Mathf.PerlinNoise(xCoord * rockScale, zCoord * rockScale);
        
        // Create sharp rock transitions - only areas above threshold become rocks
        float rockHeight = 0f;
        isRock = false;
        
        if (rockNoise > rockThreshold) 
        {
            // Sharpen the transition using power function
            float rockIntensity = Mathf.Pow((rockNoise - rockThreshold) / (1f - rockThreshold), rockSharpness);
            rockHeight = rockIntensity * this.rockHeight;
            isRock = true;
        }
        
        // Create a blending mask to determine where rocks vs dunes appear
        // Add another large arbitrary offset to this noise for a different pattern
        float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);
        
        // Blend between dunes and rocks based on terrain smoothness and blend noise
        float blendFactor = Mathf.Lerp(0.3f, 1f, terrainSmoothness);
        float finalBlend = Mathf.Clamp01(blendNoise + blendFactor - 0.5f);
        
        // Final height is a mix of smooth dunes and sharp rocks
        return Mathf.Lerp(duneHeight + rockHeight, duneHeight, finalBlend);
    }
    
    // The xCoord and zCoord here are already offset by the seed
    float GenerateOctaveNoise(float xCoord, float zCoord, float scale, float octaves, float persistence) 
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = scale;
        float maxValue = 0f;
        
        for (int i = 0; i < octaves; i++) 
        {
            value += Mathf.PerlinNoise(xCoord * frequency, zCoord * frequency) * amplitude;
            
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }
        
        return value / maxValue; // Normalize to 0-1 range
    }
    
    // Option 1: GPU Instancing (Best Performance)
    void SetupInstancedRendering() 
    {
        if (sandMaterial == null || rockMaterial == null) return;
        
        // Create separate instanced rendering for sand and rock
        GameObject sandInstancedObj = new GameObject("Instanced Sand Hexes");
        sandInstancedObj.transform.parent = transform;
        
        GameObject rockInstancedObj = new GameObject("Instanced Rock Hexes");
        rockInstancedObj.transform.parent = transform;
        
        InstancedHexRenderer sandRenderer = sandInstancedObj.AddComponent<InstancedHexRenderer>();
        InstancedHexRenderer rockRenderer = rockInstancedObj.AddComponent<InstancedHexRenderer>();
        
        sandRenderer.Initialize(hexGrid, sandMaterial, GetHexMesh(), false); // Sand hexes
        rockRenderer.Initialize(hexGrid, rockMaterial, GetHexMesh(), true);  // Rock hexes
    }

    // New method to create invisible colliders in chunks when using instanced rendering
    void CreateHexagonColliders()
    {
        ClearHexColliders(); // Ensure old colliders are removed
        Mesh individualHexMesh = GetHexMesh(); // Get the base hex mesh once

        int chunksX = Mathf.CeilToInt((float)gridWidth / colliderChunkSize);
        int chunksZ = Mathf.CeilToInt((float)gridHeight / colliderChunkSize);

        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
            {
                List<CombineInstance> combines = new List<CombineInstance>();
                
                int startX = chunkX * colliderChunkSize;
                int startZ = chunkZ * colliderChunkSize;
                int endX = Mathf.Min(startX + colliderChunkSize, gridWidth);
                int endZ = Mathf.Min(startZ + colliderChunkSize, gridHeight);

                for (int x = startX; x < endX; x++)
                {
                    for (int z = startZ; z < endZ; z++)
                    {
                        HexData hexData = hexGrid[x, z];
                        CombineInstance combine = new CombineInstance();
                        combine.mesh = individualHexMesh;
                        // Transform the individual hex mesh to its world position
                        combine.transform = Matrix4x4.TRS(hexData.position, Quaternion.identity, Vector3.one);
                        combines.Add(combine);
                    }
                }

                if (combines.Count > 0)
                {
                    GameObject colliderObj = new GameObject($"HexColliderChunk_{chunkX}_{chunkZ}");
                    colliderObj.transform.parent = transform; // Parent to the HexagonController for organization
                    colliderObj.transform.localPosition = Vector3.zero; // Collider object itself is at origin, meshes are transformed

                    Mesh combinedColliderMesh = new Mesh();
                    combinedColliderMesh.CombineMeshes(combines.ToArray(), true, true); // Merge submeshes, use world space transforms

                    MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = combinedColliderMesh;
                    meshCollider.convex = false; // For static terrain, usually not convex

                    // Assign the specified layer to the collider GameObject
                    colliderObj.layer = (int)Mathf.Log(colliderLayer.value, 2); 

                    hexColliders.Add(colliderObj);
                }
            }
        }
        Debug.Log($"Created {hexColliders.Count} optimized invisible hexagon colliders.");
    }

    void ClearHexColliders()
    {
        foreach (GameObject colliderObj in hexColliders)
        {
            if (colliderObj != null) DestroyImmediate(colliderObj); // Use DestroyImmediate for editor-time cleanup
        }
        hexColliders.Clear();
    }
    
    // Option 2: Mesh Combining (Good Performance) - This path also creates colliders
    void GenerateMeshChunks() 
    {
        ClearMeshChunks();
        
        int chunksX = Mathf.CeilToInt((float)gridWidth / chunkSize);
        int chunksZ = Mathf.CeilToInt((float)gridHeight / chunkSize);
        
        for (int chunkX = 0; chunkX < chunksX; chunkX++) 
        {
            for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++) 
            {
                CreateMeshChunk(chunkX, chunkZ);
            }
        }
    }
    
    void CreateMeshChunk(int chunkX, int chunkZ) 
    {
        // Separate combines for sand and rock
        List<CombineInstance> sandCombines = new List<CombineInstance>();
        List<CombineInstance> rockCombines = new List<CombineInstance>();
        Mesh hexMesh = GetHexMesh();
        
        int startX = chunkX * chunkSize;
        int startZ = chunkZ * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, gridWidth);
        int endZ = Mathf.Min(startZ + chunkSize, gridHeight);
        
        for (int x = startX; x < endX; x++) 
        {
            for (int z = startZ; z < endZ; z++) 
            {
                CombineInstance combine = new CombineInstance();
                combine.mesh = hexMesh;
                combine.transform = Matrix4x4.TRS(
                    hexGrid[x, z].position, 
                    Quaternion.identity, 
                    Vector3.one
                );
                
                // Add to appropriate list based on terrain type
                if (hexGrid[x, z].isRock) 
                {
                    rockCombines.Add(combine);
                }
                else 
                {
                    sandCombines.Add(combine);
                }
            }
        }
        
        // Create sand chunk if we have sand hexes
        if (sandCombines.Count > 0) 
        {
            CreateMaterialChunk(sandCombines, $"SandChunk_{chunkX}_{chunkZ}", sandMaterial);
        }
        
        // Create rock chunk if we have rock hexes
        if (rockCombines.Count > 0) 
        {
            CreateMaterialChunk(rockCombines, $"RockChunk_{chunkX}_{chunkZ}", rockMaterial);
        }
    }
    
    void CreateMaterialChunk(List<CombineInstance> combines, string name, Material material) 
    {
        GameObject chunkObj = new GameObject(name);
        chunkObj.transform.parent = transform;
        
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combines.ToArray());
        
        MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = chunkObj.AddComponent<MeshCollider>(); // Add MeshCollider here for non-instanced mode
        
        meshFilter.mesh = combinedMesh;
        meshRenderer.material = material;
        meshCollider.sharedMesh = combinedMesh; // Assign the combined mesh to the collider
        meshCollider.convex = false; // For static terrain, usually not convex

        // Assign the specified layer to the collider GameObject
        // This is applied to combined meshes too, for consistency if you want to use the layer mask
        chunkObj.layer = (int)Mathf.Log(colliderLayer.value, 2); 
        
        // Add LOD if enabled
        if (useLOD) 
        {
            SetupLOD(chunkObj, combinedMesh);
        }
        
        meshChunks.Add(chunkObj);
    }
    
    void SetupLOD(GameObject chunkObj, Mesh highDetailMesh) 
    {
        LODGroup lodGroup = chunkObj.AddComponent<LODGroup>();
        
        // Create simplified meshes for different LOD levels
        Mesh mediumDetailMesh = CreateSimplifiedMesh(highDetailMesh, 0.5f); 
        Mesh lowDetailMesh = CreateSimplifiedMesh(highDetailMesh, 0.2f);
        
        LOD[] lods = new LOD[3];
        
        // LOD 0 - Full detail
        lods[0] = new LOD(lodDistance1 / 100f, chunkObj.GetComponents<Renderer>());
        
        // LOD 1 - Medium detail
        GameObject mediumObj = CreateLODObject(chunkObj, mediumDetailMesh, "_Medium");
        lods[1] = new LOD(lodDistance2 / 100f, mediumObj.GetComponents<Renderer>());
        
        // LOD 2 - Low detail
        GameObject lowObj = CreateLODObject(chunkObj, lowDetailMesh, "_Low");
        lods[2] = new LOD(0f, lowObj.GetComponents<Renderer>());
        
        lodGroup.SetLODs(lods);
    }
    
    GameObject CreateLODObject(GameObject parent, Mesh mesh, string suffix) 
    {
        GameObject lodObj = new GameObject(parent.name + suffix);
        lodObj.transform.parent = parent.transform;
        lodObj.transform.localPosition = Vector3.zero;
        
        MeshFilter meshFilter = lodObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = lodObj.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = mesh;
        // Use the same material as the parent
        meshRenderer.material = parent.GetComponent<MeshRenderer>().material;
        
        return lodObj;
    }
    
    Mesh CreateSimplifiedMesh(Mesh originalMesh, float quality) 
    {
        Mesh simplifiedMesh = new Mesh();
        
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        
        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();
        
        Dictionary<int, int> oldToNewIndexMap = new Dictionary<int, int>();
        // Use System.Random for deterministic simplification based on seed, independent of Unity's Random.
        System.Random prng = new System.Random(seed); // Initialize with the seed

        int newIndexCounter = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Use prng.NextDouble() for the deterministic random value
            if (prng.NextDouble() < quality)
            {
                oldToNewIndexMap[i] = newIndexCounter;
                newVertices.Add(vertices[i]);
                newIndexCounter++;
            }
            else
            {
                oldToNewIndexMap[i] = -1; 
            }
        }
        
        for (int i = 0; i < triangles.Length; i += 3) 
        {
            int v0 = triangles[i];
            int v1 = triangles[i+1];
            int v2 = triangles[i+2];

            if (oldToNewIndexMap.ContainsKey(v0) && oldToNewIndexMap[v0] != -1 &&
                oldToNewIndexMap.ContainsKey(v1) && oldToNewIndexMap[v1] != -1 &&
                oldToNewIndexMap.ContainsKey(v2) && oldToNewIndexMap[v2] != -1)
            {
                newTriangles.Add(oldToNewIndexMap[v0]);
                newTriangles.Add(oldToNewIndexMap[v1]);
                newTriangles.Add(oldToNewIndexMap[v2]);
            }
        }
        
        simplifiedMesh.vertices = newVertices.ToArray();
        simplifiedMesh.triangles = newTriangles.ToArray();
        simplifiedMesh.RecalculateNormals();
        
        return simplifiedMesh;
    }
    
    Vector3 HexToWorldPos(int x, int z) 
    {
        float worldX = x * hexSize * 0.75f;  
        float worldZ = z * hexSize * Mathf.Sqrt(3f) * 0.5f + (x % 2) * hexSize * Mathf.Sqrt(3f) * 0.25f;
        return new Vector3(worldX, 0, worldZ);
    }
    
    Mesh GetHexMesh() 
    {
        Mesh hexMesh = new Mesh();
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        float currentHexMeshHeight = this.hexMeshHeight * 0.5f; 
        
        // Top face vertices
        Vector3 center = Vector3.zero;
        vertices.Add(center + Vector3.up * currentHexMeshHeight); 
        
        for (int i = 0; i < 6; i++) 
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            Vector3 vertex = new Vector3(
                Mathf.Cos(angle) * hexSize * 0.51f, 
                currentHexMeshHeight,
                Mathf.Sin(angle) * hexSize * 0.51f
            );
            vertices.Add(vertex);
        }
        
        // Bottom face vertices
        vertices.Add(center - Vector3.up * currentHexMeshHeight); 
        for (int i = 0; i < 6; i++) 
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            Vector3 vertex = new Vector3(
                Mathf.Cos(angle) * hexSize * 0.51f, 
                -currentHexMeshHeight,
                Mathf.Sin(angle) * hexSize * 0.51f
            );
            vertices.Add(vertex);
        }
        
        // Top face triangles 
        for (int i = 0; i < 6; i++) 
        {
            triangles.Add(0); 
            triangles.Add((i + 1) % 6 + 1); 
            triangles.Add(i + 1);           
        }
        
        // Bottom face triangles 
        for (int i = 0; i < 6; i++) 
        {
            triangles.Add(7);               
            triangles.Add(i + 8);           
            triangles.Add((i + 1) % 6 + 8); 
        }
        
        // Side faces 
        for (int i = 0; i < 6; i++) 
        {
            int next = (i + 1) % 6;
            
            triangles.Add(i + 1);        
            triangles.Add(next + 1);     
            triangles.Add(i + 8);        
            
            triangles.Add(next + 1);     
            triangles.Add(next + 8);     
            triangles.Add(i + 8);        
        }
        
        hexMesh.vertices = vertices.ToArray();
        hexMesh.triangles = triangles.ToArray();
        hexMesh.RecalculateNormals();
        
        return hexMesh;
    }
    
    void ClearMeshChunks() 
    {
        foreach (GameObject chunk in meshChunks) 
        {
            if (chunk != null) DestroyImmediate(chunk); // Use DestroyImmediate for editor-time cleanup
        }
        meshChunks.Clear();
    }
    
    // Public methods for accessing hex data
    public HexData GetHexData(int x, int z) 
    {
        if (x >= 0 && x < gridWidth && z >= 0 && z < gridHeight) 
        {
            return hexGrid[x, z];
        }
        // Return a default/invalid HexData if out of bounds
        return new HexData(Vector3.zero, -9999f, -9999f, -1, -1, false); 
    }
    
    public Vector3 GetHexWorldPosition(int x, int z) 
    {
        return GetHexData(x, z).position;
    }

    // --- NEW: Added a helper for GridToWorldPosition (more explicit than HexToWorldPos) ---
    public Vector3 GridToWorldPosition(int x, int z)
    {
        // This is the same logic as HexToWorldPos, but exposed for clarity.
        float worldX = x * hexSize * 0.75f;  
        float worldZ = z * hexSize * Mathf.Sqrt(3f) * 0.5f + (x % 2) * hexSize * Mathf.Sqrt(3f) * 0.25f;
        
        // Get the actual height from hexData
        float hexHeight = GetHexData(x, z).height;
        return new Vector3(worldX, baseHeight + hexHeight, worldZ);
    }
    
    // Convert world position to grid coordinates
    public Vector2Int WorldToGridPosition(Vector3 worldPos) 
    {
        // Adjust worldPos.y to be at the base height for accurate grid conversion
        // This assumes the hexes are laid out on a relatively flat plane for grid calculations
        worldPos.y = baseHeight; 

        // --- IMPORTANT: Hex grid coordinate conversion for flat-top hexes ---
        // This is a common conversion for "odd-q" or "even-q" horizontal layouts.
        // Ensure this matches how your grid is actually laid out.
        
        // Approximate x-coordinate
        float approxX = worldPos.x / (hexSize * 0.75f);
        int x = Mathf.RoundToInt(approxX);
        
        // Calculate z based on x's parity
        float zOffset = (x % 2) * hexSize * Mathf.Sqrt(3f) * 0.25f;
        float approxZ = (worldPos.z - zOffset) / (hexSize * Mathf.Sqrt(3f) * 0.5f);
        int z = Mathf.RoundToInt(approxZ);
        
        return new Vector2Int(x, z);
    }
}

public class InstancedHexRenderer : MonoBehaviour 
{
    private Matrix4x4[] matrices;
    private Mesh hexMesh;
    private Material hexMaterial;
    private int instanceCount;

    // Allocate a temporary array once to avoid constant allocations in Update
    private Matrix4x4[] batchMatrices; // Corrected type to Matrix4x4
    private const int BATCH_SIZE = 1023; // Max instances per DrawMeshInstanced call

    public void Initialize(HexagonController.HexData[,] hexGrid, Material material, Mesh mesh, bool renderOnlyRocks) 
    {
        hexMaterial = material;
        hexMesh = mesh;
        
        List<Matrix4x4> matrixList = new List<Matrix4x4>(); 
        
        for (int x = 0; x < hexGrid.GetLength(0); x++) 
        {
            for (int z = 0; z < hexGrid.GetLength(1); z++) 
            {
                // Only add if it matches the renderOnlyRocks criteria
                if (hexGrid[x, z].isRock == renderOnlyRocks) 
                {
                    Vector3 position = hexGrid[x, z].position;
                    matrixList.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one));
                }
            }
        }
        
        matrices = matrixList.ToArray(); 
        instanceCount = matrices.Length;

        // Initialize the temporary batch array once
        batchMatrices = new Matrix4x4[BATCH_SIZE];
    }
    
    void Update() 
    {
        if (matrices == null || hexMesh == null || hexMaterial == null || instanceCount == 0)
        {
            return; // Nothing to draw
        }

        // Draw in batches
        for (int i = 0; i < instanceCount; i += BATCH_SIZE)
        {
            int count = Mathf.Min(instanceCount - i, BATCH_SIZE);
            
            // Copy the current batch of matrices from the main array to the temporary batch array
            System.Array.Copy(matrices, i, batchMatrices, 0, count);

            // Draw the batch
            Graphics.DrawMeshInstanced(
                hexMesh, 
                0, // submeshIndex
                hexMaterial, 
                batchMatrices, // Pass the temporary batch array
                count,         // Pass the actual count for this batch
                null, // materialPropertyBlock (can be null if not needed)
                UnityEngine.Rendering.ShadowCastingMode.On, // shadows
                true, // receiveShadows
                0, // layer
                null, // camera (null means all cameras)
                UnityEngine.Rendering.LightProbeUsage.BlendProbes // lightProbeUsage
            );
        }
    }
}



// using UnityEngine;
// using System.Collections.Generic;
//
// public class HexagonController : MonoBehaviour 
// {
//     [Header("Grid Settings")]
//     public int gridWidth = 100;
//     public int gridHeight = 100;
//     public float hexSize = 1f;
//     [Tooltip("Adjusts the vertical height of the hexagon mesh. Increase to make hexagons appear taller.")]
//     public float hexMeshHeight = 1.0f; 
//     
//     [Header("Desert Height/Noise Settings")]
//     [Tooltip("Enter a value here to get a different terrain layout. Changing this value will generate a new environment.")]
//     public int seed = 0; // The noise seed
//     public float baseHeight = 0f;
//     
//     [Tooltip("A large multiplier for the seed to create distinct noise patterns. Increase for more drastic changes between seeds.")]
//     public float seedOffsetMultiplier = 1000f; 
//     
//     [Header("Sand Dunes (Smooth Hills)")]
//     public float duneScale = 0.05f;         // Large scale for rolling hills
//     public float duneHeight = 8f;          // Maximum dune height
//     public float duneOctaves = 3;          // Number of noise layers for dunes
//     public float dunePersistence = 0.5f;   // How much each octave contributes
//     
//     [Header("Rocky Outcroppings (Sharp Peaks)")]
//     public float rockScale = 0.08f;        // Larger scale for bigger rock clusters
//     public float rockHeight = 15f;         // Maximum rock height
//     public float rockThreshold = 0.6f;     // Only heights above this become rocks
//     public float rockSharpness = 2f;       // How sharp the rock transitions are
//     
//     [Header("Noise Mixing")]
//     public float terrainSmoothness = 0.8f; // 0 = all rocks, 1 = all dunes
//     public float neighborHeightLimit = 0.8f; // How much height difference is allowed between neighbors
//     
//     [Header("Optimization")]
//     public int chunkSize = 20; // Size of each mesh chunk for rendering (if not instanced)
//     public Material sandMaterial;  // Material for sand dunes
//     public Material rockMaterial;  // Material for rocky areas
//     public bool useInstancedRendering = true;
//     
//     [Header("LOD Settings")]
//     public bool useLOD = true;
//     public float lodDistance1 = 50f;
//     public float lodDistance2 = 100f;
//
//     [Header("Collider Settings")]
//     [Tooltip("The layer to assign to the generated hexagon colliders.")]
//     public LayerMask colliderLayer; 
//     [Tooltip("Size of the square chunks for collider generation. Larger values reduce collider count but make chunks less precise.")]
//     public int colliderChunkSize = 10; // New: Size for collider chunks
//     
//     private HexData[,] hexGrid;
//     private List<GameObject> meshChunks = new List<GameObject>();
//     private List<GameObject> hexColliders = new List<GameObject>(); 
//     private Camera playerCamera;
//     
//     // Data structure to store hex information without GameObjects
//     [System.Serializable]
//     public struct HexData 
//     {
//         public Vector3 position;
//         public float height;
//         public float rawHeight;  // Height before neighbor limiting
//         public int gridX, gridZ;
//         public bool isWalkable;
//         public bool isRock;      // True if this hex is rocky terrain
//         
//         public HexData(Vector3 pos, float h, float rawH, int x, int z, bool rock) 
//         {
//             position = pos;
//             height = h;
//             rawHeight = rawH;
//             gridX = x;
//             gridZ = z;
//             isWalkable = true;
//             isRock = rock;
//         }
//     }
//     
//     void Start() 
//     {
//         playerCamera = Camera.main;
//         
//         Debug.Log($"Generating hex grid: {gridWidth}x{gridHeight}");
//         Debug.Log($"Using instanced rendering: {useInstancedRendering}");
//         
//         GenerateOptimizedGrid();
//         
//         Debug.Log($"Grid generation complete. Created {meshChunks.Count} chunks.");
//     }
//
//     // Call this method to regenerate the grid during runtime when the seed changes
//     public void RegenerateGrid()
//     {
//         ClearMeshChunks(); // Clear existing chunks before regenerating
//         ClearHexColliders(); // Clear existing colliders before regenerating
//
//         if (useInstancedRendering)
//         {
//             // Destroy existing instanced renderers if they exist
//             InstancedHexRenderer[] existingRenderers = GetComponentsInChildren<InstancedHexRenderer>();
//             foreach (var renderer in existingRenderers)
//             {
//                 Destroy(renderer.gameObject);
//             }
//         }
//         GenerateOptimizedGrid();
//     }
//     
//     void GenerateOptimizedGrid() 
//     {
//         // First, generate all hex data
//         GenerateHexData();
//         
//         // Check if we have materials
//         if (sandMaterial == null || rockMaterial == null) 
//         {
//             Debug.LogError("Please assign both sand and rock materials in the inspector!");
//             return;
//         }
//         
//         if (useInstancedRendering) 
//         {
//             SetupInstancedRendering();
//             CreateHexagonColliders(); // Create optimized colliders for instanced rendering
//         }
//         else 
//         {
//             // Generate mesh chunks (which will have colliders if added in CreateMaterialChunk)
//             GenerateMeshChunks();
//         }
//     }
//     
//     void GenerateHexData() 
//     {
//         hexGrid = new HexData[gridWidth, gridHeight];
//         
//         // Calculate a large, unique offset based on the seed
//         // This makes sure different seeds sample vastly different parts of the Perlin noise map.
//         // Adding a large number to ensure some initial offset even if seed is 0.
//         float offsetX = (seed * seedOffsetMultiplier) + 10000f; 
//         float offsetY = (seed * seedOffsetMultiplier) + 20000f; // Use a different large offset for Y/Z
//
//         // First pass: Generate raw heights and determine terrain types
//         for (int x = 0; x < gridWidth; x++) 
//         {
//             for (int z = 0; z < gridHeight; z++) 
//             {
//                 Vector3 worldPos = HexToWorldPos(x, z);
//                 
//                 // Pass the combined (x + offsetX, z + offsetY) to the noise functions
//                 float rawHeight = GenerateDesertHeight(x + offsetX, z + offsetY, out bool isRock);
//                 
//                 worldPos.y = baseHeight + rawHeight;
//                 hexGrid[x, z] = new HexData(worldPos, rawHeight, rawHeight, x, z, isRock);
//             }
//         }
//         
//         // Second pass: Apply neighbor height limiting
//         ApplyNeighborHeightLimit();
//     }
//     
//     void ApplyNeighborHeightLimit() 
//     {
//         HexData[,] adjustedGrid = new HexData[gridWidth, gridHeight];
//         
//         for (int x = 0; x < gridWidth; x++) 
//         {
//             for (int z = 0; z < gridHeight; z++) 
//             {
//                 HexData currentHex = hexGrid[x, z];
//                 float maxAllowedHeight = GetMaxAllowedHeight(x, z);
//                 
//                 // Limit height but keep the terrain type
//                 float adjustedHeight = Mathf.Min(currentHex.rawHeight, maxAllowedHeight);
//                 
//                 Vector3 adjustedPos = currentHex.position;
//                 adjustedPos.y = baseHeight + adjustedHeight;
//                 
//                 adjustedGrid[x, z] = new HexData(adjustedPos, adjustedHeight, currentHex.rawHeight, x, z, currentHex.isRock);
//             }
//         }
//         
//         hexGrid = adjustedGrid;
//     }
//     
//     float GetMaxAllowedHeight(int x, int z) 
//     {
//         float currentRawHeight = hexGrid[x, z].rawHeight;
//         float maxNeighborHeight = 0f;
//         int neighborCount = 0;
//         
//         // Check all 6 hexagon neighbors
//         Vector2Int[] neighbors = GetHexNeighbors(x, z);
//         
//         foreach (Vector2Int neighbor in neighbors) 
//         {
//             if (IsValidGridPosition(neighbor.x, neighbor.y)) 
//             {
//                 float neighborHeight = hexGrid[neighbor.x, neighbor.y].rawHeight;
//                 maxNeighborHeight = Mathf.Max(maxNeighborHeight, neighborHeight);
//                 neighborCount++;
//             }
//         }
//         
//         if (neighborCount == 0) return currentRawHeight;
//         
//         // Allow some height difference but cap extreme variations
//         float heightDifference = currentRawHeight - maxNeighborHeight;
//         float maxAllowedDifference = neighborHeightLimit * (rockHeight + duneHeight);
//         
//         if (heightDifference > maxAllowedDifference) 
//         {
//             return maxNeighborHeight + maxAllowedDifference;
//         }
//         
//         return currentRawHeight;
//     }
//     
//     public Vector2Int[] GetHexNeighbors(int x, int z) 
//     {
//         // Hexagon neighbors depend on whether we're on an even or odd row
//         if (x % 2 == 0) 
//         {
//             // Even row
//             return new Vector2Int[] 
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z - 1), // Top Right
//                 new Vector2Int(x + 1, z),     // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z),     // Bottom Left
//                 new Vector2Int(x - 1, z - 1)  // Top Left
//             };
//         }
//         else 
//         {
//             // Odd row
//             return new Vector2Int[] 
//             {
//                 new Vector2Int(x, z - 1),     // Top
//                 new Vector2Int(x + 1, z),     // Top Right
//                 new Vector2Int(x + 1, z + 1), // Bottom Right
//                 new Vector2Int(x, z + 1),     // Bottom
//                 new Vector2Int(x - 1, z + 1), // Bottom Left
//                 new Vector2Int(x - 1, z)      // Top Left
//             };
//         }
//     }
//     
//     public bool IsValidGridPosition(int x, int z) 
//     {
//         return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
//     }
//     
//     // The xCoord and zCoord here are already offset by the seed
//     float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock) 
//     {
//         // Generate smooth sand dunes using multiple octaves of Perlin noise
//         float duneNoise = GenerateOctaveNoise(xCoord, zCoord, duneScale, duneOctaves, dunePersistence);
//         float duneHeight = duneNoise * this.duneHeight;
//         
//         // Generate rocky outcropping noise
//         float rockNoise = Mathf.PerlinNoise(xCoord * rockScale, zCoord * rockScale);
//         
//         // Create sharp rock transitions - only areas above threshold become rocks
//         float rockHeight = 0f;
//         isRock = false;
//         
//         if (rockNoise > rockThreshold) 
//         {
//             // Sharpen the transition using power function
//             float rockIntensity = Mathf.Pow((rockNoise - rockThreshold) / (1f - rockThreshold), rockSharpness);
//             rockHeight = rockIntensity * this.rockHeight;
//             isRock = true;
//         }
//         
//         // Create a blending mask to determine where rocks vs dunes appear
//         // Add another large arbitrary offset to this noise for a different pattern
//         float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);
//         
//         // Blend between dunes and rocks based on terrain smoothness and blend noise
//         float blendFactor = Mathf.Lerp(0.3f, 1f, terrainSmoothness);
//         float finalBlend = Mathf.Clamp01(blendNoise + blendFactor - 0.5f);
//         
//         // Final height is a mix of smooth dunes and sharp rocks
//         return Mathf.Lerp(duneHeight + rockHeight, duneHeight, finalBlend);
//     }
//     
//     // The xCoord and zCoord here are already offset by the seed
//     float GenerateOctaveNoise(float xCoord, float zCoord, float scale, float octaves, float persistence) 
//     {
//         float value = 0f;
//         float amplitude = 1f;
//         float frequency = scale;
//         float maxValue = 0f;
//         
//         for (int i = 0; i < octaves; i++) 
//         {
//             value += Mathf.PerlinNoise(xCoord * frequency, zCoord * frequency) * amplitude;
//             
//             maxValue += amplitude;
//             amplitude *= persistence;
//             frequency *= 2f;
//         }
//         
//         return value / maxValue; // Normalize to 0-1 range
//     }
//     
//     // Option 1: GPU Instancing (Best Performance)
//     void SetupInstancedRendering() 
//     {
//         if (sandMaterial == null || rockMaterial == null) return;
//         
//         // Create separate instanced rendering for sand and rock
//         GameObject sandInstancedObj = new GameObject("Instanced Sand Hexes");
//         sandInstancedObj.transform.parent = transform;
//         
//         GameObject rockInstancedObj = new GameObject("Instanced Rock Hexes");
//         rockInstancedObj.transform.parent = transform;
//         
//         InstancedHexRenderer sandRenderer = sandInstancedObj.AddComponent<InstancedHexRenderer>();
//         InstancedHexRenderer rockRenderer = rockInstancedObj.AddComponent<InstancedHexRenderer>();
//         
//         sandRenderer.Initialize(hexGrid, sandMaterial, GetHexMesh(), false); // Sand hexes
//         rockRenderer.Initialize(hexGrid, rockMaterial, GetHexMesh(), true);  // Rock hexes
//     }
//
//     // New method to create invisible colliders in chunks when using instanced rendering
//     void CreateHexagonColliders()
//     {
//         ClearHexColliders(); // Ensure old colliders are removed
//         Mesh individualHexMesh = GetHexMesh(); // Get the base hex mesh once
//
//         int chunksX = Mathf.CeilToInt((float)gridWidth / colliderChunkSize);
//         int chunksZ = Mathf.CeilToInt((float)gridHeight / colliderChunkSize);
//
//         for (int chunkX = 0; chunkX < chunksX; chunkX++)
//         {
//             for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
//             {
//                 List<CombineInstance> combines = new List<CombineInstance>();
//                 
//                 int startX = chunkX * colliderChunkSize;
//                 int startZ = chunkZ * colliderChunkSize;
//                 int endX = Mathf.Min(startX + colliderChunkSize, gridWidth);
//                 int endZ = Mathf.Min(startZ + colliderChunkSize, gridHeight);
//
//                 for (int x = startX; x < endX; x++)
//                 {
//                     for (int z = startZ; z < endZ; z++)
//                     {
//                         HexData hexData = hexGrid[x, z];
//                         CombineInstance combine = new CombineInstance();
//                         combine.mesh = individualHexMesh;
//                         // Transform the individual hex mesh to its world position
//                         combine.transform = Matrix4x4.TRS(hexData.position, Quaternion.identity, Vector3.one);
//                         combines.Add(combine);
//                     }
//                 }
//
//                 if (combines.Count > 0)
//                 {
//                     GameObject colliderObj = new GameObject($"HexColliderChunk_{chunkX}_{chunkZ}");
//                     colliderObj.transform.parent = transform; // Parent to the HexagonController for organization
//                     colliderObj.transform.localPosition = Vector3.zero; // Collider object itself is at origin, meshes are transformed
//
//                     Mesh combinedColliderMesh = new Mesh();
//                     combinedColliderMesh.CombineMeshes(combines.ToArray(), true, true); // Merge submeshes, use world space transforms
//
//                     MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
//                     meshCollider.sharedMesh = combinedColliderMesh;
//                     meshCollider.convex = false; // For static terrain, usually not convex
//
//                     // Assign the specified layer to the collider GameObject
//                     colliderObj.layer = (int)Mathf.Log(colliderLayer.value, 2); 
//
//                     hexColliders.Add(colliderObj);
//                 }
//             }
//         }
//         Debug.Log($"Created {hexColliders.Count} optimized invisible hexagon colliders.");
//     }
//
//     void ClearHexColliders()
//     {
//         foreach (GameObject colliderObj in hexColliders)
//         {
//             if (colliderObj != null) DestroyImmediate(colliderObj); // Use DestroyImmediate for editor-time cleanup
//         }
//         hexColliders.Clear();
//     }
//     
//     // Option 2: Mesh Combining (Good Performance) - This path also creates colliders
//     void GenerateMeshChunks() 
//     {
//         ClearMeshChunks();
//         
//         int chunksX = Mathf.CeilToInt((float)gridWidth / chunkSize);
//         int chunksZ = Mathf.CeilToInt((float)gridHeight / chunkSize);
//         
//         for (int chunkX = 0; chunkX < chunksX; chunkX++) 
//         {
//             for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++) 
//             {
//                 CreateMeshChunk(chunkX, chunkZ);
//             }
//         }
//     }
//     
//     void CreateMeshChunk(int chunkX, int chunkZ) 
//     {
//         // Separate combines for sand and rock
//         List<CombineInstance> sandCombines = new List<CombineInstance>();
//         List<CombineInstance> rockCombines = new List<CombineInstance>();
//         Mesh hexMesh = GetHexMesh();
//         
//         int startX = chunkX * chunkSize;
//         int startZ = chunkZ * chunkSize;
//         int endX = Mathf.Min(startX + chunkSize, gridWidth);
//         int endZ = Mathf.Min(startZ + chunkSize, gridHeight);
//         
//         for (int x = startX; x < endX; x++) 
//         {
//             for (int z = startZ; z < endZ; z++) 
//             {
//                 CombineInstance combine = new CombineInstance();
//                 combine.mesh = hexMesh;
//                 combine.transform = Matrix4x4.TRS(
//                     hexGrid[x, z].position, 
//                     Quaternion.identity, 
//                     Vector3.one
//                 );
//                 
//                 // Add to appropriate list based on terrain type
//                 if (hexGrid[x, z].isRock) 
//                 {
//                     rockCombines.Add(combine);
//                 }
//                 else 
//                 {
//                     sandCombines.Add(combine);
//                 }
//             }
//         }
//         
//         // Create sand chunk if we have sand hexes
//         if (sandCombines.Count > 0) 
//         {
//             CreateMaterialChunk(sandCombines, $"SandChunk_{chunkX}_{chunkZ}", sandMaterial);
//         }
//         
//         // Create rock chunk if we have rock hexes
//         if (rockCombines.Count > 0) 
//         {
//             CreateMaterialChunk(rockCombines, $"RockChunk_{chunkX}_{chunkZ}", rockMaterial);
//         }
//     }
//     
//     void CreateMaterialChunk(List<CombineInstance> combines, string name, Material material) 
//     {
//         GameObject chunkObj = new GameObject(name);
//         chunkObj.transform.parent = transform;
//         
//         Mesh combinedMesh = new Mesh();
//         combinedMesh.CombineMeshes(combines.ToArray());
//         
//         MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
//         MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
//         MeshCollider meshCollider = chunkObj.AddComponent<MeshCollider>(); // Add MeshCollider here for non-instanced mode
//         
//         meshFilter.mesh = combinedMesh;
//         meshRenderer.material = material;
//         meshCollider.sharedMesh = combinedMesh; // Assign the combined mesh to the collider
//         meshCollider.convex = false; // For static terrain, usually not convex
//
//         // Assign the specified layer to the collider GameObject
//         // This is applied to combined meshes too, for consistency if you want to use the layer mask
//         chunkObj.layer = (int)Mathf.Log(colliderLayer.value, 2); 
//         
//         // Add LOD if enabled
//         if (useLOD) 
//         {
//             SetupLOD(chunkObj, combinedMesh);
//         }
//         
//         meshChunks.Add(chunkObj);
//     }
//     
//     void SetupLOD(GameObject chunkObj, Mesh highDetailMesh) 
//     {
//         LODGroup lodGroup = chunkObj.AddComponent<LODGroup>();
//         
//         // Create simplified meshes for different LOD levels
//         Mesh mediumDetailMesh = CreateSimplifiedMesh(highDetailMesh, 0.5f); 
//         Mesh lowDetailMesh = CreateSimplifiedMesh(highDetailMesh, 0.2f);
//         
//         LOD[] lods = new LOD[3];
//         
//         // LOD 0 - Full detail
//         lods[0] = new LOD(lodDistance1 / 100f, chunkObj.GetComponents<Renderer>());
//         
//         // LOD 1 - Medium detail
//         GameObject mediumObj = CreateLODObject(chunkObj, mediumDetailMesh, "_Medium");
//         lods[1] = new LOD(lodDistance2 / 100f, mediumObj.GetComponents<Renderer>());
//         
//         // LOD 2 - Low detail
//         GameObject lowObj = CreateLODObject(chunkObj, lowDetailMesh, "_Low");
//         lods[2] = new LOD(0f, lowObj.GetComponents<Renderer>());
//         
//         lodGroup.SetLODs(lods);
//     }
//     
//     GameObject CreateLODObject(GameObject parent, Mesh mesh, string suffix) 
//     {
//         GameObject lodObj = new GameObject(parent.name + suffix);
//         lodObj.transform.parent = parent.transform;
//         lodObj.transform.localPosition = Vector3.zero;
//         
//         MeshFilter meshFilter = lodObj.AddComponent<MeshFilter>();
//         MeshRenderer meshRenderer = lodObj.AddComponent<MeshRenderer>();
//         
//         meshFilter.mesh = mesh;
//         // Use the same material as the parent
//         meshRenderer.material = parent.GetComponent<MeshRenderer>().material;
//         
//         return lodObj;
//     }
//     
//     Mesh CreateSimplifiedMesh(Mesh originalMesh, float quality) 
//     {
//         Mesh simplifiedMesh = new Mesh();
//         
//         Vector3[] vertices = originalMesh.vertices;
//         int[] triangles = originalMesh.triangles;
//         
//         List<Vector3> newVertices = new List<Vector3>();
//         List<int> newTriangles = new List<int>();
//         
//         Dictionary<int, int> oldToNewIndexMap = new Dictionary<int, int>();
//         // Use System.Random for deterministic simplification based on seed, independent of Unity's Random.
//         System.Random prng = new System.Random(seed); // Initialize with the seed
//
//         int newIndexCounter = 0;
//         for (int i = 0; i < vertices.Length; i++)
//         {
//             // Use prng.NextDouble() for the deterministic random value
//             if (prng.NextDouble() < quality)
//             {
//                 oldToNewIndexMap[i] = newIndexCounter;
//                 newVertices.Add(vertices[i]);
//                 newIndexCounter++;
//             }
//             else
//             {
//                 oldToNewIndexMap[i] = -1; 
//             }
//         }
//         
//         for (int i = 0; i < triangles.Length; i += 3) 
//         {
//             int v0 = triangles[i];
//             int v1 = triangles[i+1];
//             int v2 = triangles[i+2];
//
//             if (oldToNewIndexMap.ContainsKey(v0) && oldToNewIndexMap[v0] != -1 &&
//                 oldToNewIndexMap.ContainsKey(v1) && oldToNewIndexMap[v1] != -1 &&
//                 oldToNewIndexMap.ContainsKey(v2) && oldToNewIndexMap[v2] != -1)
//             {
//                 newTriangles.Add(oldToNewIndexMap[v0]);
//                 newTriangles.Add(oldToNewIndexMap[v1]);
//                 newTriangles.Add(oldToNewIndexMap[v2]);
//             }
//         }
//         
//         simplifiedMesh.vertices = newVertices.ToArray();
//         simplifiedMesh.triangles = newTriangles.ToArray();
//         simplifiedMesh.RecalculateNormals();
//         
//         return simplifiedMesh;
//     }
//     
//     Vector3 HexToWorldPos(int x, int z) 
//     {
//         float worldX = x * hexSize * 0.75f;  
//         float worldZ = z * hexSize * Mathf.Sqrt(3f) * 0.5f + (x % 2) * hexSize * Mathf.Sqrt(3f) * 0.25f;
//         return new Vector3(worldX, 0, worldZ);
//     }
//     
//     Mesh GetHexMesh() 
//     {
//         Mesh hexMesh = new Mesh();
//         
//         List<Vector3> vertices = new List<Vector3>();
//         List<int> triangles = new List<int>();
//         
//         float currentHexMeshHeight = this.hexMeshHeight * 0.5f; 
//         
//         // Top face vertices
//         Vector3 center = Vector3.zero;
//         vertices.Add(center + Vector3.up * currentHexMeshHeight); 
//         
//         for (int i = 0; i < 6; i++) 
//         {
//             float angle = i * 60f * Mathf.Deg2Rad;
//             Vector3 vertex = new Vector3(
//                 Mathf.Cos(angle) * hexSize * 0.51f, 
//                 currentHexMeshHeight,
//                 Mathf.Sin(angle) * hexSize * 0.51f
//             );
//             vertices.Add(vertex);
//         }
//         
//         // Bottom face vertices
//         vertices.Add(center - Vector3.up * currentHexMeshHeight); 
//         for (int i = 0; i < 6; i++) 
//         {
//             float angle = i * 60f * Mathf.Deg2Rad;
//             Vector3 vertex = new Vector3(
//                 Mathf.Cos(angle) * hexSize * 0.51f, 
//                 -currentHexMeshHeight,
//                 Mathf.Sin(angle) * hexSize * 0.51f
//             );
//             vertices.Add(vertex);
//         }
//         
//         // Top face triangles 
//         for (int i = 0; i < 6; i++) 
//         {
//             triangles.Add(0); 
//             triangles.Add((i + 1) % 6 + 1); 
//             triangles.Add(i + 1);           
//         }
//         
//         // Bottom face triangles 
//         for (int i = 0; i < 6; i++) 
//         {
//             triangles.Add(7);               
//             triangles.Add(i + 8);           
//             triangles.Add((i + 1) % 6 + 8); 
//         }
//         
//         // Side faces 
//         for (int i = 0; i < 6; i++) 
//         {
//             int next = (i + 1) % 6;
//             
//             triangles.Add(i + 1);        
//             triangles.Add(next + 1);     
//             triangles.Add(i + 8);        
//             
//             triangles.Add(next + 1);     
//             triangles.Add(next + 8);     
//             triangles.Add(i + 8);        
//         }
//         
//         hexMesh.vertices = vertices.ToArray();
//         hexMesh.triangles = triangles.ToArray();
//         hexMesh.RecalculateNormals();
//         
//         return hexMesh;
//     }
//     
//     void ClearMeshChunks() 
//     {
//         foreach (GameObject chunk in meshChunks) 
//         {
//             if (chunk != null) DestroyImmediate(chunk); // Use DestroyImmediate for editor-time cleanup
//         }
//         meshChunks.Clear();
//     }
//     
//     // Public methods for accessing hex data
//     public HexData GetHexData(int x, int z) 
//     {
//         if (x >= 0 && x < gridWidth && z >= 0 && z < gridHeight) 
//         {
//             return hexGrid[x, z];
//         }
//         return new HexData(Vector3.zero, 0, 0, 0, 0, false);
//     }
//     
//     public Vector3 GetHexWorldPosition(int x, int z) 
//     {
//         return GetHexData(x, z).position;
//     }
//     
//     // Convert world position to grid coordinates
//     public Vector2Int WorldToGridPosition(Vector3 worldPos) 
//     {
//         // Adjust worldPos.y to be at the base height for accurate grid conversion
//         // This assumes the hexes are laid out on a relatively flat plane for grid calculations
//         worldPos.y = baseHeight; 
//
//         int x = Mathf.RoundToInt(worldPos.x / (hexSize * 0.75f)); // Corrected x calculation
//         
//         // Calculate z based on x's parity
//         float zOffset = (x % 2) * hexSize * Mathf.Sqrt(3f) * 0.25f;
//         int z = Mathf.RoundToInt((worldPos.z - zOffset) / (hexSize * Mathf.Sqrt(3f) * 0.5f));
//         
//         return new Vector2Int(x, z);
//     }
// }
//
// public class InstancedHexRenderer : MonoBehaviour 
// {
//     private Matrix4x4[] matrices;
//     private Mesh hexMesh;
//     private Material hexMaterial;
//     private int instanceCount;
//
//     // Allocate a temporary array once to avoid constant allocations in Update
//     private Matrix4x4[] batchMatrices;
//     private const int BATCH_SIZE = 1023; // Max instances per DrawMeshInstanced call
//
//     public void Initialize(HexagonController.HexData[,] hexGrid, Material material, Mesh mesh, bool renderOnlyRocks) 
//     {
//         hexMaterial = material;
//         hexMesh = mesh;
//         
//         List<Matrix4x4> matrixList = new List<Matrix4x4>(); 
//         
//         for (int x = 0; x < hexGrid.GetLength(0); x++) 
//         {
//             for (int z = 0; z < hexGrid.GetLength(1); z++) 
//             {
//                 if (hexGrid[x, z].isRock == renderOnlyRocks) 
//                 {
//                     Vector3 position = hexGrid[x, z].position;
//                     matrixList.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one));
//                 }
//             }
//         }
//         
//         matrices = matrixList.ToArray(); 
//         instanceCount = matrices.Length;
//         
//         Debug.Log($"Generated {instanceCount} hexes.");
//
//         // Initialize the temporary batch array once
//         batchMatrices = new Matrix4x4[BATCH_SIZE];
//     }
//     
//     void Update() 
//     {
//         if (matrices == null || hexMesh == null || hexMaterial == null || instanceCount == 0)
//         {
//             return; // Nothing to draw
//         }
//
//         // Draw in batches
//         for (int i = 0; i < instanceCount; i += BATCH_SIZE)
//         {
//             int count = Mathf.Min(instanceCount - i, BATCH_SIZE);
//             
//             // Copy the current batch of matrices from the main array to the temporary batch array
//             System.Array.Copy(matrices, i, batchMatrices, 0, count);
//
//             // Draw the batch
//             Graphics.DrawMeshInstanced(
//                 hexMesh, 
//                 0, // submeshIndex
//                 hexMaterial, 
//                 batchMatrices, // Pass the temporary batch array
//                 count,         // Pass the actual count for this batch
//                 null, // materialPropertyBlock (can be null if not needed)
//                 UnityEngine.Rendering.ShadowCastingMode.On, // shadows
//                 true, // receiveShadows
//                 0, // layer
//                 null, // camera (null means all cameras)
//                 UnityEngine.Rendering.LightProbeUsage.BlendProbes // lightProbeUsage
//             );
//         }
//     }
// }