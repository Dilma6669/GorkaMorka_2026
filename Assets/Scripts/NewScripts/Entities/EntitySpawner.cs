using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

// Phase 8.2: UnitSpawner Class
// Purpose: Manages the instantiation and initial placement of Unit GameObjects onto SimpleHexGrids.
public class EntitySpawner : MonoBehaviour
{
    private GameLevelManager gameLevelManager;
    
    [Header("Spawner Settings")]
    [Tooltip("The prefab GameObject that has a Unit.cs component attached to it.")]
    public GameObject unitPrefab;
    public GameObject vehiclePrefab;
    public GameObject craftPrefab;
    
    public GameObject GroundedGridsContainer;
    public GameObject FloatingGridsContainer;

    [Tooltip("The axial coordinates on the defaultSpawnGrid where units will be spawned.")]
    public Vector2Int defaultSpawnCoordinates = Vector2Int.zero; // Default to grid center
    
    public List<UnitData> unitsToSpawn;
    
    
    private void Awake()
    {
        gameLevelManager = GetComponent<GameLevelManager>();
    }
    
    public void SpawnAllUnits()
    {
        foreach (var data in unitsToSpawn)
        {
            SpawnUnit(data);
        }
    }

    /// <summary>
    /// Spawns a unit at the specified grid and coordinates.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid to spawn the unit on.</param>
    /// <param name="coords">The axial coordinates within that grid.</param>
    /// <returns>The instantiated Unit component, or null if spawning failed.</returns>
    public Entity SpawnUnit(UnitData unitData)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("UnitSpawner: Unit Prefab is not assigned!");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
        
        float targetHeight = 0f;
        if (GameLevelManager.ActiveGrid is SimpleHexGridTerrain groundGrid)
        {
            targetHeight = groundGrid.GetHexHeight((Vector2Int)unitData.GetLevelCoords(GameLevelManager.CurrentLevel)!);
        }
        else 
        {
            // Fallback for non-ground grids
            targetHeight = GameLevelManager.ActiveGrid.HexagonsInGrid[(Vector2Int)unitData.GetLevelCoords(GameLevelManager.CurrentLevel)!].Height;
        }

        // Now calculate position with the REAL height
        Vector3 spawnPos = GameLevelManager.ActiveGrid.GetHexWorldPosition((Vector2Int)unitData.GetLevelCoords(GameLevelManager.CurrentLevel)!, targetHeight);
        
        
        Debug.Log($"Spawning unit at: {unitData.GetLevelCoords(GameLevelManager.CurrentLevel)} | Height: {targetHeight} | WorldPos: {spawnPos}");
        
        GameObject spawnedGameObject = Instantiate(unitPrefab, spawnPos, rotationToMatchGridCreation);
        
        spawnedGameObject.transform.SetParent(GameLevelManager.ActiveGrid.EntityContainer.transform);
        
        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError(
                $"EntitySpawner: The assigned unitPrefab '{unitPrefab.name}' does not have a Unit.cs component!",
                unitPrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(unitData);

        return newEntity;
    }

    // public Entity SpawnVehicle(VehicleData vehicleData)
    // {
    //     if (unitPrefab == null)
    //     {
    //         Debug.LogError("EntitySpawner: Unit Prefab is not assigned!");
    //         return null;
    //     }
    //     if (vehicleData.spawnGridBase == null)
    //     {
    //         Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
    //         return null;
    //     }
    //     if (!vehicleData.spawnGridBase.IsValidCoordinates(vehicleData.spawnCoordinates))
    //     {
    //         Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {vehicleData.spawnCoordinates} are invalid on grid '{vehicleData.spawnGridBase.name}'.");
    //         return null;
    //     }
    //     
    //     // This rotates the car to face the positive X-axis upon creation.
    //     Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
    //     
    //     float targetHeight = 0f;
    //     if (vehicleData.spawnGridBase is SimpleHexGridTerrain groundGrid)
    //     {
    //         targetHeight = groundGrid.GetHexHeight(vehicleData.spawnCoordinates);
    //     }
    //     else 
    //     {
    //         // Fallback for non-ground grids
    //         targetHeight = vehicleData.spawnGridBase.HexagonsInGrid[vehicleData.spawnCoordinates].Height;
    //     }
    //
    //     // Now calculate position with the REAL height
    //     Vector3 spawnPos = vehicleData.spawnGridBase.GetHexWorldPosition(vehicleData.spawnCoordinates, targetHeight);
    //     
    //     
    //     Debug.Log($"Spawning unit at: {vehicleData.spawnCoordinates} | Height: {targetHeight} | WorldPos: {spawnPos}");
    //     
    //     GameObject spawnedGameObject = Instantiate(vehiclePrefab, spawnPos, rotationToMatchGridCreation);
    //    
    //     // Vehicles are anchored to the grid that they are spawning on
    //     spawnedGameObject.transform.SetParent(vehicleData.spawnGridBase.EntityContainer.transform);
    //
    //     Entity newEntity = spawnedGameObject.GetComponent<Entity>();
    //     if (newEntity == null)
    //     {
    //         Debug.LogError($"EntitySpawner: The assigned vehiclePrefab '{vehiclePrefab.name}' does not have a Vehicle.cs component!", vehiclePrefab);
    //         Destroy(spawnedGameObject); // Clean up if no Unit component
    //         return null;
    //     }
    //
    //     newEntity.Initialize(EntityType.Vehicle, vehicleData);
    //     
    //     
    //     newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);
    //     DataManager.RegisterData(newEntity.EntityGUID, vehicleData);
    //
    //     return newEntity;
    // }

    // public Entity SpawnCraft(CraftData craftData)
    // {
    //     if (craftPrefab == null)
    //     {
    //         Debug.LogError("EntitySpawner: Craft Prefab is not assigned!");
    //         return null;
    //     }
    //
    //     // Standard safety checks
    //     if (craftData.spawnGridBase == null)
    //     {
    //         Debug.LogError("EntitySpawner: Cannot spawn craft, target grid is null!");
    //         return null;
    //     }
    //
    //     // Calculate spawn position (using the Craft's intended landing coordinates)
    //     float targetHeight = craftData.spawnGridBase.HexagonsInGrid[craftData.spawnCoordinates].Height;
    //     Vector3 spawnPos = craftData.spawnGridBase.GetHexWorldPosition(craftData.spawnCoordinates, targetHeight);
    //
    //     Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
    //
    //     // Spawn it (Parent it to your FloatingGridsContainer)
    //     GameObject spawnedGameObject = Instantiate(craftPrefab, spawnPos, rotationToMatchGridCreation);
    //     spawnedGameObject.transform.SetParent(FloatingGridsContainer.transform);
    //
    //     Entity newEntity = spawnedGameObject.GetComponent<Entity>();
    //     if (newEntity == null)
    //     {
    //         Debug.LogError($"EntitySpawner: Prefab '{craftPrefab.name}' is missing an Entity component!");
    //         Destroy(spawnedGameObject);
    //         return null;
    //     }
    //
    //     newEntity.Initialize(EntityType.Craft, craftData);
    //     newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);
    //     DataManager.RegisterData(newEntity.EntityGUID, craftData);
    //
    //     return newEntity;
    // }

    public EntityData CreateUnitDataOnActiveGrid()
    {
        if (gameLevelManager == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return null;
        }

        UnitData entityData = ScriptableObject.CreateInstance<UnitData>();

        entityData.entityName = "Test Unit";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        
        entityData.SetLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.Galaxy,
            coords = Vector2Int.zero
        });
        entityData.SetLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.System,
            coords = null
        });
        entityData.SetLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.World,
            coords = null
        });
        entityData.SetLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.Terrain,
            coords = null
        });
        
        entityData.SetLastJumpLevelCoords(new LevelPositionPair()
        {
            level = HexGridManager.GridType.Galaxy,
            coords = Vector2Int.zero
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

        entityData.entityGUID = DataManager.RegisterData(entityData);
        return entityData;
    }
    
    public void SpawnModelsOnGrid(SimpleHexGridBase hexGridBase)
    {
        Debug.Log($"SpawnModelsOnGrid");
        
        List<EntityData> dataListToUpdate = new List<EntityData>();
        
        foreach (var data in DataManager.globalDataRegistry.Values)
        {
            List<LevelPositionPair> levelPositions = data.levelCoords;
            
            LevelPositionPair cachedEntityTerrainCoords = levelPositions.Find(p => p.level == HexGridManager.GridType.Terrain);
            LevelPositionPair cachedEntityWorldCoords = levelPositions.Find(p => p.level == HexGridManager.GridType.World);
            LevelPositionPair cachedEntitySystemCoords = levelPositions.Find(p => p.level == HexGridManager.GridType.System);
            LevelPositionPair cachedEntityGalaxyCoords = levelPositions.Find(p => p.level == HexGridManager.GridType.Galaxy);

            switch (hexGridBase.GridType)
            {
                case HexGridManager.GridType.None:
                    break;
                case HexGridManager.GridType.Interior:
                    break;
                case HexGridManager.GridType.Terrain:
                {
                    // Try load cached coords
                    Vector2Int coordsToSpawn = cachedEntityTerrainCoords.coords != null
                        ? (Vector2Int)cachedEntityTerrainCoords.coords
                        : Vector2Int.zero;

                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.Terrain,
                        coords = coordsToSpawn
                    });
                    dataListToUpdate.Add(data);

                    break;
                }
                case HexGridManager.GridType.World:
                {
                    // Try load cached coords
                    Vector2Int coordsToSpawn = cachedEntityWorldCoords.coords != null
                        ? (Vector2Int)cachedEntityWorldCoords.coords
                        : Vector2Int.zero;

                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.World,
                        coords = coordsToSpawn
                    });
                    dataListToUpdate.Add(data);
                    
                    break;
                }
                case HexGridManager.GridType.System:
                {
                    // Try load cached coords
                    Vector2Int coordsToSpawn = cachedEntitySystemCoords.coords != null
                        ? (Vector2Int)cachedEntitySystemCoords.coords
                        : Vector2Int.zero;

                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.System,
                        coords = coordsToSpawn
                    });
                    dataListToUpdate.Add(data);

                    break;
                }
                case HexGridManager.GridType.Galaxy:
                {
                    Vector2Int coordsToSpawn = cachedEntityGalaxyCoords.coords != null
                        ? (Vector2Int)cachedEntityGalaxyCoords.coords
                        : Vector2Int.zero;

                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.Galaxy,
                        coords = coordsToSpawn
                    });
                    dataListToUpdate.Add(data);
                    
                    break;
                }
            }
        }

        foreach (var data in dataListToUpdate)
        {
            DataManager.UpdateData(data.entityGUID, data);
            
            switch (data.entityType)
            {
                case EntityType.Unit:
                    SpawnUnit(data as UnitData);
                    break;
                case EntityType.Vehicle:
                   // SpawnVehicle(data as VehicleData);
                    break;
                case EntityType.Craft:
                    // SpawnCraft(data as CraftData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    public void DestroyAllActiveEntities()
    {
        // Destroy all current GameObjects in the scene
        foreach (var entry in EntityManager.GetAllEntities())
        {
            if (entry.Value != null)
            {
                Destroy(entry.Value.gameObject);
            }
        }
    
        // Clear the tracking list
        EntityManager.ClearAll(); 
    }
    
    public enum EntityType
    {
        Unit,
        Vehicle,
        Craft
    }
}