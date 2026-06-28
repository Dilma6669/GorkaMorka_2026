using UnityEngine;
using System.Collections.Generic;

public class PopulaterGalaxy : PopulaterBase
{
    private void Awake()
    {
        hexGrid = GetComponent<SimpleHexGridGalaxy>();
    }
     
    public override void PopulateObjects(int worldSeed)
    {
        ClearAll();
        
        // 1. Calculate sum of weights for relative distribution
        float totalWeight = 0f;
        foreach (var item in spawnableObjects) totalWeight += item.spawnWeight;

        var hexOccupancyUpdates = new List<(Vector2Int key, GameObject obj)>();

        foreach (var hex in hexGrid.HexagonsInGrid)
        {
            // 1. Create a unique seed for this specific hex
            int hexSeed = worldSeed + (hex.Key.x * 73856093) ^ (hex.Key.y * 19349663);
            System.Random prng = new System.Random(hexSeed);

            // 2. Use prng for everything:
            if (prng.NextDouble() > spawnDensity) continue; 
    
            float roll = (float)prng.NextDouble() * totalWeight;
            SpawnableObject selectedPrefab = GetRandomPrefab(roll); // Ensure this uses prng if needed
        
            if (selectedPrefab.prefab == null) continue;

            float randomRotationY = (float)prng.NextDouble() * 360f;
        
            Vector3 surfacePosition = hexGrid.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            Quaternion rotation = Quaternion.Euler(0, randomRotationY, 0);
            GameObject terrainObject = Instantiate(selectedPrefab.prefab, surfacePosition, rotation, hexGrid.ObjectsContainer.transform);
    
            AssignParentChunkIDToObject(terrainObject, hex.Value);
            if (selectedPrefab.occupiesHex)
            {
                hexOccupancyUpdates.Add((hex.Key, terrainObject));
            }
        }

        // Apply updates...
        foreach (var update in hexOccupancyUpdates)
        {
            if (hexGrid.HexagonsInGrid.TryGetValue(update.key, out HexData hexData))
            {
                hexData.IsOccupied = true;
                hexData.HexOccupier = update.obj.name;
                hexGrid.HexagonsInGrid[update.key] = hexData;
            }
        }
    }
}