using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelPortalManager : MonoBehaviour
{
    private GameLevelManager gameLevelManager;
    
    // 1. The static instance
    public static LevelPortalManager Instance { get; private set; }

    private Dictionary<string, int> portalSeeds = new Dictionary<string, int>();

    // 2. Awake sets up the singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: keeps it alive when changing scenes

        gameLevelManager = GetComponent<GameLevelManager>();
    }

    public void RegisterPortal(SimpleHexGridBase grid, Vector2Int coords, int seed)
    {
        string key = $"{grid.name}_{coords.x}_{coords.y}";
        portalSeeds[key] = seed;
    }

    public int GetSeedForPortal(SimpleHexGridBase grid, Vector2Int coords)
    {
        string key = $"{grid.name}_{coords.x}_{coords.y}";
        return portalSeeds.ContainsKey(key) ? portalSeeds[key] : Random.Range(0, 999999);
    }
    
    // Inside LevelPortalManager.cs
// Inside LevelPortalManager.cs
    public void EnterPortal(SimpleHexGridBase grid, Vector2Int coords)
    {
        EntityCommander.SetEntityToCommand(null);
        
        HexData portalHex = grid.HexagonsInGrid[coords];
        if (!portalHex.IsPortal) return;

        // 1. Get the seed for the destination
        int newSeed = GetSeedForPortal(grid, coords);

        // 2. Perform the switch
        gameLevelManager.SwitchToLevel(portalHex.DestinationLevelType);

        // 3. Reset and Rebuild
        if (gameLevelManager.ActiveGrid is SimpleHexGridTerrain terrainGrid)
        {
            // Set the seed on the generator so GenerateGrid uses the right one
            terrainGrid.SetSeed(newSeed); 
        
            // Wipe everything clean
            terrainGrid.ResetGrid();
        
            // Rebuild the geometry and the population
            terrainGrid.GenerateGrid(); 
        }
    }
}