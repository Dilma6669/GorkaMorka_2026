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
    private bool targetReached  = false;
    
    private List<float> pathWorldHeights;
    
    
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
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("PathMover: Cannot start moving, path is null or empty.");
            isMoving = false;
            return;
        }

        currentPath = path;
        pathWorldHeights = new List<float>();
        currentNodeIndex = 0;
        
        isMoving = true;

        // Pre-calculate heights once when the path is created
        foreach (var node in currentPath)
        {
            float h = node.GridBaseReference.GetHexWorldPosition(node.GridCoordinates, 
                node.GridBaseReference.GetHexData(node.GridCoordinates).Height).y;
            pathWorldHeights.Add(h);
        }

        HexData hexData = currentPath[0].GridBaseReference.GetHexData(currentPath[0].GridCoordinates);
        
        // Immediately snap to the start of the path
        // Ensure the mover's Y position is respected, just updating XZ for the snap point
        Vector3 startHexWorldPos = currentPath[0].GridBaseReference.GetHexWorldPosition(currentPath[0].GridCoordinates, hexData.Height);
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

    public bool IsMoving() => isMoving;

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
        HexData finalHexData = finalNode.GridBaseReference.GetHexData(finalNode.GridCoordinates);
        Vector3 finalPos = finalNode.GridBaseReference.GetHexWorldPosition(finalNode.GridCoordinates, finalHexData.Height);
    
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
        int skipCount = 4;
        int skipIndex = Mathf.Min(currentNodeIndex + skipCount, currentPath.Count - 1);
        
        if (currentPath.Count <= nextIndex)
            return;
        
        if (currentPath.Count <= skipIndex)
            return;

        int targetIndex;
        
        targetIndex = nextIndex;
        
        // If skip node is same height as current
        if (currentPath.Count > skipCount)
        {
            targetIndex = skipIndex;
        }

        // --------------------------------------
        
        PathNode targetNode = currentPath[targetIndex];
        Vector3 targetHexWorldPos = targetNode.GridBaseReference.GetHexWorldPosition(targetNode.GridCoordinates, 0);
        Vector3 targetPosWithCurrentY = new Vector3(targetHexWorldPos.x, transform.position.y, targetHexWorldPos.z);

        // 2. Movement
        transform.position = Vector3.MoveTowards(transform.position, targetPosWithCurrentY, moveSpeed * Time.deltaTime);

        // 3. Rotation: Still face the target we are moving toward
        Vector3 dir = (new Vector3(targetHexWorldPos.x, transform.position.y, targetHexWorldPos.z) - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir),
                rotationSpeed * Time.deltaTime);
        }

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(targetHexWorldPos.x, 0, targetHexWorldPos.z));
        
        
        // larger threshold for skipping
        if (dist < 0.3f)
        {
            NodeArrival(targetNode);
            
            currentNodeIndex = targetIndex; // Good here!
        }

    }
    
    private void NodeArrival(PathNode targetNode)
    {
        entity.SnapToHex(targetNode.GridBaseReference, targetNode.GridCoordinates);
        
    }

}