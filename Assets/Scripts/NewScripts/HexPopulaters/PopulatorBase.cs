using UnityEngine;
using System.Collections.Generic;

public abstract class PopulaterBase : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("Percentage of total hexes that will have an object (0.5 = 50% of hexes)")]
    public float spawnDensity = 0.5f;
    
    protected SimpleHexGridBase hexGrid;
    
    [System.Serializable]
    public struct SpawnableObject
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnWeight;
        [Tooltip("If checked, objects of this type will block movement on this hex.")]
        public bool occupiesHex;
    }
    
    public List<SpawnableObject> spawnableObjects;
    
    // Key is the chunkID, Value is a list of all objects in that chunk
    private Dictionary<Vector2Int, List<CullableObject>> allObjects = new();

    public abstract void PopulateObjects(int worldSeed);

    protected void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData)
    {
        CullableObject cullableObject = terrainObject.GetComponent<CullableObject>();
        if (cullableObject != null)
        {
            Vector2Int chunkID = hexGrid.GetChunkID(hexData.GridCoordinates);

            cullableObject.parentChunkID = chunkID;
            RegisterObject(chunkID, cullableObject);
            
            cullableObject.SetVisibility(false); 
        }
    }
    
    protected SpawnableObject GetRandomPrefab(float roll)
    {
        float cumulative = 0f;
        foreach (var item in spawnableObjects)
        {
            cumulative += item.spawnWeight;
            if (roll <= cumulative) return item;
        }
        return default;
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