using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GameLevelManager : MonoBehaviour
{
    private EntitySpawner entitySpawner;
    private HexOverlayManager hexOverlayManager;
    public HexGridManager.GridType startingLevel;

    public static SimpleHexGridBase ActiveGrid { get; private set; }

    public HexGridManager.GridType currentLevel;
    public static HexGridManager.GridType CurrentLevel;

    // The Inspector will see this field
    [SerializeField] private int _masterSeed = 99;

    // This is the static property the rest of your code uses
    public static int MasterSeed
    {
        get { return Instance._masterSeed; }
        private set => Instance._masterSeed = value;
    }

    [SerializeField] private NullableVector2Int lastJumpedGalaxyCoords;
    [SerializeField] private NullableVector2Int lastJumpedSystemCoords;
    [SerializeField] private NullableVector2Int lastJumpedWorldCoords;
    [SerializeField] private NullableVector2Int lastJumpedTerrainCoords;

// Getters
    public Vector2Int? GetGlobalGalaxyCoords() =>
        lastJumpedGalaxyCoords.isSet ? (Vector2Int?)lastJumpedGalaxyCoords.coords : null;

    public Vector2Int? GetGlobalSystemCoords() =>
        lastJumpedSystemCoords.isSet ? (Vector2Int?)lastJumpedSystemCoords.coords : null;

    public Vector2Int? GetGlobalWorldCoords() =>
        lastJumpedWorldCoords.isSet ? (Vector2Int?)lastJumpedWorldCoords.coords : null;

    public Vector2Int? GetGlobalTerrainCoords() =>
        lastJumpedTerrainCoords.isSet ? (Vector2Int?)lastJumpedTerrainCoords.coords : null;


    private void SetGlobalLastJumpedCoords(EntityData entityData)
    {
        Vector2Int? coords = null!;
        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Galaxy);
        lastJumpedGalaxyCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedGalaxyCoords.coords = coords.Value;

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.System);
        lastJumpedSystemCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedSystemCoords.coords = coords.Value;

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.World);
        lastJumpedWorldCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedWorldCoords.coords = coords.Value;

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Terrain);
        lastJumpedTerrainCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedTerrainCoords.coords = coords.Value;
    }


    [System.Serializable]
    public struct LevelData
    {
        public HexGridManager.GridType type;
        public GameObject levelRoot;
    }

    public List<LevelData> levels;

    public static GameLevelManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        entitySpawner = GetComponent<EntitySpawner>();
        hexOverlayManager = GetComponent<HexOverlayManager>();
    }

    private void Start()
    {
        Random.InitState(MasterSeed);

        EntityData entityData = entitySpawner.CreateUnitDataOnActiveGrid();

        entityData.SetLastJumpLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.Galaxy,
            coords = null
        });
        entityData.SetLastJumpLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.System,
            coords = null
        });
        entityData.SetLastJumpLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.World,
            coords = null
        });
        entityData.SetLastJumpLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.Terrain,
            coords = null
        });


        DataManager.UpdateData(entityData.entityGUID, entityData);
        SetGlobalLastJumpedCoords(entityData);

        int seed = GenerateSeed();

        SwitchToLevel(startingLevel, seed);
    }

    public void SwitchToLevel(HexGridManager.GridType targetLevel, int seed)
    {
        entitySpawner.DestroyAllActiveEntities();
        hexOverlayManager.ClearAll();
        
        if (ActiveGrid != null)
        {
            bool isMovingUp = targetLevel > ActiveGrid.GridType;
            bool isMovingDown = targetLevel < ActiveGrid.GridType;

            Debug.Log($"Entity movingUp: {isMovingUp}, movingDown: {isMovingDown}");
            
            if (isMovingDown)
            {
                if (EntityCommander.GetEntityInCommand() != null)
                {
                    if (DataManager.TryGetData(EntityCommander.GetEntityInCommand().EntityGUID, out EntityData entityData))
                    {
                        if (currentLevel == HexGridManager.GridType.Galaxy)
                        {
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.Galaxy,
                                coords = EntityCommander.GetEntityInCommand().CurrentGridCoordinates
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.System,
                                coords = null
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = null
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.Terrain,
                                coords = null
                            });
                        }
                        else if (currentLevel == HexGridManager.GridType.System)
                        {
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.Galaxy,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Galaxy)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.System,
                                coords = EntityCommander.GetEntityInCommand().CurrentGridCoordinates
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = null
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = null
                            });
                        }
                        else if (currentLevel == HexGridManager.GridType.World)
                        {
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.Galaxy,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Galaxy)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.System,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.System)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = EntityCommander.GetEntityInCommand().CurrentGridCoordinates
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = null
                            });
                        }
                        else if (currentLevel == HexGridManager.GridType.Terrain)
                        {
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.Galaxy,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Galaxy)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.System,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.System)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.World)
                            });
                            entityData.SetLastJumpLevelCoords(new LevelPositionPair()
                            {
                                level = HexGridManager.GridType.World,
                                coords = EntityCommander.GetEntityInCommand().CurrentGridCoordinates
                            });
                        }
                        DataManager.UpdateData(entityData.entityGUID, entityData);
                        SetGlobalLastJumpedCoords(entityData);
                    }
                }
            }
        }

        
        EntityCommander.SetEntityToCommand(null);
        
        foreach (var data in levels)
        {
            data.levelRoot.SetActive(false);
            
            // Set active only if it matches, disable everything else
            bool isMatch = (data.type == targetLevel);
            if (isMatch)
            {
                data.levelRoot.SetActive(true);
                ActiveGrid = data.levelRoot.GetComponent<SimpleHexGridBase>();
                currentLevel = targetLevel;
                CurrentLevel = targetLevel;
            }
        }

        ActiveGrid.GridGUID = seed;
        if (ActiveGrid.Populater != null)
        {
            ActiveGrid.Populater.ClearAll();
        }
        
        Debug.Log($"Switched to: {targetLevel}");
        
        GenerateNewGridOnActiveGrid(seed);
    }

    public void GenerateNewGridOnActiveGrid(int newSeed)
    {
        Debug.Log($"GENERATOR: Received Seed {newSeed}");
        // Set the seed on the generator so GenerateGrid uses the right one
        ActiveGrid.SetSeed(newSeed);

        // Wipe everything clean
        ActiveGrid.ResetGrid();

        // Rebuild the geometry and the population
        ActiveGrid.GenerateGrid();
        
        entitySpawner.SpawnModelsOnGrid(ActiveGrid);
    }
    
    [ContextMenu("Move down Level")]
    public void MoveDownLevel()
    {
        Entity entity = EntityCommander.GetEntityInCommand();
        if (entity == null) return;
        
        // Use the entity's current location to jump
        HexGridManager.GridType childLevel = ActiveGrid.GridType - 1;
        int seed = GenerateSeed();
        SwitchToLevel(childLevel, seed);
    }

    [ContextMenu("Move up level")]
    public void MoveUpLevel()
    {
        Entity entity = EntityCommander.GetEntityInCommand();
        if (entity == null) return;

        HexGridManager.GridType parentLevel = ActiveGrid.GridType + 1;
        
        int seed = GenerateSeed();
        SwitchToLevel(parentLevel, seed);
    }
    
    public int GenerateSeed()
    {
        string seedString = MasterSeed.ToString();

        // The null checks are already handled by your Getters
        if (GetGlobalGalaxyCoords() is Vector2Int g) seedString += $"|G:{g.x},{g.y}";
        if (GetGlobalSystemCoords() is Vector2Int s) seedString += $"|S:{s.x},{s.y}";
        if (GetGlobalWorldCoords() is Vector2Int w) seedString += $"|W:{w.x},{w.y}";
        if (GetGlobalTerrainCoords() is Vector2Int t) seedString += $"|T:{t.x},{t.y}";

        return seedString.GetHashCode();
    }
}

[System.Serializable]
public struct NullableVector2Int
{
    public bool isSet;
    public Vector2Int coords;

    // Add this constructor
    public NullableVector2Int(Vector2Int coords, bool isSet)
    {
        this.coords = coords;
        this.isSet = isSet;
    }
}