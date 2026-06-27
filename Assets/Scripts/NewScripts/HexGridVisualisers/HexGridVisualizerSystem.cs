using System.Collections.Generic;
using UnityEngine;

public class HexGridVisualizerSystem : HexGridVisualizerBase
{
    private SimpleHexGridSystem masterGrid;
    private Dictionary<Vector2Int, bool> activeChunks = new Dictionary<Vector2Int, bool>();

    private MaterialPropertyBlock propBlock;
    
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
        masterGrid = GetComponent<SimpleHexGridSystem>();
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
        // Seed the random generator with the MasterSeed for consistent results
        Random.InitState(GameLevelManager.MasterSeed);

        foreach (var kvp in masterGrid.chunkVisualData)
        {
            if (activeChunks.TryGetValue(kvp.Key, out bool isVisible) && isVisible)
            {
                float dist = Vector3.Distance(camPos, masterGrid.chunkBounds[kvp.Key].center);
                Mesh mesh = (dist < lodDistance) ? highDetailMesh : lowDetailMesh;
                Material mat = (dist < lodDistance) ? hexMaterial_HighRes : hexMaterial_LowRes;

                // Set a random shade value for this draw call
                // float randomShade = Random.Range(0.2f, 1.0f);
                // propBlock.SetFloat("_Shade", randomShade); 

                Graphics.DrawMeshInstanced(mesh, 0, mat, kvp.Value, kvp.Value.Length, propBlock);
            }
        }
    }


    public override void GenerateVisualGrid(SimpleHexGridBase gridBase)
    {
        // Data is now populated by the Master Grid during GeneratePhysicsProxy
    }
    
    public void Clear()
    {
        activeChunks.Clear();
    }
}