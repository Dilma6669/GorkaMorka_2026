using System;
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
    
    private EntityCommander entityCommander;

    private void Awake()
    {
        entityCommander = GetComponent<EntityCommander>();
    }

    private void Start()
    {
        //TestSpawnDefaultUnit();
    }

    /// <summary>
    /// Spawns a unit at the specified grid and coordinates.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid to spawn the unit on.</param>
    /// <param name="coords">The axial coordinates within that grid.</param>
    /// <returns>The instantiated Unit component, or null if spawning failed.</returns>
    public Entity SpawnUnit(SimpleHexGrid grid, Vector2Int coords)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("UnitSpawner: Unit Prefab is not assigned!");
            return null;
        }

        if (grid == null)
        {
            Debug.LogError("UnitSpawner: Cannot spawn unit, target grid is null!");
            return null;
        }

        if (!grid.IsValidCoordinates(coords))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {coords} are invalid on grid '{grid.name}'.");
            return null;
        }

        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 90, 0);
        
        GameObject spawnedGameObject = Instantiate(unitPrefab, transform.position, rotationToMatchGridCreation);
        
        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError(
                $"EntitySpawner: The assigned unitPrefab '{unitPrefab.name}' does not have a Unit.cs component!",
                unitPrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(EntityType.Unit, grid, coords);

        // --- Important for Multi-Unit Spawning ---
        // For now, the UnitCommander will just command the *last* unit spawned.
        // In a later step, we will implement unit selection (clicking) to pick which unit to command.
        if (entityCommander != null)
        {
            entityCommander.entityToCommand = newEntity; // Assign the newly spawned unit to the commander
            Debug.Log($"EntitySpawner: Assigned '{newEntity.name}' to EntityCommander.");
        }
        else
        {
            Debug.LogWarning(
                "EntitySpawner: EntityCommander reference not set in Inspector. Spawned unit will not be assigned to commander automatically.");
        }
        // --- End Multi-Unit Spawning adjustment ---

        return newEntity;
    }

    public Entity SpawnVehicle(SimpleHexGrid grid, Vector2Int coords)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("EntitySpawner: Unit Prefab is not assigned!");
            return null;
        }
        if (grid == null)
        {
            Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
            return null;
        }
        if (!grid.IsValidCoordinates(coords))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {coords} are invalid on grid '{grid.name}'.");
            return null;
        }
        
        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 90, 0);

        GameObject spawnedGameObject = Instantiate(vehiclePrefab, transform.position, rotationToMatchGridCreation);
       
        // Vehicles are anchored to the grid that they are spawning on
        spawnedGameObject.transform.SetParent(grid.EntityContainer.transform);

        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError($"EntitySpawner: The assigned vehiclePrefab '{vehiclePrefab.name}' does not have a Vehicle.cs component!", vehiclePrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(EntityType.Vehicle, grid, coords);

        // --- Important for Multi-Unit Spawning ---
        // For now, the UnitCommander will just command the *last* unit spawned.
        // In a later step, we will implement unit selection (clicking) to pick which unit to command.
        if (entityCommander != null)
        {
            entityCommander.entityToCommand = newEntity; // Assign the newly spawned unit to the commander
            Debug.Log($"EntitySpawner: Assigned '{newEntity.name}' to EntityCommander.");
        }
        else
        {
            Debug.LogWarning("EntitySpawner: EntityCommander reference not set in Inspector. Spawned vehicle will not be assigned to commander automatically.");
        }
        // --- End Multi-Unit Spawning adjustment ---

        return newEntity;
    }

    public Entity SpawnCraft(SimpleHexGrid grid, Vector2Int coords)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("EntitySpawner: Unit Prefab is not assigned!");
            return null;
        }

        if (grid == null)
        {
            Debug.LogError("EntitySpawner: Cannot spawn unit, target grid is null!");
            return null;
        }

        if (!grid.IsValidCoordinates(coords))
        {
            Debug.LogError($"EntitySpawner: Cannot spawn unit, coordinates {coords} are invalid on grid '{grid.name}'.");
            return null;
        }

        // This rotates the car to face the positive X-axis upon creation.
        Quaternion rotationToMatchGridCreation = Quaternion.Euler(0, 90, 0);
        
        GameObject spawnedGameObject = Instantiate(craftPrefab, transform.position, rotationToMatchGridCreation);
       
        // Crafts are not anchored to any grid
        spawnedGameObject.transform.SetParent(FloatingGridsContainer.transform);

        Entity newEntity = spawnedGameObject.GetComponent<Entity>();
        if (newEntity == null)
        {
            Debug.LogError(
                $"EntitySpawner: The assigned craftPrefab '{craftPrefab.name}' does not have a Craft.cs component!",
                craftPrefab);
            Destroy(spawnedGameObject); // Clean up if no Unit component
            return null;
        }

        newEntity.Initialize(EntityType.Craft, grid, coords);

        // --- Important for Multi-Unit Spawning ---
        // For now, the UnitCommander will just command the *last* unit spawned.
        // In a later step, we will implement unit selection (clicking) to pick which unit to command.
        if (entityCommander != null)
        {
            entityCommander.entityToCommand = newEntity; // Assign the newly spawned unit to the commander
            Debug.Log($"EntitySpawner: Assigned '{newEntity.name}' to EntityCommander.");
        }
        else
        {
            Debug.LogWarning(
                "EntitySpawner: EntityCommander reference not set in Inspector. Spawned craft will not be assigned to commander automatically.");
        }
        // --- End Multi-Unit Spawning adjustment ---

        return newEntity;
    }

    // --- Editor Test Button ---
    [ContextMenu("Spawn Default Unit")]
    void TestSpawnDefaultUnit()
    {
        if (defaultSpawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }
        // This will now simply create a new unit each time the button is clicked.
        SpawnUnit(defaultSpawnGrid, defaultSpawnCoordinates);
       // HexGridManager.Instance.UpdateUnwalkableHexagonsOnAllGrids();
    }
    
    [ContextMenu("Spawn Default Vehicle")]
    void TestSpawnDefaultVehicle()
    {
        if (defaultSpawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }
        // This will now simply create a new unit each time the button is clicked.
        SpawnVehicle(defaultSpawnGrid, defaultSpawnCoordinates);
        HexGridManager.Instance.UpdateUnwalkableHexagonsOnAllGrids();
    }
    
    [ContextMenu("Spawn Default Craft")]
    void TestSpawnDefaultCraft()
    {
        if (defaultSpawnGrid == null)
        {
            Debug.LogError("EntitySpawner: Default Spawn Grid is not set for TestSpawnDefaultUnit!");
            return;
        }
        // This will now simply create a new unit each time the button is clicked.
        SpawnCraft(defaultSpawnGrid, defaultSpawnCoordinates);
        HexGridManager.Instance.UpdateUnwalkableHexagonsOnAllGrids();
    }
    
    public enum EntityType
    {
        Unit,
        Vehicle,
        Craft
    }
}