using System;
using UnityEngine;
using System.Collections.Generic;

// Phase 9.1 (Refactored): PathMover Class
// Purpose: Moves a GameObject along a given list of PathNodes.
// It no longer directly references the MultiGridPathfinder or initiates pathfinding itself.
public class UnitPathMover : MonoBehaviour, IEntityPathMover
{
    private Entity entity;
    
    [Tooltip("The speed at which the object moves along the path.")]
    public float moveSpeed = 5f;

    // We'll also add a rotation speed to control how fast the unit turns.
    [Tooltip("The speed at which the object rotates to face the next waypoint.")]
    public float rotationSpeed = 10f;

    private List<PathNode> currentPath;
    private int currentNodeIndex;
    private bool isMoving = false;

    private void Awake()
    {
        entity = GetComponent<Entity>();
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

        // --- NEW: VERTICAL SKIP CONSTRAINT ---
        // Calculate heights of current and potential target
        int nextIndex = currentNodeIndex + 1;
        int skipIndex = Mathf.Min(currentNodeIndex + 2, currentPath.Count - 1);

        PathNode currentNode = currentPath[currentNodeIndex];
        if (currentPath.Count <= nextIndex)
            return;
        if (currentPath.Count <= skipIndex)
            return;
        PathNode nextNode = currentPath[nextIndex];
        PathNode skipNode = currentPath[skipIndex];

        float currentHeight = currentNode.GridReference.GetHexData(currentNode.GridCoordinates).Height;
        float nextHeight = nextNode.GridReference.GetHexData(nextNode.GridCoordinates).Height;
        float skipHeight = skipNode.GridReference.GetHexData(skipNode.GridCoordinates).Height;

        
        // Only skip to skipIndex if the height doesn't change on the way there
        int targetIndex = (Mathf.Approximately(currentHeight, nextHeight) && Mathf.Approximately(nextHeight, skipHeight)) 
            ? skipIndex 
            : nextIndex;
        
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

        // Check if we are close enough to the target position in XZ plane
        if (dist < 0.3f) // Small threshold for "reached"
        {
            entity.SnapToHex(targetNode.GridReference, targetNode.GridCoordinates);

            currentNodeIndex = targetIndex; // Good here!
        }
    }

}