using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GameLevelManager : MonoBehaviour
{
    public static GameLevelManager Instance { get; private set; }
    
    private EntitySpawner entitySpawner;
    private HexOverlayManager hexOverlayManager;
    
    public static SimpleHexGridBase ActiveGrid { get; private set; }
    
    // The Inspector will see this field
    [SerializeField] private int _masterSeed = 99;
    
    [Header("Skybox materials")]
    public Material galaxySkybox;
    public Material systemSkybox;
    public Material worldSkybox;
    public Material terrainSkybox;
    
    [Header("Level settings")]
    public HexGridManager.GridType startingLevel;
    public HexGridManager.GridType currentLevel;
    public static HexGridManager.GridType CurrentLevel;

    // This is the static property the rest of your code uses
    public static int MasterSeed
    {
        get { return Instance._masterSeed; }
        private set => Instance._masterSeed = value;
    }

    [Header("Portal settings")]
    [SerializeField] private NullableVector2Int lastJumpedGalaxyCoords;
    [SerializeField] private NullableVector2Int lastJumpedSystemCoords;
    [SerializeField] private NullableVector2Int lastJumpedWorldCoords;
    [SerializeField] private NullableVector2Int lastJumpedTerrainCoords;
    
    [Header("Level gameObjects")]
    public List<LevelData> levels;
    

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
        //Debug.Log($"Setting lastJumpedGalaxyCoords.coords = {lastJumpedGalaxyCoords.coords} | coords.HasValue = {coords.HasValue}");

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.System);
        lastJumpedSystemCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedSystemCoords.coords = coords.Value;
        //Debug.Log($"Setting lastJumpedSystemCoords.coords = {lastJumpedSystemCoords.coords} | coords.HasValue = {coords.HasValue}");

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.World);
        lastJumpedWorldCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedWorldCoords.coords = coords.Value;
        //Debug.Log($"Setting lastJumpedWorldCoords.coords = {lastJumpedWorldCoords.coords} | coords.HasValue = {coords.HasValue}");

        coords = entityData.GetLastJumpLevelCoords(HexGridManager.GridType.Terrain);
        lastJumpedTerrainCoords.isSet = coords.HasValue;
        if (coords.HasValue) lastJumpedTerrainCoords.coords = coords.Value;
        //Debug.Log($"Setting lastJumpedTerrainCoords.coords = {lastJumpedTerrainCoords.coords} | coords.HasValue = {coords.HasValue}");
    }
    
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
        
        SetGlobalLastJumpedCoords(entityData);

        SwitchToLevel(startingLevel);
    }

    public void SwitchToLevel(HexGridManager.GridType targetLevel)
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
                                level = HexGridManager.GridType.Terrain,
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
                                level = HexGridManager.GridType.Terrain,
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
                                level = HexGridManager.GridType.Terrain,
                                coords = EntityCommander.GetEntityInCommand().CurrentGridCoordinates
                            });
                        }
                        DataManager.UpdateData(entityData.entityGUID, entityData);
                    }
                    SetGlobalLastJumpedCoords(entityData);
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

        ActiveGrid.GridGUID = GenerateSeed();
        if (ActiveGrid.Populater != null)
        {
            ActiveGrid.Populater.ClearAll();
        }
        
        Debug.Log($"Switched to: {targetLevel}");

        ApplySkybox(targetLevel);
        
        GenerateNewGridOnActiveGrid(ActiveGrid.GridGUID);
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

        SwitchToLevel(childLevel);
    }

    [ContextMenu("Move up level")]
    public void MoveUpLevel()
    {
        Entity entity = EntityCommander.GetEntityInCommand();
        if (entity == null) return;

        HexGridManager.GridType parentLevel = ActiveGrid.GridType + 1;
        
        int seed = GenerateSeed();
        SwitchToLevel(parentLevel);
    }
    
    public int GenerateSeed()
    {
        string seedString = MasterSeed.ToString();

        Debug.Log($"fuck GetGlobalGalaxyCoords() = {GetGlobalGalaxyCoords()}");
        Debug.Log($"fuck GetGlobalSystemCoords() = {GetGlobalSystemCoords()}");
        Debug.Log($"fuck GetGlobalWorldCoords() = {GetGlobalWorldCoords()}");
        Debug.Log($"fuck GetGlobalTerrainCoords() = {GetGlobalTerrainCoords()}");

        // The null checks are already handled by your Getters
        if (GetGlobalGalaxyCoords() is Vector2Int g) seedString += $"|G:{g.x},{g.y}";
        if (GetGlobalSystemCoords() is Vector2Int s) seedString += $"|S:{s.x},{s.y}";
        if (GetGlobalWorldCoords() is Vector2Int w) seedString += $"|W:{w.x},{w.y}";
        if (GetGlobalTerrainCoords() is Vector2Int t) seedString += $"|T:{t.x},{t.y}";

        Debug.Log($"GENERATOR: Created Seed = {seedString}");
        
        return seedString.GetHashCode();
    }
    
    public void ApplySkybox(HexGridManager.GridType levelType)
    {
        switch (levelType)
        {
            case HexGridManager.GridType.None:
            case HexGridManager.GridType.Interior:
                return;
            case HexGridManager.GridType.Galaxy:
                RenderSettings.skybox = galaxySkybox;
                break;
            case HexGridManager.GridType.System:
                RenderSettings.skybox = systemSkybox;
                break;
            case HexGridManager.GridType.World:
                RenderSettings.skybox = worldSkybox;
                break;
            case HexGridManager.GridType.Terrain:
                RenderSettings.skybox = terrainSkybox;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(levelType), levelType, null);
        }
        
        // This forces Unity to refresh the ambient light based on the new skybox
        DynamicGI.UpdateEnvironment();
    }
}

[System.Serializable]
public struct LevelData
{
    public HexGridManager.GridType type;
    public GameObject levelRoot;
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