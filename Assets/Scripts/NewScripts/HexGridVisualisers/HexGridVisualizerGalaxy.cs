using System.Collections.Generic;
using UnityEngine;

public class HexGridVisualizerGalaxy : HexGridVisualizerBase
{
    private SimpleHexGridGalaxy masterGrid;
    private Dictionary<Vector2Int, bool> activeChunks = new Dictionary<Vector2Int, bool>();

    private MaterialPropertyBlock propBlock;
    
    private Dictionary<Vector2Int, bool> isHighRes = new Dictionary<Vector2Int, bool>();
    
    [Header("GPU Landscape Settings")] 
    public Mesh highDetailMesh;
    public Mesh lowDetailMesh;
    public Material hexMaterial_HighRes;
    public Material hexMaterial_LowRes;
    public float lodDistance = 200.0f;

    private Camera camera;
    
    protected new void Awake()
    {
        base.Awake();
        masterGrid = GetComponent<SimpleHexGridGalaxy>();
        camera = Camera.main;
        propBlock = new MaterialPropertyBlock();
    }

    public void SetChunkVisibility(Vector2Int chunkID, bool isVisible)
    {
        activeChunks[chunkID] = isVisible;
    }

    void Update()
    {
        Vector3 camPos = camera.transform.position;

        foreach (var kvp in masterGrid.chunkVisualData)
        {
            if (activeChunks.TryGetValue(kvp.Key, out bool isVisible) && isVisible)
            {
                // Fetch the "baked" choice instead of calculating distance
                bool useHigh = isHighRes[kvp.Key];
                
                Mesh mesh = useHigh ? highDetailMesh : lowDetailMesh;
                Material mat = useHigh ? hexMaterial_HighRes : hexMaterial_LowRes;

                Graphics.DrawMeshInstanced(mesh, 0, mat, kvp.Value, kvp.Value.Length);
            }
        }
    }


    public override void GenerateVisualGrid(SimpleHexGridBase gridBase)
    {
        isHighRes.Clear();
    
        // Use a scale factor to control how large the 'patches' are
        float scale = 0.1f; 

        foreach (var chunkID in masterGrid.chunkVisualData.Keys)
        {
            // PerlinNoise takes two floats. 
            // We add the MasterSeed to the coordinates so the 'noise' 
            // shifts based on your seed.
            float noise = Mathf.PerlinNoise(
                (chunkID.x + GameLevelManager.MasterSeed) * scale, 
                (chunkID.y + GameLevelManager.MasterSeed) * scale
            );

            // If noise > 0.5, it's high res, otherwise low res.
            // This creates clusters instead of a 50/50 split.
            isHighRes[chunkID] = noise > 0.5f; 
        }
    }
    
    public void Clear()
    {
        activeChunks.Clear();
    }
}