using UnityEngine;
using System.Collections.Generic;

public class WorldPopulator : MonoBehaviour
{
    SimpleHexGridBase simpleHexGridBase;
    TerrainGenerator terrainGenerator;
    
    public GameObject treePrefab;
    public GameObject rockPrefab;
    
    // Key is the chunkID, Value is a list of all objects in that chunk
    private Dictionary<Vector2Int, List<CullableObject>> allObjects = new();
    
    private void Awake()
    {
        simpleHexGridBase = GetComponent<SimpleHexGridBase>();
        terrainGenerator = GetComponent<TerrainGenerator>();
    }
     
    public void PopulateWorld(int worldSeed)
    {
        ClearAll();
        Random.InitState(worldSeed);

        float totalThreshold = simpleHexGridBase.terrainSettings.treePercentage + simpleHexGridBase.terrainSettings.rockPercentage;
    
        // 1. Create a list to store the modifications
        var hexOccupancyUpdates = new List<(Vector2Int key, GameObject obj)>();

        foreach (var hex in simpleHexGridBase.HexagonsInGrid)
        {
            float roll = Random.value;
            if (roll > totalThreshold) continue; 

            GameObject prefab = (roll < simpleHexGridBase.terrainSettings.treePercentage) ? treePrefab : rockPrefab;
    
            Vector3 surfacePosition = simpleHexGridBase.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            GameObject terrainObject = Instantiate(prefab, surfacePosition, randomRotation, simpleHexGridBase.ObjectsContainer.transform);
    
            AssignParentChunkIDToObject(terrainObject, hex.Value);
        
            // 2. Queue the update instead of applying it immediately
            hexOccupancyUpdates.Add((hex.Key, terrainObject));
        }

        // 3. Apply all updates safely after the loop
        foreach (var update in hexOccupancyUpdates)
        {
            if (simpleHexGridBase.HexagonsInGrid.TryGetValue(update.key, out HexData hexData))
            {
                hexData.SetIsOccupied(true);
                hexData.SetOccupier(update.obj.name);
                simpleHexGridBase.HexagonsInGrid[update.key] = hexData;
            }
        }
    }

    private void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData)
    {
        CullableObject cullableObject = terrainObject.GetComponent<CullableObject>();
        if (cullableObject != null)
        {
            Vector2Int chunkID = simpleHexGridBase.GetChunkID(hexData.GridCoordinates);

            cullableObject.parentChunkID = chunkID;
            RegisterObject(chunkID, cullableObject);
            
            cullableObject.SetVisibility(false); 
        }
    }

    private void RegisterObject(Vector2Int chunkID, CullableObject cullableObject)
    {
        // If the chunk doesn't exist in our dictionary yet, create the list
        if (!allObjects.ContainsKey(chunkID))
        {
            allObjects[chunkID] = new List<CullableObject>();
        }
        
        allObjects[chunkID].Add(cullableObject);
    }

    public void SetVisibilityOfObjectsInChunk(Vector2Int chunkID, bool isVisible)
    {
        if (allObjects.TryGetValue(chunkID, out List<CullableObject> objectsInChunk))
        {
            foreach (CullableObject obj in objectsInChunk)
            {
                obj.SetVisibility(isVisible);
            }
        }
    }
    

    public void ClearAll() 
    {
        allObjects.Clear();
    }
}