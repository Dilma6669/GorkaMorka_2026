using UnityEngine;
using System.Collections.Generic;

public class WorldPopulator : MonoBehaviour
{
    SimpleHexGridGround simpleHexGridGround;
    TerrainGenerator terrainGenerator;
    
    public GameObject treePrefab;
    public GameObject rockPrefab;
    
    // Key is the chunkID, Value is a list of all objects in that chunk
    private Dictionary<Vector2Int, List<CullableObject>> allObjects = new();
    
    private void Awake()
    {
        simpleHexGridGround = GetComponent<SimpleHexGridGround>();
        terrainGenerator = GetComponent<TerrainGenerator>();
    }
     
    public void PopulateWorld(int worldSeed)
    {
        ClearAll();
        Random.InitState(worldSeed);

        float totalThreshold = terrainGenerator.terrainSettings.treePercentage + terrainGenerator.terrainSettings.rockPercentage;

        foreach (var hex in simpleHexGridGround.HexagonsInGrid)
        {
            float roll = Random.value;
            if (roll > totalThreshold) continue; 

            // Determine which object to spawn based on the relative percentages
            // If roll < treePercentage, it's a tree. If between tree and tree+rock, it's a rock.
            GameObject prefab = (roll < terrainGenerator.terrainSettings.treePercentage) ? treePrefab : rockPrefab;
        
            Vector3 surfacePosition = simpleHexGridGround.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

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

    private void UpdateObjectVisibilityByDistance(CullableObject obj, float activeRadius)
    {
        float radiusSquared = activeRadius * activeRadius;

        // Calculate distance squared (cheaper than Vector3.Distance)
        float distSq = (obj.transform.position - Camera.main.transform.position).sqrMagnitude;
        
        obj.SetVisibility(distSq < radiusSquared);
    }

    public void ClearAll() 
    {
        allObjects.Clear();
    }
}