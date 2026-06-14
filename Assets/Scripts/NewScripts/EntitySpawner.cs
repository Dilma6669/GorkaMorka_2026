using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Phase 8.2: UnitSpawner Class
// Purpose: Manages the instantiation and initial placement of Unit GameObjects onto SimpleHexGrids.
public class EntitySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [Tooltip("The prefab GameObject that has a Unit.cs component attached to it.")]
    public GameObject unitPrefab;
    public GameObject vehiclePrefab;
    public GameObject craftPrefab;
    
    public GameObject GroundedGridsContainer;
    public GameObject FloatingGridsContainer;
    
    [Tooltip("The SimpleHexGrid where units will be spawned by default.")]
    public SimpleHexGrid defaultSpawnGrid;

    [Tooltip("The axial coordinates on the defaultSpawnGrid where units will be spawned.")]
    public Vector2Int defaultSpawnCoordinates = Vector2Int.zero; // Default to grid center
    
    public List<UnitData> unitsToSpawn;

    private void Awake()
    {
        defaultSpawnGrid.OnGridReady += SpawnInitialModels;
    }
    
    
    private void Start()
    {
        // Subscribe to the event

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

        if (unitData.spawnGrid == null)
        {
            Debug.LogError("UnitSpawner: Cannot spawn unit, target grid is null!");
            return null;
        }

        if (!unitData.spawnGrid.IsValidCoordinates(unitData.spawnCoordinates))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {unitData.spawnCoordinates} are invalid on grid '{unitData.spawnGrid.name}'.");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
        
        
        float targetHeight = 0f;
        if (unitData.spawnGrid is GroundHexGrid groundGrid)
        {
            targetHeight = groundGrid.GetHexHeight(unitData.spawnCoordinates);
        }
        else 
        {
            // Fallback for non-ground grids
            targetHeight = unitData.spawnGrid.HexagonsInGrid[unitData.spawnCoordinates].Height;
        }

        // Now calculate position with the REAL height
        Vector3 spawnPos = unitData.spawnGrid.GetHexWorldPosition(unitData.spawnCoordinates, targetHeight);
        
        
        Debug.Log($"Spawning unit at: {unitData.spawnCoordinates} | Height: {targetHeight} | WorldPos: {spawnPos}");
        
        GameObject spawnedGameObject = Instantiate(unitPrefab, spawnPos, rotationToMatchGridCreation);
        
        // This might fuck things up
        spawnedGameObject.transform.SetParent(unitData.spawnGrid.EntityContainer.transform);
        
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
        if (vehicleData.spawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
            return null;
        }
        if (!vehicleData.spawnGrid.IsValidCoordinates(vehicleData.spawnCoordinates))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {vehicleData.spawnCoordinates} are invalid on grid '{vehicleData.spawnGrid.name}'.");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
        
        float targetHeight = 0f;
        if (vehicleData.spawnGrid is GroundHexGrid groundGrid)
        {
            targetHeight = groundGrid.GetHexHeight(vehicleData.spawnCoordinates);
        }
        else 
        {
            // Fallback for non-ground grids
            targetHeight = vehicleData.spawnGrid.HexagonsInGrid[vehicleData.spawnCoordinates].Height;
        }

        // Now calculate position with the REAL height
        Vector3 spawnPos = vehicleData.spawnGrid.GetHexWorldPosition(vehicleData.spawnCoordinates, targetHeight);
        
        
        Debug.Log($"Spawning unit at: {vehicleData.spawnCoordinates} | Height: {targetHeight} | WorldPos: {spawnPos}");
        
        GameObject spawnedGameObject = Instantiate(vehiclePrefab, spawnPos, rotationToMatchGridCreation);
       
        // Vehicles are anchored to the grid that they are spawning on
        spawnedGameObject.transform.SetParent(vehicleData.spawnGrid.EntityContainer.transform);

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

    // public Entity SpawnCraft(SimpleHexGrid grid, Vector2Int coords)
    // {
    //     if (unitPrefab == null)
    //     {
    //         Debug.LogError("EntitySpawner: Unit Prefab is not assigned!");
    //         return null;
    //     }
    //
    //     if (grid == null)
    //     {
    //         Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
    //         return null;
    //     }
    //
    //     if (!grid.IsValidCoordinates(coords))
    //     {
    //         Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {coords} are invalid on grid '{grid.name}'.");
    //         return null;
    //     }
    //
    //     // This rotates the car to face the positive X-axis upon creation.
    //     Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 0, 0);
    //     
    //     GameObject spawnedGameObject = Instantiate(craftPrefab, transform.position, rotationToMatchGridCreation);
    //    
    //     // Crafts are not anchored to any grid
    //     spawnedGameObject.transform.SetParent(FloatingGridsContainer.transform);
    //
    //     Entity newEntity = spawnedGameObject.GetComponent<Entity>();
    //     if (newEntity == null)
    //     {
    //         Debug.LogError(
    //             $"EntitySpawner: The assigned craftPrefab '{craftPrefab.name}' does not have a Craft.cs component!",
    //             craftPrefab);
    //         Destroy(spawnedGameObject); // Clean up if no Unit component
    //         return null;
    //     }
    //
    //     newEntity.Initialize(EntityType.Craft, grid, coords);
    //
    //     // --- Important for Multi-Unit Spawning ---
    //     // For now, the UnitCommander will just command the *last* unit spawned.
    //     // In a later step, we will implement unit selection (clicking) to pick which unit to command.
    //     if (entityCommander != null)
    //     {
    //         EntityCommander.SetEntityToCommand(newEntity); // Assign the newly spawned unit to the commander
    //     }
    //     else
    //     {
    //         Debug.LogWarning(
    //             "EntitySpawner: EntityCommander reference not set in Inspector. Spawned craft will not be assigned to commander automatically.");
    //     }
    //     // --- End Multi-Unit Spawning adjustment ---
    //
    //     newEntity.EntityGUID = EntityManager.RegisterEntity(newEntity);
    //     
    //     return newEntity;
    // }

    // --- Editor Test Button ---
    [ContextMenu("Spawn Default Unit")]
    void TestSpawnDefaultUnit()
    {
        if (defaultSpawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }

        UnitData entityData = ScriptableObject.CreateInstance<UnitData>();

        entityData.unitName = "Test Unit";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        entityData.spawnGrid = defaultSpawnGrid;
        entityData.spawnCoordinates = new Vector2Int(3,3);
        
        SpawnUnit(entityData);
    }
    
    [ContextMenu("Spawn Default Vehicle")]
    void TestSpawnDefaultVehicle()
    {
        if (defaultSpawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }
        
        VehicleData entityData = ScriptableObject.CreateInstance<VehicleData>();

        entityData.unitName = "Test Vehicle";
        entityData.maxHealth = 100;
        entityData.currentHealth = 100;
        entityData.baseMoveSpeed = 5;
        entityData.spawnGrid = defaultSpawnGrid;
        entityData.spawnCoordinates = new Vector2Int(-7,5);
        
        // This will now simply create a new unit each time the button is clicked.
        SpawnVehicle(entityData);
    }
    
    private void OnDestroy()
    {
        // Always unsubscribe when the spawner is destroyed
        defaultSpawnGrid.OnGridReady -= SpawnInitialModels;
    }
    
    private void SpawnInitialModels(SimpleHexGrid hexGrid)
    {
        Debug.Log("Spawner: Grid is ready, spawning units now!");
        TestSpawnDefaultUnit();
        TestSpawnDefaultVehicle();
    }

    
    public enum EntityType
    {
        Unit,
        Vehicle,
        Craft
    }
}