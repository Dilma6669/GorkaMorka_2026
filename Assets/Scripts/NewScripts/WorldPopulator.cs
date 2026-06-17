using UnityEngine;
using System.Collections.Generic;

public class WorldPopulator : MonoBehaviour
{
    SimpleHexGridGround simpleHexGridGround;
    
    public GameObject treePrefab;
    public GameObject rockPrefab;
    
    // Key is the chunkID, Value is a list of all objects in that chunk
    private static Dictionary<Vector2Int, List<CullableObject>> allObjects = new();
    
    private void Awake()
    {
        simpleHexGridGround = GetComponent<SimpleHexGridGround>();
    }
     
    public void PopulateWorld(int worldSeed)
    {
        ClearAll();
        Random.InitState(worldSeed);

        foreach (var hex in simpleHexGridGround.HexagonsInGrid)
        {
            float roll = Random.value;
            if (roll > 0.2f) continue; // Only 20% get objects

            Vector3 surfacePosition = simpleHexGridGround.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            GameObject prefab = (roll < 0.1f) ? treePrefab : rockPrefab;
            GameObject terrainObject = Instantiate(prefab, surfacePosition, randomRotation, simpleHexGridGround.ObjectsContainer.transform);
            
            AssignParentChunkIDToObject(terrainObject, hex.Value);
        }
    }

    private void AssignParentChunkIDToObject(GameObject terrainObject, HexData hexData)
    {
        CullableObject cullableObject = terrainObject.GetComponent<CullableObject>();
        if (cullableObject != null)
        {
            Vector2Int chunkID = simpleHexGridGround.GetChunkID(hexData.GridCoordinates);

            cullableObject.parentChunkID = chunkID;
            RegisterObject(chunkID, cullableObject);
            
            cullableObject.SetVisibility(false); 
        }
    }

    private static void RegisterObject(Vector2Int chunkID, CullableObject cullableObject)
    {
        // If the chunk doesn't exist in our dictionary yet, create the list
        if (!allObjects.ContainsKey(chunkID))
        {
            allObjects[chunkID] = new List<CullableObject>();
        }
        
        allObjects[chunkID].Add(cullableObject);
    }

    public static void SetVisibilityOfObjectsInChunk(Vector2Int chunkID, bool isVisible)
    {
        if (allObjects.TryGetValue(chunkID, out List<CullableObject> objectsInChunk))
        {
            foreach (CullableObject obj in objectsInChunk)
            {
                obj.SetVisibility(isVisible);
            }
        }
    }
    
    public static void ClearAll() 
    {
        allObjects.Clear();
    }
}