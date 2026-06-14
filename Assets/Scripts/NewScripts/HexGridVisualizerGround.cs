using UnityEngine;
using System.Collections.Generic;

public class HexGridVisualizerGround : MonoBehaviour
{
    [Header("Overlay Settings (The few GameObjects we still need)")]
    public GameObject HexagonsContainer;

    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private const int BATCH_SIZE = 128; 
    
    public float hexVisualHeight = 1.0f; 

    private SimpleHexGrid targetGrid;

    private Matrix4x4[] fullMatrixArray; // Store all matrices here
    private Bounds[] batchBounds; // Stores the box for each batch
    
    [Header("GPU Landscape Settings")]
    public Mesh highDetailMesh;
    public Mesh lowDetailMesh; // Add this
    public Material hexMaterial_HighRes;
    public Material hexMaterial_LowRes;
    public float lodDistance = 200.0f; // Distance to switch meshes
    public float extremeDistance = 600.0f;
    
    private Matrix4x4[][] batchCache;
    
    private UnityEngine.MaterialPropertyBlock _propBlock;

// 2. Initialize it in Awake
    void Awake()
    {
        _propBlock = new UnityEngine.MaterialPropertyBlock();
        targetGrid = GetComponent<SimpleHexGrid>();
        targetGrid.OnGridReady += GenerateVisualGrid;
    }
    
    public void GenerateVisualGrid(SimpleHexGrid grid)
    {
        // 1. Safety check
        if (grid.HexagonsInGrid == null || grid.HexagonsInGrid.Count == 0) return;

        matrices.Clear();
        float scaleFactor = grid.hexSize * 2f;
        Vector3 scale = new Vector3(scaleFactor, hexVisualHeight, scaleFactor);

        foreach (var hex in grid.HexagonsInGrid.Values)
        {
            Vector3 pos = grid.GetHexWorldPosition(hex.GridCoordinates, hex.Height);
            matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        }
    
        fullMatrixArray = matrices.ToArray();

        // 2. DECLARE AND CALCULATE numBatches FIRST
        int numBatches = Mathf.CeilToInt((float)fullMatrixArray.Length / BATCH_SIZE);
    
        // 3. Now it is safe to use numBatches to initialize your arrays
        batchBounds = new Bounds[numBatches];
        batchCache = new Matrix4x4[numBatches][];
    
        // 4. Fill the arrays
        for(int b = 0; b < numBatches; b++) 
        {
            int start = b * BATCH_SIZE;
            int count = Mathf.Min(fullMatrixArray.Length - start, BATCH_SIZE);
        
            batchCache[b] = new Matrix4x4[count];
            System.Array.Copy(fullMatrixArray, start, batchCache[b], 0, count);

            // Calculate Bounds
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        
            for (int i = 0; i < count; i++) 
            {
                Vector3 pos = batchCache[b][i].GetColumn(3);
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
            batchBounds[b] = new Bounds((min + max) * 0.5f, (max - min) + Vector3.one * 5f);
        }
    }
    
    void Update()
    {
        if (batchCache == null || batchBounds == null) return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        Vector3 camPos = Camera.main.transform.position;

        // Create an array of indices and sort them by distance to camera
        // Doing this every frame is surprisingly fast for ~2000 batches
        int[] sortedIndices = new int[batchCache.Length];
        for (int i = 0; i < batchCache.Length; i++) sortedIndices[i] = i;

        System.Array.Sort(sortedIndices, (a, b) => 
            Vector3.Distance(camPos, batchBounds[a].center).CompareTo(
                Vector3.Distance(camPos, batchBounds[b].center)));

        foreach (int b in sortedIndices)
        {
            if (GeometryUtility.TestPlanesAABB(planes, batchBounds[b]))
            {
                float dist = Vector3.Distance(camPos, batchBounds[b].center);
                int count = batchCache[b].Length;
        
                if (dist < lodDistance) 
                    Graphics.DrawMeshInstanced(highDetailMesh, 0, hexMaterial_HighRes, batchCache[b], count);
                else if (dist < extremeDistance)
                    Graphics.DrawMeshInstanced(lowDetailMesh, 0, hexMaterial_LowRes, batchCache[b], count);
            }
        }
    }
    
    void OnDestroy()
    {
        targetGrid.OnGridReady -= GenerateVisualGrid;
    }
}