using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

// Phase 9.2 (Updated): Unit Class
// Purpose: Represents a single movable unit in the game,
// holding its current grid position, managing its visual snap to hexes,
// and orchestrating its movement via an attached PathMover.
public class Entity : MonoBehaviour
{
    [Header("Unit Grid State")]
    [Tooltip("The SimpleHexGrid this unit is currently occupying.")]
    public SimpleHexGrid currentGrid;
    [Tooltip("The axial coordinates of the hex this unit is currently occupying.")]
    public Vector2Int currentGridCoordinates;
    
    [Header("Movement Components")]
    [Tooltip("Reference to the PathMover component on this GameObject.")]
    public IEntityPathMover entityPathMover; // Reference to the PathMover

    [FormerlySerializedAs("unitHeightOffset")]
    [Header("Visual Offset")]
    [Tooltip("The vertical offset from the center of the hex to the unit's pivot point. Adjust this so the unit sits correctly on the hex surface.")]
    public float entityHeightOffset = 0.5f; // Default offset, adjust in Inspector per unit type

    public SimpleHexGrid EntityGrid;
    
    public EntitySpawner.EntityType EntityType;

    public string EntityGUID;

    void Awake()
    {
        // Ensure unitPathMover is assigned, either manually or found automatically
        if (entityPathMover == null)
        {
            entityPathMover = GetComponent<IEntityPathMover>();
            if (entityPathMover == null)
            {
                Debug.LogError($"Unit '{name}': No PathMover component found! Unit will not be able to move.", this);
            }
        }
    }

    /// <summary>
    /// Initializes the unit's starting position and grid state.
    /// This should be called immediately after instantiating a unit.
    /// </summary>
    /// <param name="grid">The SimpleHexGrid to spawn on.</param>
    /// <param name="coords">The axial coordinates on the grid.</param>
    public void Initialize(EntitySpawner.EntityType entityType, SimpleHexGrid grid, Vector2Int coords)
    {
        if (grid == null)
        {
            Debug.LogError($"Unit '{name}': Attempted to initialize with a null grid.", this);
            return;
        }
        if (!grid.IsValidCoordinates(coords))
        {
            Debug.LogWarning($"Unit '{name}': Attempted to initialize on invalid coordinates {coords} on grid '{grid.name}'.", this);
            return;
        }

        EntityType = entityType;
        SnapToHex(grid, coords); // Snap to the initial position
        HexGridManager.Instance.UpdateUnwalkableHexagonsOnAllGrids();
        Debug.Log($"Unit '{name}' initialized on grid '{currentGrid.name}' at {currentGridCoordinates}.");
    }

    /// <summary>
    /// Snaps the unit's transform to the center of the specified hexagon,
    /// applying the unitHeightOffset. Updates the unit's current grid state.
    /// </summary>
    /// <param name="grid">The target SimpleHexGrid.</param>
    /// <param name="coords">The target axial coordinates on the grid.</param>
    public void SnapToHex(SimpleHexGrid grid, Vector2Int coords)
    {
        if (grid == null)
        {
            Debug.LogError($"Unit '{name}': Cannot snap to hex, provided grid is null.", this);
            return;
        }
        if (!grid.IsValidCoordinates(coords))
        {
            Debug.LogWarning($"Unit '{name}': Attempted to snap to invalid coordinates {coords} on grid '{grid.name}'. Unit will remain at current position.", this);
            return;
        }

        currentGrid = grid;
        currentGridCoordinates = coords;
        transform.SetParent(grid.EntityContainer.transform);

        HexData hexData = grid.GetHexData(coords);
        
        // Calculate the world position of the hex center
        Vector3 hexCenterWorldPos = grid.GetHexWorldPosition(coords, hexData.Height);

        // Apply the unit's specific height offset
        transform.position = new Vector3(hexCenterWorldPos.x, hexCenterWorldPos.y + entityHeightOffset, hexCenterWorldPos.z);
    }

    /// <summary>
    /// Commands this unit to move along a given path.
    /// Delegates the actual movement logic to the attached PathMover.
    /// </summary>
    /// <param name="path">The list of PathNodes defining the path.</param>
    public void MoveUnitAlongPath(List<PathNode> path)
    {
        if (entityPathMover != null)
        {
            entityPathMover.StartMoving(path);
        }
        else
        {
            Debug.LogError($"Unit '{name}': Cannot move, no PathMover component assigned or found!", this);
        }
    }

    /// <summary>
    /// Stops any ongoing movement for this unit.
    /// </summary>
    public void StopUnitMovement()
    {
        if (entityPathMover != null)
        {
            entityPathMover.StopMoving();
        }
    }

    /// <summary>
    /// Returns true if the unit is currently moving.
    /// </summary>
    public bool IsUnitMoving()
    {
        return entityPathMover != null && entityPathMover.IsMoving();
    }
}