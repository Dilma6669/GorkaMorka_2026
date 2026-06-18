using UnityEngine;
using System.Collections.Generic;

public class HexGridVisualizerGround : HexGridVisualizerBase
{
    private SimpleHexGridGround masterGrid;
    private Dictionary<Vector2Int, bool> activeChunks = new Dictionary<Vector2Int, bool>();

    [Header("GPU Landscape Settings")] 
    public Mesh highDetailMesh;
    public Mesh lowDetailMesh;
    public Material hexMaterial_HighRes;
    public Material hexMaterial_LowRes;
    public float lodDistance = 200.0f;

    protected new void Awake()
    {
        base.Awake();
        masterGrid = GetComponent<SimpleHexGridGround>();
    }

    public void SetChunkVisibility(Vector2Int chunkID, bool isVisible)
    {
        activeChunks[chunkID] = isVisible;
    }

    void Update()
    {
        Vector3 camPos = Camera.main.transform.position;

        foreach (var kvp in masterGrid.chunkVisualData)
        {
            if (activeChunks.TryGetValue(kvp.Key, out bool isVisible) && isVisible)
            {
                float dist = Vector3.Distance(camPos, masterGrid.chunkBounds[kvp.Key].center);
                Mesh mesh = (dist < lodDistance) ? highDetailMesh : lowDetailMesh;
                Material mat = (dist < lodDistance) ? hexMaterial_HighRes : hexMaterial_LowRes;

                Graphics.DrawMeshInstanced(mesh, 0, mat, kvp.Value, kvp.Value.Length);
            }
        }
    }

    public override void GenerateVisualGrid(SimpleHexGridBase gridBase)
    {
        // Data is now populated by the Master Grid during GeneratePhysicsProxy
    }
}