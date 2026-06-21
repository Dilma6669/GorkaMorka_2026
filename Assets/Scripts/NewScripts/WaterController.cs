using System.Collections.Generic;
using UnityEngine;

public class WaterController : MonoBehaviour
{
    public SimpleHexGridGround simpleHexGridGround;
    public GameObject waterPrefab; // Changed to GameObject for simplicity
    public Material waterMaterial; // Changed to GameObject for simplicity
    public float globalWaterLevel = 1.0f;
    
    // Store active water planes by their ChunkID
    private Dictionary<Vector2Int, WaterEntity> activeWaterPlanes = new();

    private void Start()
    {
        simpleHexGridGround = GetComponent<SimpleHexGridGround>();
    }
    
    public void CreateWaterForChunk(Vector2Int chunkID, List<HexData> chunkHexes, Transform parent)
    {
        GameObject waterObj = new GameObject("WaterMesh_" + chunkID);
        waterObj.transform.SetParent(parent);
        waterObj.transform.localPosition = new Vector3(0, globalWaterLevel, 0); 
    
        // Add this line
        waterObj.AddComponent<WaterEntity>(); 
    
        MeshFilter mf = waterObj.AddComponent<MeshFilter>();
        MeshRenderer mr = waterObj.AddComponent<MeshRenderer>();
        MeshCollider mc = waterObj.AddComponent<MeshCollider>();
    
        mf.mesh = BuildWaterMesh(chunkHexes); 
        mc.sharedMesh = mf.mesh;
        
        mr.material = waterMaterial;

        // Don't forget to store it so we can toggle it later!
        activeWaterPlanes[chunkID] = waterObj.GetComponent<WaterEntity>();
    }
    
    private Mesh BuildWaterMesh(List<HexData> chunkHexes)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();

        // 1. Identify current chunk + all immediate neighbors (The "Halo")
        HashSet<Vector2Int> allRequiredCoords = new HashSet<Vector2Int>();
        foreach (var hex in chunkHexes)
        {
            allRequiredCoords.Add(hex.GridCoordinates);
            foreach (var neighbor in simpleHexGridGround.GetHexNeighbors(hex.GridCoordinates))
                allRequiredCoords.Add(neighbor);
        }

        // 2. Add all vertices at the flat globalWaterLevel
        foreach (var coords in allRequiredCoords)
        {
            // We use your grid's math to find the XZ, but force Y to flat level
            Vector3 worldPos = simpleHexGridGround.GetHexWorldPosition(coords, 0); 
            Vector3 flatPos = new Vector3(worldPos.x, globalWaterLevel, worldPos.z);

            coordToIndex[coords] = vertices.Count;
            vertices.Add(flatPos);
        }

        // 3. Build triangles for current chunk hexes ONLY
        foreach (var hex in chunkHexes)
        {
            List<Vector2Int> neighbors = simpleHexGridGround.GetHexNeighbors(hex.GridCoordinates);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int next = neighbors[(i + 1) % neighbors.Count];
            
                if (coordToIndex.ContainsKey(hex.GridCoordinates) && 
                    coordToIndex.ContainsKey(neighbors[i]) && 
                    coordToIndex.ContainsKey(next))
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
    
    public void SetVisibilityOfWaterInChunk(Vector2Int chunkID, bool isVisible)
    {
        if (activeWaterPlanes.TryGetValue(chunkID, out WaterEntity waterObj))
        {
            waterObj.SetVisibility(isVisible);
        }
    }
}