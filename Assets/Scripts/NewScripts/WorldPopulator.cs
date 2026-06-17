using UnityEngine;
using System.Collections.Generic;

public class WorldPopulator : MonoBehaviour
{
    SimpleHexGridGround simpleHexGridGround;
    
    public GameObject treePrefab; // Drag your tree "circle" here
    public GameObject rockPrefab; // Drag your rock "square" here
    
    private void Awake()
    {
        simpleHexGridGround = GetComponent<SimpleHexGridGround>();
    }
    
    public void PopulateWorld(int worldSeed)
    {
        // Set the random seed so the results are always the same for this map
        Random.InitState(worldSeed);

        foreach (var hex in simpleHexGridGround.HexagonsInGrid)
        {
            // Simple logic: 20% chance to spawn something
            float roll = Random.value;

            Vector3 surfacePosition = simpleHexGridGround.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
            
            if (roll < 0.1f) // 10% chance for a tree
            {
                Instantiate(treePrefab, surfacePosition + Vector3.up * 0.5f, Quaternion.identity, simpleHexGridGround.ObjectsContainer.transform);
            }
            else if (roll < 0.2f) // Another 10% chance for a rock
            {
                Instantiate(rockPrefab, surfacePosition + Vector3.up * 0.5f, Quaternion.identity, simpleHexGridGround.ObjectsContainer.transform);
            }
        }
    }
}