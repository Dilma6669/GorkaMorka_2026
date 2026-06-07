using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Phase 9.1 (Refactored): PathMover Class
// Purpose: Moves a GameObject along a given list of PathNodes.
// It no longer directly references the MultiGridPathfinder or initiates pathfinding itself.
public class UnitPathMover : MonoBehaviour, IEntityPathMover
{
    // pathfinding fucks up when change this to UnitEntity... look into this when sober
    private UnitEntity entity;
    
    [Tooltip("The speed at which the object moves along the path.")]
    public float moveSpeed = 5f;

    // We'll also add a rotation speed to control how fast the unit turns.
    [Tooltip("The speed at which the object rotates to face the next waypoint.")]
    public float rotationSpeed = 10f;

    private List<PathNode> currentPath;
    private int currentNodeIndex;
    private bool isMoving = false;
    private bool targetReached  = false;
    
    private List<float> pathWorldHeights;
    
    private void Awake()
    {
        entity = GetComponent<UnitEntity>() ??
                 GetComponentInParent<UnitEntity>() ??
                 GetComponentInChildren<UnitEntity>();
    }

    void Update()
    {
        if (isMoving && currentPath != null && currentPath.Count > 0)
        {
            MoveAlongPath();
        }
    }


    public void StartMoving(List<PathNode> path)
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("PathMover: Cannot start moving, path is null or empty.");
            isMoving = false;
            return;
        }

        currentPath = path;
        pathWorldHeights = new List<float>();
        currentNodeIndex = 0;
        
        // Check to Clear unit as driver
        if (entity.isDriver && entity.CurrentGrid.griEntity != null)
        {
            string gridGUID = entity.CurrentGrid.griEntity.EntityGUID;
                
            // issue here
            if (EntityManager.TryGetEntity(gridGUID, out Entity vehicleEntity))
            {
                entity.isDriver = false;
                VehicleEntity vehicle = (VehicleEntity)vehicleEntity;
                vehicle.ClearDriver();
                Debug.Log($"Unit {entity.name} has stopped being Driver for  grid {entity.CurrentGrid.name}!");
            }
        }
        
        isMoving = true;

        // Pre-calculate heights once when the path is created
        foreach (var node in currentPath)
        {
            float h = node.GridReference.GetHexWorldPosition(node.GridCoordinates, 
                node.GridReference.GetHexData(node.GridCoordinates).Height).y;
            pathWorldHeights.Add(h);
        }

        HexData hexData = currentPath[0].GridReference.GetHexData(currentPath[0].GridCoordinates);
        
        // Immediately snap to the start of the path
        // Ensure the mover's Y position is respected, just updating XZ for the snap point
        Vector3 startHexWorldPos = currentPath[0].GridReference.GetHexWorldPosition(currentPath[0].GridCoordinates, hexData.Height);
        transform.position = new Vector3(startHexWorldPos.x, transform.position.y, startHexWorldPos.z);

        targetReached = true;
        Debug.Log($"PathMover on '{name}': Started moving along path with {path.Count} nodes.");
    }
    
    public void StopMoving()
    {
        PathNode finalNode = currentPath.Last();
        NodeArrival(finalNode);
        isMoving = false;
        currentPath = null;
        currentNodeIndex = 0;
        pathWorldHeights = null;
        Debug.Log($"PathMover on '{name}': Stopped moving.");
    }

   
    public bool IsMoving()
    {
        return isMoving;
    }
    
    public void MoveAlongPath()
    {
        if (isMoving == false)
            return;
        
        if (currentPath == null || currentPath.Count == 0)
        {
            return;
        }

        // Final destination check
        PathNode finalNode = currentPath.Last();
        HexData finalHexData = finalNode.GridReference.GetHexData(finalNode.GridCoordinates);
        Vector3 finalPos = finalNode.GridReference.GetHexWorldPosition(finalNode.GridCoordinates, finalHexData.Height);
    
        // Check if we are close enough to the final destination to declare "Done"
        float distToFinal = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
            new Vector3(finalPos.x, 0, finalPos.z));

        if (distToFinal < 0.05f) // Arrival threshold
        {
            StopMoving(); 
            return; // Exit early so we don't keep moving
        }
        // --------------------------------------
        
        // Calculate heights of current and potential target
        int nextIndex = currentNodeIndex + 1;
        int skipIndex = Mathf.Min(currentNodeIndex + 2, currentPath.Count - 1);
        
        PathNode currentNode = currentPath[currentNodeIndex];
        float currentHeight = pathWorldHeights[currentNodeIndex];
        
        if (currentPath.Count <= nextIndex)
            return;
        PathNode nextNode = currentPath[nextIndex];
        float nextHeight = pathWorldHeights[nextIndex];
        
        
        if (currentPath.Count <= skipIndex)
            return;
        PathNode skipNode = currentPath[skipIndex];
        float skipHeight = pathWorldHeights[skipIndex];

        int targetIndex;
        bool targetJumping = false;
        
        // If next node is same height as current
        if (Mathf.Approximately(currentHeight, nextHeight))
        {
            targetIndex = nextIndex;
            
            // If skip node is same height as current
            if (currentPath.Count > 2 && Mathf.Approximately(nextHeight, skipHeight))
            {
                targetIndex = skipIndex;
            }

        }
        else // If next node is NOT the same height as current
        {
            targetIndex = nextIndex;
            targetJumping = true;
        }
        
        // --------------------------------------
        
        PathNode targetNode = currentPath[targetIndex];
        HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
        Vector3 targetHexWorldPos =
            targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
        Vector3 targetPosWithCurrentY = new Vector3(targetHexWorldPos.x, transform.position.y, targetHexWorldPos.z);

        // 2. Movement
        transform.position = Vector3.MoveTowards(transform.position, targetPosWithCurrentY, moveSpeed * Time.deltaTime);

        // 3. Rotation: Still face the target we are moving toward
        Vector3 dir = (targetPosWithCurrentY - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir),
                rotationSpeed * Time.deltaTime);
        }

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(targetHexWorldPos.x, 0, targetHexWorldPos.z));
        
        
        // Smaller threshold for Jumping
        if (dist < 0.05f && targetJumping)
        {
            NodeArrival(targetNode);

            currentNodeIndex = targetIndex; // Good here!
            return;
        }
        
        // larger threshold for skipping
        if (dist < 0.3f)
        {
            NodeArrival(targetNode);
            
            currentNodeIndex = targetIndex; // Good here!
        }

    }

    private void NodeArrival(PathNode targetNode)
    {
        entity.SnapToHex(targetNode.GridReference, targetNode.GridCoordinates);
        
        // Boarding
        CheckForBoardingVehicle(targetNode.GridReference);
        
        //Driving
        CheckForDrivingVehicle(targetNode);
    }

    private void CheckForBoardingVehicle(SimpleHexGrid grid)
    {
        if (entity.CurrentGrid != grid)
        {
            Debug.Log($"Unit {entity.name} has boarded new grid {grid.name}!");
            entity.SetEntityToNewGrid(grid);
        }
    }

    private void CheckForDrivingVehicle(PathNode targetNode)
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return;
        }
        
        HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
        
        // If the tile is a Command Seat
        if (hexData.IsCommandSeat)
        {
            string gridGUID = entity.CurrentGrid.griEntity.EntityGUID;
            
            if(EntityManager.TryGetEntity(gridGUID, out Entity vehicleEntity))
            {
                entity.isDriver = true;
                VehicleEntity vehicle = (VehicleEntity)vehicleEntity;
                vehicle.SetDriver(entity);
                EntitySelectionManager.SelectVehicle(vehicle);
                Debug.Log($"Unit {entity.name} has become Driver for new grid {entity.CurrentGrid.name}!");
            }
        }
    }

}