using System;
using System.Collections;
using System.Collections.Generic;
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
    
    
    private void Start()
    {
        gameLevelManager = GetComponent<GameLevelManager>();
        // needs to be in start because GameLevelManager needs to assign correct grid first.
        gameLevelManager.ActiveGrid.OnGridReady += SpawnInitialModels;
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

        if (unitData.spawnGridBase == null)
        {
            Debug.LogError("UnitSpawner: Cannot spawn unit, target grid is null!");
            return null;
        }

        if (!unitData.spawnGridBase.IsValidCoordinates(unitData.spawnCoordinates))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {unitData.spawnCoordinates} are invalid on grid '{unitData.spawnGridBase.name}'.");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
        
        
        float targetHeight = 0f;
        if (unitData.spawnGridBase is SimpleHexGridTerrain groundGrid)
        {
            targetHeight = groundGrid.GetHexHeight(unitData.spawnCoordinates);
        }
        else 
        {
            // Fallback for non-ground grids
            targetHeight = unitData.spawnGridBase.HexagonsInGrid[unitData.spawnCoordinates].Height;
        }

        // Now calculate position with the REAL height
        Vector3 spawnPos = unitData.spawnGridBase.GetHexWorldPosition(unitData.spawnCoordinates, targetHeight);
        
        
        Debug.Log($"Spawning unit at: {unitData.spawnCoordinates} | Height: {targetHeight} | WorldPos: {spawnPos}");
        
        GameObject spawnedGameObject = Instantiate(unitPrefab, spawnPos, rotationToMatchGridCreation);
        
        // This might fuck things up
        spawnedGameObject.transform.SetParent(unitData.spawnGridBase.EntityContainer.transform);
        
        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError(
                $"EntitySpawner: The assigned unitPrefab '{unitPrefab.name}' does not have a Unit.cs component!",
                unitPrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(EntityType.Unit, unitData);

        // --- Important for Multi-Unit Spawning ---
        // For now, the UnitCommander will just command the *last* unit spawned.
        // In a later step, we will implement unit selection (clicking) to pick which unit to command.
        EntitySelectionManager.SelectUnit(newEntity);

        // --- End Multi-Unit Spawning adjustment ---
        
        newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);

        return newEntity;
    }

    public Entity SpawnVehicle(VehicleData vehicleData)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("EntitySpawner: Unit Prefab is not assigned!");
            return null;
        }
        if (vehicleData.spawnGridBase == null)
        {
            Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
            return null;
        }
        if (!vehicleData.spawnGridBase.IsValidCoordinates(vehicleData.spawnCoordinates))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {vehicleData.spawnCoordinates} are invalid on grid '{vehicleData.spawnGridBase.name}'.");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
        
        float targetHeight = 0f;
        if (vehicleData.spawnGridBase is SimpleHexGridTerrain groundGrid)
        {
            targetHeight = groundGrid.GetHexHeight(vehicleData.spawnCoordinates);
        }
        else 
        {
            // Fallback for non-ground grids
            targetHeight = vehicleData.spawnGridBase.HexagonsInGrid[vehicleData.spawnCoordinates].Height;
        }

        // Now calculate position with the REAL height
        Vector3 spawnPos = vehicleData.spawnGridBase.GetHexWorldPosition(vehicleData.spawnCoordinates, targetHeight);
        
        
        Debug.Log($"Spawning unit at: {vehicleData.spawnCoordinates} | Height: {targetHeight} | WorldPos: {spawnPos}");
        
        GameObject spawnedGameObject = Instantiate(vehiclePrefab, spawnPos, rotationToMatchGridCreation);
       
        // Vehicles are anchored to the grid that they are spawning on
        spawnedGameObject.transform.SetParent(vehicleData.spawnGridBase.EntityContainer.transform);

        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError($"EntitySpawner: The assigned vehiclePrefab '{vehiclePrefab.name}' does not have a Vehicle.cs component!", vehiclePrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(EntityType.Vehicle, vehicleData);
        
        
        newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);

        return newEntity;
    }

    public Entity SpawnCraft(CraftData craftData)
    {
        if (craftPrefab == null)
        {
            Debug.LogError("EntitySpawner: Craft Prefab is not assigned!");
            return null;
        }

        // Standard safety checks
        if (craftData.spawnGridBase == null)
        {
            Debug.LogError("EntitySpawner: Cannot spawn craft, target grid is null!");
            return null;
        }

        // Calculate spawn position (using the Craft's intended landing coordinates)
        float targetHeight = craftData.spawnGridBase.HexagonsInGrid[craftData.spawnCoordinates].Height;
        Vector3 spawnPos = craftData.spawnGridBase.GetHexWorldPosition(craftData.spawnCoordinates, targetHeight);
    
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
    
        // Spawn it (Parent it to your FloatingGridsContainer)
        GameObject spawnedGameObject = Instantiate(craftPrefab, spawnPos, rotationToMatchGridCreation);
        spawnedGameObject.transform.SetParent(FloatingGridsContainer.transform);

        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError($"EntitySpawner: Prefab '{craftPrefab.name}' is missing an Entity component!");
            Destroy(spawnedGameObject);
            return null;
        }

        newEntity.Initialize(EntityType.Craft, craftData);
        newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);

        return newEntity;
    }

    // --- Editor Test Button ---
    [ContextMenu("Spawn Default Unit")]
    void TestSpawnDefaultUnit()
    {
        if (gameLevelManager == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }

        UnitData entityData = ScriptableObject.CreateInstance<UnitData>();

        entityData.unitName = "Test Unit";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        entityData.spawnGridBase = gameLevelManager.ActiveGrid;
        entityData.spawnCoordinates = new Vector2Int(-4,7);
        
        SpawnUnit(entityData);
    }
    
    [ContextMenu("Spawn Default Vehicle")]
    void TestSpawnDefaultVehicle()
    {
        if (gameLevelManager == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }
        
        VehicleData entityData = ScriptableObject.CreateInstance<VehicleData>();

        entityData.unitName = "Test Vehicle";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        entityData.spawnGridBase = gameLevelManager.ActiveGrid;
        entityData.spawnCoordinates = new Vector2Int(-17,10);
        
        // This will now simply create a new unit each time the button is clicked.
        SpawnVehicle(entityData);
    }
    
    [ContextMenu("Spawn Default Craft")]
    void TestSpawnDefaultCraft()
    {
        // Ensure you have an instance of CraftData, perhaps assigned in the inspector or created here
        CraftData entityData = ScriptableObject.CreateInstance<CraftData>();
        entityData.unitName = "Test Craft";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        entityData.spawnGridBase = gameLevelManager.ActiveGrid;
        entityData.spawnCoordinates = new Vector2Int(0,0);
    
        SpawnCraft(entityData);
    }
    
    private void OnDestroy()
    {
        // Always unsubscribe when the spawner is destroyed
        gameLevelManager.ActiveGrid.OnGridReady -= SpawnInitialModels;
    }
    
    private void SpawnInitialModels(SimpleHexGridBase hexGridBase)
    {
        Debug.Log("Spawner: Grid is ready, spawning units now!");
        TestSpawnDefaultUnit();
        TestSpawnDefaultVehicle();
        TestSpawnDefaultCraft();
    }

    
    public enum EntityType
    {
        Unit,
        Vehicle,
        Craft
    }
}