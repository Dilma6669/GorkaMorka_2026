using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

// Phase 9.2 (Updated): Unit Class
// Purpose: Represents a single movable unit in the game,
// holding its current grid position, managing its visual snap to hexes,
// and orchestrating its movement via an attached PathMover.
public abstract class Entity : MonoBehaviour
{
    [Header("Entity Data")]
    public string EntityGUID;
    public string EntityName;
    public int MaxHealth;
    public int CurrentHealth;
    public float BaseMoveSpeed;
    public EntitySpawner.EntityType EntityType;
    public List<LevelPositionPair> LevelCoords = new List<LevelPositionPair>();
    public List<LevelPositionPair> LastJumpCoords = new List<LevelPositionPair>();
    
    [SerializeField] private NullableVector2Int LevelGalaxyCoords;
    [SerializeField] private NullableVector2Int LevelSystemCoords;
    [SerializeField] private NullableVector2Int LevelWorldCoords;
    [SerializeField] private NullableVector2Int LevelTerrainCoords;
    
    [SerializeField] private NullableVector2Int LastJumpGalaxyCoords;
    [SerializeField] private NullableVector2Int LastJumpSystemCoords;
    [SerializeField] private NullableVector2Int LastJumpWorldCoords;
    [SerializeField] private NullableVector2Int LastJumpTerrainCoords;

    private void UpdateEntityLevelCoords()
    {
        if (DataManager.TryGetData(EntityGUID, out UnitData data))
        {
            Vector2Int? coords = null;
            
            coords = data.GetLevelCoords(HexGridManager.GridType.Galaxy);
            LevelGalaxyCoords.isSet = coords != null;
            if (coords.HasValue) LevelGalaxyCoords.coords = coords.Value;

            coords = data.GetLevelCoords(HexGridManager.GridType.System);
            LevelSystemCoords.isSet = coords != null;
            if (coords.HasValue) LevelSystemCoords.coords = coords.Value;

            coords = data.GetLevelCoords(HexGridManager.GridType.World);
            LevelWorldCoords.isSet = coords != null;
            if (coords.HasValue) LevelWorldCoords.coords = coords.Value;

            coords = data.GetLevelCoords(HexGridManager.GridType.Terrain);
            LevelTerrainCoords.isSet = coords != null;
            if (coords.HasValue) LevelTerrainCoords.coords = coords.Value;
        }
    }
    
    private void UpdateEntityLastJumpCoords()
    {
        if (DataManager.TryGetData(EntityGUID, out UnitData data))
        {
            Vector2Int? coords = null;
            
            coords = data.GetLastJumpLevelCoords(HexGridManager.GridType.Galaxy);
            LastJumpGalaxyCoords.isSet = coords != null;
            if (coords.HasValue) LastJumpGalaxyCoords.coords = coords.Value;

            coords = data.GetLastJumpLevelCoords(HexGridManager.GridType.System);
            LastJumpSystemCoords.isSet = coords != null;
            if (coords.HasValue) LastJumpSystemCoords.coords = coords.Value;

            coords = data.GetLastJumpLevelCoords(HexGridManager.GridType.World);
            LastJumpWorldCoords.isSet = coords != null;
            if (coords.HasValue) LastJumpWorldCoords.coords = coords.Value;

            coords = data.GetLastJumpLevelCoords(HexGridManager.GridType.Terrain);
            LastJumpTerrainCoords.isSet = coords != null;
            if (coords.HasValue) LastJumpTerrainCoords.coords = coords.Value;
        }
    }

    [FormerlySerializedAs("CurrentGrid")]
    [Header("Grid State")]
    [Tooltip("The SimpleHexGrid this unit is currently occupying.")]
    public SimpleHexGridBase currentGridBase;
    [Tooltip("The axial coordinates of the hex this unit is currently occupying.")]
    public Vector2Int CurrentGridCoordinates;
    [Tooltip("The seed for this hexagon thats used to generate the child Level")]
    public int CurrentHexGUID;
    
    [Header("Movement Components")]
    [Tooltip("Reference to the PathMover component on this GameObject.")]
    public IEntityPathMover EntityPathMover; // Reference to the PathMover

    [FormerlySerializedAs("unitHeightOffset")]
    [Header("Visual Offset")]
    [Tooltip("The vertical offset from the center of the hex to the unit's pivot point. Adjust this so the unit sits correctly on the hex surface.")]
    public float entityHeightOffset = 0.5f; // Default offset, adjust in Inspector per unit type
    
    public float CurrentGroundY { get; set; }
    
    public virtual void ImportDataToEntity(EntityData data)
    {
        this.EntityGUID = data.entityGUID;
        this.EntityName = data.entityName;
        this.MaxHealth = data.maxHealth;
        this.CurrentHealth = data.currentHealth;
        this.BaseMoveSpeed = data.baseMoveSpeed;
        this.EntityType = data.entityType;
        this.LevelCoords = data.levelCoords;
        this.LastJumpCoords = data.lastJumpLevelCoords;
    }

// 3. SyncFrom pulls state directly from another instance
    public virtual void SyncFrom(Entity other)
    {
        this.EntityGUID = other.EntityGUID;
        this.EntityName = other.EntityName;
        this.MaxHealth = other.MaxHealth;
        this.CurrentHealth = other.CurrentHealth;
        this.BaseMoveSpeed = other.BaseMoveSpeed;
        this.EntityType = other.EntityType;
        this.LevelCoords = other.LevelCoords;
        this.LastJumpCoords = other.LastJumpCoords;
        
        // Keep the GUID/Type stable, but copy the dynamic state
    }
    
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
    public virtual void Initialize(EntityData entityData)
    {
        ImportDataToEntity(entityData);

        Vector2Int? coordsToSnapTo = entityData.GetLevelCoords(GameLevelManager.CurrentLevel);
        if (coordsToSnapTo == null)
        {
            coordsToSnapTo = Vector2Int.zero;
        }
        SnapToHex(GameLevelManager.ActiveGrid, (Vector2Int)coordsToSnapTo); // Snap to the initial position
        
        // Ensure we specifically look for the component
        EntityPathMover = GetComponent<IEntityPathMover>();

        if (EntityPathMover == null)
        {
            Debug.LogError($"Unit '{name}': PathMover component not found on this object!", this);
        }

        EntityManager.RegisterEntity(entityData.entityGUID, this);
            
        Debug.Log($"Unit '{name}' initialized on grid '{currentGridBase.name}' at {CurrentGridCoordinates}.");
    }

    public virtual void SnapToHex(SimpleHexGridBase gridBase, Vector2Int coords)
    {
        currentGridBase = gridBase;
        CurrentGridCoordinates = coords;
        CurrentHexGUID = gridBase.HexagonsInGrid[coords].HexGUID;
        transform.SetParent(gridBase.EntityContainer.transform);

        HexData hexData = gridBase.GetHexData(CurrentGridCoordinates);
        
        // Get the Y level of the top surface of the hex
        Vector3 hexSurfacePosition = gridBase.GetHexTopSurfacePosition(coords, hexData.Height);

        CurrentGroundY = hexSurfacePosition.y + entityHeightOffset;
    
        // Apply the surface Y + the unit's "standing height" 
        // (Use a small offset for the unit's feet relative to the top of the hex)
        transform.position = new Vector3(
            hexSurfacePosition.x, 
            CurrentGroundY, 
            hexSurfacePosition.z
        );

        SetLevelCoordsForEntity(gridBase, coords);
    }

    private void SetLevelCoordsForEntity(SimpleHexGridBase gridBase, Vector2Int coords)
    {
        Debug.Log($"*****************************");
        if (DataManager.TryGetData(EntityGUID, out UnitData data))
        {
            if (gridBase.GridType == HexGridManager.GridType.Galaxy)
            {
                // If galaxy coords have changed, reset all other levels
                if (coords != data.GetLevelCoords(HexGridManager.GridType.Galaxy))
                {
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.System,
                        coords = null
                    });
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.World,
                        coords = null
                    });
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.Terrain,
                        coords = null
                    });
                }

                // Set the Galaxy position
                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.Galaxy,
                    coords = coords
                });
            }

            if (gridBase.GridType == HexGridManager.GridType.System)
            {
                // If galaxy coords have changed, reset all other levels
                if (coords != data.GetLevelCoords(HexGridManager.GridType.System))
                {
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.World,
                        coords = null
                    });
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.Terrain,
                        coords = null
                    });
                }

                // Set the System position
                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.Galaxy,
                    coords = data.GetLevelCoords(HexGridManager.GridType.Galaxy)
                });
                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.System,
                    coords = coords
                });
            }

            if (gridBase.GridType == HexGridManager.GridType.World)
            {
                if (coords != data.GetLevelCoords(HexGridManager.GridType.World))
                {
                    data.SetLevelCoords(new LevelPositionPair()
                    {
                        level = HexGridManager.GridType.Terrain,
                        coords = null
                    });
                }

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.Galaxy,
                    coords = data.GetLevelCoords(HexGridManager.GridType.Galaxy)
                });

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.System,
                    coords = data.GetLevelCoords(HexGridManager.GridType.System)
                });

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.World,
                    coords = coords
                });
            }

            if (gridBase.GridType == HexGridManager.GridType.Terrain)
            {
                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.Galaxy,
                    coords = data.GetLevelCoords(HexGridManager.GridType.Galaxy)
                });

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.System,
                    coords = data.GetLevelCoords(HexGridManager.GridType.System)
                });

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.World,
                    coords = data.GetLevelCoords(HexGridManager.GridType.World)
                });

                data.SetLevelCoords(new LevelPositionPair()
                {
                    level = HexGridManager.GridType.Terrain,
                    coords = coords
                });
            }
            DataManager.UpdateData(data.entityGUID, data);
            UpdateEntityLevelCoords();
            UpdateEntityLastJumpCoords();
        }
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