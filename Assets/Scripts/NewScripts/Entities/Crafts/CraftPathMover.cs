using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CraftPathMover : MonoBehaviour, IEntityPathMover
{
    private CraftEntity entity;

    [Header("Flight Settings")]
    public float moveSpeed = 8f;
    public float rotationSpeed = 15f;
    public float targetAltitude = 2.0f; // The height the craft hovers at
    
    private List<PathNode> currentPath;
    private int currentNodeIndex;
    private bool isMoving = false;

    private void Awake()
    {
        entity = GetComponent<CraftEntity>();
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
        currentPath = path;
        currentNodeIndex = 0;
        isMoving = true;
    }

    public void StopMoving()
    {
        isMoving = false;
        currentPath = null;
    }

    public bool IsMoving() => isMoving;

    public void MoveAlongPath()
    {
        if (currentNodeIndex >= currentPath.Count)
        {
            StopMoving();
            return;
        }

        // 1. Get Target Position (Including Altitude)
        PathNode targetNode = currentPath[currentNodeIndex];
        HexData hexData = targetNode.GridBaseReference.GetHexData(targetNode.GridCoordinates);
        Vector3 targetHexWorldPos = targetNode.GridBaseReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
        
        // Apply hover altitude
        Vector3 finalTargetPos = targetHexWorldPos + (Vector3.up * targetAltitude);

        // 2. Rotation (Rotate to face target)
        Vector3 direction = (finalTargetPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 3. Movement
        transform.position = Vector3.MoveTowards(transform.position, finalTargetPos, moveSpeed * Time.deltaTime);

        // 4. Check Arrival
        if (Vector3.Distance(transform.position, finalTargetPos) < 0.1f)
        {
            // Snap the entity to the node data
            entity.SnapToHex(targetNode.GridBaseReference, targetNode.GridCoordinates);
            currentNodeIndex++;
        }
    }
}