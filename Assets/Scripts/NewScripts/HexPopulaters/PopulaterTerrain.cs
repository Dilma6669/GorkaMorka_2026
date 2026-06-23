using UnityEngine;
using System.Collections.Generic;

public class PopulaterTerrain : MonoBehaviour
{
    SimpleHexGridTerrain simpleHexGridTerrain;

    [Range(0f, 1f)]
    [Tooltip("Percentage of total hexes that will have an object (0.5 = 50% of hexes)")]
    public float spawnDensity = 0.5f;
    
    [System.Serializable]
    public struct SpawnableObject
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnWeight; // Chance relative to others
    }
    
    public List<SpawnableObject> spawnableObjects;
    
    // Key is the chunkID, Value is a list of all objects in that chunk
    private Dictionary<Vector2Int, List<CullableObject>> allObjects = new();
    
    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridTerrain>();
    }
     
    public void PopulateWorld(int worldSeed)
    {
        ClearAll();
        Random.InitState(worldSeed);

        // 1. Calculate sum of weights for relative distribution
        float totalWeight = 0f;
        foreach (var item in spawnableObjects) totalWeight += item.spawnWeight;

        var hexOccupancyUpdates = new List<(Vector2Int key, GameObject obj)>();

        foreach (var hex in simpleHexGridTerrain.HexagonsInGrid)
        {
            // 2. Density Check: Does this hex get an object at all?
            if (Random.value > spawnDensity) continue; 
    
            // 3. Variety Check: Which object? 
            // We pick a random value between 0 and totalWeight
            float roll = Random.value * totalWeight;
            GameObject prefabToSpawn = GetRandomPrefab(roll);
        
            if (prefabToSpawn == null) continue;

            // ... instantiation logic (stays the same) ...
            Vector3 surfacePosition = simpleHexGridTerrain.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            GameObject terrainObject = Instantiate(prefabToSpawn, surfacePosition, randomRotation, simpleHexGridTerrain.ObjectsContainer.transform);
        
            AssignParentChunkIDToObject(terrainObject, hex.Value);
            hexOccupancyUpdates.Add((hex.Key, terrainObject));
        }

        // Apply updates...
        foreach (var update in hexOccupancyUpdates)
        {
            if (simpleHexGridTerrain.HexagonsInGrid.TryGetValue(update.key, out HexData hexData))
            {
                hexData.IsOccupied = true;
                hexData.HexOccupier = update.obj.name;
                simpleHexGridTerrain.HexagonsInGrid[update.key] = hexData;
            }
        }
    }
    
    private GameObject GetRandomPrefab(float roll)
    {
        float cumulative = 0f;
        foreach (var item in spawnableObjects)
        {
            cumulative += item.spawnWeight;
            if (roll <= cumulative) return item.prefab;
        }
        return null;
    }

    private void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData)
    {
        CullableObject cullableObject = terrainObject.GetComponent<CullableObject>();
        if (cullableObject != null)
        {
            Vector2Int chunkID = simpleHexGridTerrain.GetChunkID(hexData.GridCoordinates);

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