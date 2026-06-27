using UnityEngine;
using System.Collections.Generic;

public class PopulaterTerrain : PopulaterBase
{
    SimpleHexGridTerrain simpleHexGridTerrain;
    
    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridTerrain>();
    }
     
    public override void PopulateWorld(int worldSeed)
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
    
    protected override void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData)
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
}