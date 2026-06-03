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
            StopMoving();
            return;
        }

        // 1. SKIP LOGIC: Target the NEXT node instead of the CURRENT one
        // If we have at least one node ahead of us, aim for that one.
        int targetIndex = Mathf.Min(currentNodeIndex + 2, currentPath.Count - 1);
    
        PathNode targetNode = currentPath[targetIndex];
        Vector3 targetPos = GetWorldPos(targetNode);
        Vector3 targetPosWithCurrentY = new Vector3(targetPos.x, transform.position.y, targetPos.z);

        // 2. Movement
        transform.position = Vector3.MoveTowards(transform.position, targetPosWithCurrentY, moveSpeed * Time.deltaTime);

        // 3. Rotation: Still face the target we are moving toward
        Vector3 dir = (targetPosWithCurrentY - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        }

        // 4. ARRIVAL CHECK:
        // We check distance to the node we are aiming at (the one we skipped to)
        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
            new Vector3(targetPos.x, 0, targetPos.z));

        if (dist < 0.3f) 
        {
            // Final snap logic
            if (targetIndex == currentPath.Count - 1)
            {
                Entity entity = GetComponent<Entity>();
                if (entity != null) 
                {
                    entity.SnapToHex(targetNode.GridReference, targetNode.GridCoordinates);
                }
            }
        
            // Increment index. Because we are skipping, we jump by 2, 
            // or just move the index to the one we just "arrived" at.
            currentNodeIndex = targetIndex; 
        }
    }
    
    // Helper to clean up your code
    private Vector3 GetWorldPos(PathNode node)
    {
        HexData hexData = node.GridReference.GetHexData(node.GridCoordinates);
        return node.GridReference.GetHexWorldPosition(node.GridCoordinates, hexData.Height);
    }
}