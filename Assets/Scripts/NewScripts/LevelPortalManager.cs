using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelPortalManager : MonoBehaviour
{
    private GameLevelManager gameLevelManager;
    private EntitySpawner _entitySpawner;
    
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
        _entitySpawner = GetComponent<EntitySpawner>();
    }

    public void RegisterPortal(SimpleHexGridBase grid, Vector2Int coords, int seed)
    {
        string key = $"{grid.GridType}_{coords.x}_{coords.y}";
        portalSeeds[key] = seed;
    }

    public int GetSeedForPortal(SimpleHexGridBase grid, Vector2Int coords)
    {
        string key = $"{grid.GridType}_{coords.x}_{coords.y}";
        bool exists = portalSeeds.ContainsKey(key);
        Debug.Log($"Looking for key: {key}. Found? {exists}");
        return exists ? portalSeeds[key] : Random.Range(0, 999999);
    }
    
    public void EnterPortal(SimpleHexGridBase grid, Vector2Int coords)
    {
        Debug.Log($"ENTERING PORTAL: Grid Reference = {grid.GetInstanceID()}, Grid Type = {grid.GridType}");
        // 1. CAPTURE the destination info using the CURRENT grid (before switching)
        HexData portalHex = grid.HexagonsInGrid[coords];
    
        // 2. GET the seed using the CURRENT grid reference
        int newSeed = GetSeedForPortal(grid, coords);
    
        // 3. STASH unit data
        Entity entity = EntityCommander.GetEntityInCommand();
        if (entity != null)
        {
            PendingEntityTransfer.Instance.storedUnitData = entity.ExportData();
        }
        EntityCommander.SetEntityToCommand(null);

        // 4. NOW switch levels (this changes gameLevelManager.ActiveGrid)
        gameLevelManager.SwitchToLevel(portalHex.DestinationLevelType);
    
        // 5. Use the seed we already captured
        GenerateNewGrid(newSeed);
    }

    public void GenerateNewGrid(int newSeed)
    {
        // Set the seed on the generator so GenerateGrid uses the right one
        gameLevelManager.ActiveGrid.SetSeed(newSeed);

        // Wipe everything clean
        gameLevelManager.ActiveGrid.ResetGrid();

        // Rebuild the geometry and the population
        gameLevelManager.ActiveGrid.GenerateGrid();
        
        _entitySpawner.SpawnModels(gameLevelManager.ActiveGrid);
    }
}