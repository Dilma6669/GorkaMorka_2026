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
        Random.InitState(worldSeed);

        foreach (var hex in simpleHexGridGround.HexagonsInGrid)
        {
            float roll = Random.value;
            Vector3 surfacePosition = simpleHexGridGround.GetHexTopSurfacePosition(hex.Value.GridCoordinates, hex.Value.Height);
        
            // Generate a random rotation around the Y-axis (Up)
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        
            if (roll < 0.1f) // Tree
            {
                Instantiate(treePrefab, surfacePosition, randomRotation, simpleHexGridGround.ObjectsContainer.transform);
            }
            else if (roll < 0.2f) // Rock
            {
                Instantiate(rockPrefab, surfacePosition, randomRotation, simpleHexGridGround.ObjectsContainer.transform);
            }
        }
    }
}