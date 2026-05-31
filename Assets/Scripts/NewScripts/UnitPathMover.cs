using UnityEngine;
using System.Collections.Generic;

// Phase 9.1 (Refactored): PathMover Class
// Purpose: Moves a GameObject along a given list of PathNodes.
// It no longer directly references the MultiGridPathfinder or initiates pathfinding itself.
public class UnitPathMover : MonoBehaviour, IEntityPathMover
{
    [Tooltip("The speed at which the object moves along the path.")]
    public float moveSpeed = 5f;

    // We'll also add a rotation speed to control how fast the unit turns.
    [Tooltip("The speed at which the object rotates to face the next waypoint.")]
    public float rotationSpeed = 10f;

    private List<PathNode> currentPath;
    private int currentNodeIndex;
    private bool isMoving = false;

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
        currentNodeIndex = 0;
        isMoving = true;

        HexData hexData = currentPath[0].GridReference.GetHexData(currentPath[0].GridCoordinates);
        
        // Immediately snap to the start of the path
        // Ensure the mover's Y position is respected, just updating XZ for the snap point
        Vector3 startHexWorldPos = currentPath[0].GridReference.GetHexWorldPosition(currentPath[0].GridCoordinates, hexData.Height);
        transform.position = new Vector3(startHexWorldPos.x, transform.position.y, startHexWorldPos.z);

        Debug.Log($"PathMover on '{name}': Started moving along path with {path.Count} nodes.");
    }
    
    public void StopMoving()
    {
        isMoving = false;
        currentPath = null;
        currentNodeIndex = 0;
        Debug.Log($"PathMover on '{name}': Stopped moving.");
    }

   
    public bool IsMoving()
    {
        return isMoving;
    }

    public void MoveAlongPath()
    {
        if (currentNodeIndex >= currentPath.Count)
        {
            // Reached the end of the path
            StopMoving();
            Debug.Log($"PathMover on '{name}': Path complete!");
            return;
        }

        PathNode targetNode = currentPath[currentNodeIndex];
        
        HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
        // Get the target hex's center world position
        Vector3 targetHexWorldPos = targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);

        // Maintain the mover's current Y position while moving towards the target hex's XZ
        Vector3 targetPosWithCurrentY = new Vector3(targetHexWorldPos.x, transform.position.y, targetHexWorldPos.z);

        // Calculate the direction to the next hexagon
        Vector3 directionToTarget = targetPosWithCurrentY - transform.position;

        // Only rotate if the unit is moving
        if (directionToTarget != Vector3.zero)
        {
            // Calculate the target rotation to face the direction of movement
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Move towards the target position
        transform.position = Vector3.MoveTowards(transform.position, targetPosWithCurrentY, moveSpeed * Time.deltaTime);

        // If we want the unit to smoothly move up/down between hexes, we need to blend the Y position too.
        // For simple snapping, we only move XZ, and let SnapToHex handle Y.
        // For smoother vertical transitions, uncomment/adapt the following line:
        // transform.position = Vector3.Lerp(transform.position, targetHexWorldPos + Vector3.up * GetComponent<Unit>().unitHeightOffset, moveSpeed * Time.deltaTime / Vector3.Distance(transform.position, targetPos));

        // Check if we are close enough to the target position in XZ plane
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(targetHexWorldPos.x, 0, targetHexWorldPos.z)) < 0.05f) // Small threshold for "reached"
        {
            // Snap to the exact hex position (including proper Y with offset)
            // This assumes the PathMover is on a GameObject that also has a Unit component.
            Entity entity = GetComponent<Entity>();
            if (entity != null)
            {
                entity.SnapToHex(targetNode.GridReference, targetNode.GridCoordinates);
                
                // We need to re-establish units onboard a vehicles new hexagons positions
                /*if (targetNode.GridReference.EntityContainer != null)
                {
                    foreach (var entityOnVehicle in targetNode.GridReference.EntityContainer.GetComponentsInChildren<Entity>())
                    {
                        entityOnVehicle.SnapToHex(entityOnVehicle.currentGrid, entityOnVehicle.currentGridCoordinates);
                    }
                }*/
            }
            else
            {
                // Fallback if no Unit component, just snap to hex center Y
                transform.position = targetHexWorldPos;
            }

            currentNodeIndex++; // Move to the next node in the path
        }
    }
}