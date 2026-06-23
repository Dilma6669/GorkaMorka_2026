using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization; // Required for List<PathNode>

// Phase 9.4: UnitCommander Class (Test Script)
// Purpose: A simple script to demonstrate commanding a specific unit to find and traverse a path.
// In a full game, this logic would be part of a larger input/selection manager.
public class EntityCommander : MonoBehaviour
{

    private static Entity entityToCommand;
    private static SimpleHexGridBase _targetGridBase;
    private static Vector2Int targetCoordinates;

    [Tooltip("The specific Unit this commander will control.")]
    public Entity EntityInCommand;
    
    [FormerlySerializedAs("TargetGrid")]
    [Header("Target for Pathfinding (for testing)")]
    [Tooltip("The grid where the target hex is located.")]
    public SimpleHexGridBase targetGridBase;
    
    [Tooltip("The axial coordinates of the target hex on the target grid.")]
    private MultiGridPathfinder pathfinder;
    public Vector2Int TargetCoordinates;
    
    // A static reference to the current instance
    public static EntityCommander Instance { get; private set; }
    

    private void Awake()
    {
        pathfinder = GetComponent<MultiGridPathfinder>();
        Instance = this;
    }

    public static void SetEntityToCommand(Entity entity)
    {
        entityToCommand = entity;
        
        if (Instance != null)
        {
            Instance.EntityInCommand = entity;
        }

        if (entity != null)
        {
            Debug.Log($"EntityCommander: Assigned '{entity.name}' to EntityCommander.");
        }
        
        EntitySelectionManager.CachedMovementPath = null;
    }
    
    public static Entity GetEntityInCommand()
    {
        return entityToCommand;
    }

    public static void SetTargetGridAndCoordinates(SimpleHexGridBase gridBase, Vector2Int coords)
    {
        _targetGridBase = gridBase;
        targetCoordinates = coords;
        
        if (Instance != null)
        {
            Instance.targetGridBase = gridBase;
            Instance.TargetCoordinates = coords;
        }
    }
    
    
    // Inside your EntityCommander.cs script
    [ContextMenu("Command Unit to Move")]
    public static void CommandUnitToMove()
    {
        if (entityToCommand == null)
        {
            Debug.LogError("UnitCommander: unitToCommand is null. Cannot command unit to move.");
            return;
        }
        
        if (_targetGridBase == null)
        {
            Debug.LogError("UnitCommander: Target Grid for pathfinding is not assigned!");
            return;
        }
        if (!_targetGridBase.IsValidCoordinates(targetCoordinates))
        {
            Debug.LogError($"UnitCommander: Target coordinates {targetCoordinates} are invalid on grid '{_targetGridBase.name}'.");
            return;
        }
    
        // 1. Get the correct mover component based on the entity's type
        // This makes the code work for both Units and Vehicles
        IEntityPathMover moverComponent = entityToCommand.GetComponent<IEntityPathMover>();
        
        if (moverComponent == null)
        {
            Debug.LogError($"EntityCommander: No PathMover or VehiclePathMover found on entity '{entityToCommand.name}'.");
            return;
        }
        
        List<PathNode> path = EntitySelectionManager.CachedMovementPath;
    
        // 3. If a path is found, command the mover component to start
        if (path != null && path.Count > 0)
        {
            Debug.Log($"EntityCommander: Found path for '{entityToCommand.name}'. Commanding entity to move.");
  
            entityToCommand.MoveUnitAlongPath(path);
        }
        else
        {
            Debug.LogWarning($"EntityCommander: No path found for '{entityToCommand.name}' to {targetCoordinates} on '{_targetGridBase.name}'.");
        }
    }
    
    private bool IsPathPossibleForVehicle(List<PathNode> path)
    {
        if (path == null || path.Count < 3) return true; // Too short to have sharp turns

        // Loop through the path and check the angles between turns
        for (int i = 0; i < path.Count - 2; i++)
        {
            // Get world positions for three consecutive nodes
            HexData data1 = path[i].GridBaseReference.GetHexData(path[i].GridCoordinates);
            Vector3 pos1 = path[i].GridBaseReference.GetHexWorldPosition(path[i].GridCoordinates, data1.Height);

            HexData data2 = path[i + 1].GridBaseReference.GetHexData(path[i + 1].GridCoordinates);
            Vector3 pos2 = path[i + 1].GridBaseReference.GetHexWorldPosition(path[i + 1].GridCoordinates, data2.Height);

            HexData data3 = path[i + 2].GridBaseReference.GetHexData(path[i + 2].GridCoordinates);
            Vector3 pos3 = path[i + 2].GridBaseReference.GetHexWorldPosition(path[i + 2].GridCoordinates, data3.Height);

            // Calculate directions between the nodes
            Vector3 dirToNext = (pos2 - pos1).normalized;
            Vector3 dirToAfterNext = (pos3 - pos2).normalized;

            // Find the angle of the turn
            float turnAngle = Vector3.Angle(dirToNext, dirToAfterNext);

            // If the turn angle is sharper than 60 degrees (a sharp hex corner), it's impossible
            if (turnAngle > 60f)
            {
                return false; 
            }
        }

        return true;
    }
}