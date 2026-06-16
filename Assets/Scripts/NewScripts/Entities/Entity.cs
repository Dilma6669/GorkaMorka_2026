using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

// Phase 9.2 (Updated): Unit Class
// Purpose: Represents a single movable unit in the game,
// holding its current grid position, managing its visual snap to hexes,
// and orchestrating its movement via an attached PathMover.
public class Entity : MonoBehaviour
{
    [Header("Entity Data")]
    public string UnitName;
    public int MaxHealth;
    public int CurrentHealth;
    public float BaseMoveSpeed;
    public EntitySpawner.EntityType EntityType;
    public string EntityGUID;
    
    [FormerlySerializedAs("CurrentGrid")]
    [Header("Grid State")]
    [Tooltip("The SimpleHexGrid this unit is currently occupying.")]
    public SimpleHexGridBase currentGridBase;
    [Tooltip("The axial coordinates of the hex this unit is currently occupying.")]
    public Vector2Int CurrentGridCoordinates;
    
    [Header("Movement Components")]
    [Tooltip("Reference to the PathMover component on this GameObject.")]
    public IEntityPathMover EntityPathMover; // Reference to the PathMover

    [FormerlySerializedAs("unitHeightOffset")]
    [Header("Visual Offset")]
    [Tooltip("The vertical offset from the center of the hex to the unit's pivot point. Adjust this so the unit sits correctly on the hex surface.")]
    public float entityHeightOffset = 0.5f; // Default offset, adjust in Inspector per unit type
    
    public float CurrentGroundY { get; set; }
    
    void Awake()
    {
        // Ensure unitPathMover is assigned, either manually or found automatically
        if (EntityPathMover == null)
        {
            EntityPathMover = GetComponent<IEntityPathMover>();
            if (EntityPathMover == null)
            {
                Debug.LogError($"Unit '{name}': No PathMover component found! Unit will not be able to move.", this);
            }
        }
    }
    
    void Update() {
        if (currentGridBase != null) {
            float surfaceY = currentGridBase.GetHexTopSurfaceY(CurrentGridCoordinates);
            Debug.DrawLine(transform.position, new Vector3(transform.position.x, surfaceY, transform.position.z), Color.green);
        }
    }

    /// <summary>
    /// Initializes the unit's starting position and grid state.
    /// This should be called immediately after instantiating a unit.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid to spawn on.</param>
    /// <param name="coords">The axial coordinates on the grid.</param>
    public virtual void Initialize(EntitySpawner.EntityType entityType, EntityData entityData)
    {
        if (entityData.spawnGridBase == null)
        {
            Debug.LogError($"Unit '{name}': Attempted to initialize with a null grid.", this);
            return;
        }
        if (!entityData.spawnGridBase.IsValidCoordinates(entityData.spawnCoordinates))
        {
            Debug.LogWarning($"Unit '{name}': Attempted to initialize on invalid coordinates {entityData.spawnCoordinates} on grid '{entityData.spawnGridBase.name}'.", this);
            return;
        }

        EntityType = entityType;
        UnitName = entityData.unitName;
        MaxHealth = entityData.maxHealth;
        CurrentHealth = entityData.maxHealth;
        BaseMoveSpeed = entityData.baseMoveSpeed;
        
        SnapToHex(entityData.spawnGridBase, entityData.spawnCoordinates); // Snap to the initial position
        
        // Ensure we specifically look for the component
        EntityPathMover = GetComponent<IEntityPathMover>();

        if (EntityPathMover == null)
        {
            Debug.LogError($"Unit '{name}': PathMover component not found on this object!", this);
        }
            
        Debug.Log($"Unit '{name}' initialized on grid '{currentGridBase.name}' at {CurrentGridCoordinates}.");
    }

    public virtual void SnapToHex(SimpleHexGridBase gridBase, Vector2Int coords)
    {
        currentGridBase = gridBase;
        CurrentGridCoordinates = coords;
        transform.SetParent(gridBase.EntityContainer.transform);

        HexData hexData = gridBase.GetHexData(CurrentGridCoordinates);
        
        // Get the Y level of the top surface of the hex
        Vector3 hexSurfacePosition = gridBase.GetHexTopSurfacePosition(coords, hexData.Height);

        CurrentGroundY = hexSurfacePosition.y + entityHeightOffset;
        
        Debug.Log($"Snapping entity of type: {EntityType} to = {hexSurfacePosition}");
    
        // Apply the surface Y + the unit's "standing height" 
        // (Use a small offset for the unit's feet relative to the top of the hex)
        transform.position = new Vector3(
            hexSurfacePosition.x, 
            CurrentGroundY, 
            hexSurfacePosition.z
        );
    }

    /// <summary>
    /// Commands this unit to move along a given path.
    /// Delegates the actual movement logic to the attached PathMover.
    /// </summary>
    /// <param name="path">The list of PathNodes defining the path.</param>
    public void MoveUnitAlongPath(List<PathNode> path)
    {
        if (EntityPathMover != null)
        {
            EntityPathMover.StartMoving(path);
        }
        else
        {
            Debug.LogError($"Unit '{name}': Cannot move, no PathMover component assigned or found!", this);
        }
    }
    
    
    public void SetEntityToNewGrid(SimpleHexGridBase gridBase)
    {
        currentGridBase = gridBase;
    }
    
    public virtual void EntitySelected(bool isSelected)
    {

    }
}