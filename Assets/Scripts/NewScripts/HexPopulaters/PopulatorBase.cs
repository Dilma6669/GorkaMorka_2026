using UnityEngine;
using System.Collections.Generic;

public abstract class PopulaterBase : MonoBehaviour
{
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

    public abstract void PopulateWorld(int worldSeed);

    protected abstract void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData);
    
    protected GameObject GetRandomPrefab(float roll)
    {
        float cumulative = 0f;
        foreach (var item in spawnableObjects)
        {
            cumulative += item.spawnWeight;
            if (roll <= cumulative) return item.prefab;
        }
        return null;
    }

    protected void RegisterObject(Vector2Int chunkID, CullableObject cullableObject)
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
        // Destroy all current GameObjects in the scene
        foreach (var entry in allObjects)
        {
            foreach (var obj in entry.Value)
            {
                Destroy(obj.gameObject);
            }
        }
        allObjects.Clear();
    }
}