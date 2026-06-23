using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Purpose: Handles path traversal specifically for vehicles.
// Implements steering-based forward movement and path smoothing.
public class VehiclePathMover : MonoBehaviour, IEntityPathMover
{
    [Header("Movement Settings")]
    [Tooltip("The speed at which the vehicle moves forward.")]
    public float moveSpeed = 5f;

    [Tooltip("The arc degree that a vehicle can turn.")]
    public float turningArc = 10f;
    
    [Tooltip("The speed at which the vehicle rotates to steer. Slower values create wider turns.")]
    public float turnSpeed = 5f;

    [Tooltip("How many nodes ahead the vehicle should look to smooth its turns.")]
    public int lookAheadNodes = 2;

    [Header("Debugging")]
    public List<PathNode> currentPath;
    private int currentNodeIndex = 0;
    
    private VehicleEntity entity;
    
    private bool isMoving = false;

    [Header("Node Advancement")]
    [Tooltip("How closely the vehicle's forward direction must be aligned with the next node to proceed.")]
    [Range(0.8f, 1.0f)]
    public float nodeAlignmentThreshold = 0.95f;

    private bool isReversing;
    
    
    private float lastDistanceToTarget = float.MaxValue;
    private float timeSinceLastProgress = 0f;
    private const float STUCK_THRESHOLD = 2.0f; // Seconds before declaring "stuck"
    
    
    private void Awake()
    {
        entity = GetComponent<VehicleEntity>();
        
        if (entity == null)
        {
            Debug.LogError($"VehiclePathMover on {gameObject.name} needs a VehicleEntity component!");
        }
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
            Debug.LogWarning("VehiclePathMover: Cannot start moving, path is null or empty.");
            isMoving = false;
            return;
        }

        currentNodeIndex = 0;
        currentPath = GetSmoothPathForVehicle(path);
    
        // CHANGE THIS FROM 0 TO 1:
        // Skip the first node because it's the hex the vehicle is currently occupying
      //  currentNodeIndex = currentPath.Count > 1 ? 1 : 0; 
    
        isMoving = true;
    }
    
    public void StopMoving()
    {
        isMoving = false;
        currentPath = null;
        currentNodeIndex = 0;
        entity.RefreshShadowHexCollider();
        entity.RefreshArcsHexColliders();
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

        if (entity.GetDriver() == null)
        {
            return;
        }

        
        // NEW RE-ASSESSMENT LAYER: 
        // If the vehicle can see the final destination or is close enough,
        // skip all intermediate breadcrumbs and head straight for the final tile!
        if (currentPath.Count > 1 && currentNodeIndex < currentPath.Count - 1)
        {
            int finalIndex = currentPath.Count - 1;
            PathNode finalNode = currentPath[finalIndex];
            HexData finalHexData = finalNode.GridBaseReference.GetHexData(finalNode.GridCoordinates);
            Vector3 finalPos = finalNode.GridBaseReference.GetHexWorldPosition(finalNode.GridCoordinates, finalHexData.Height);
    
            // Calculate distance to the final goal
            float distanceToGoal = Vector3.Distance(transform.position, finalPos);
    
            // If you are within a reasonable range of the final goal, drop the middle nodes
            if (distanceToGoal < 8.0f) 
            {
                currentNodeIndex = finalIndex;
            }
        }
        
        // 1. Determine our target: 
        // We target the node 2 steps ahead to create the "look-ahead" arc.
        int skipIndex = Mathf.Min(currentNodeIndex + 2, currentPath.Count - 1);
        PathNode targetNode = currentPath[skipIndex];
    
        HexData hexData = targetNode.GridBaseReference.GetHexData(targetNode.GridCoordinates);
        Vector3 targetHexWorldPos = targetNode.GridBaseReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);

        // 2. Determine steering look-ahead:
        // Look a bit further than the target node for smooth steering.
        int steeringLookAhead = Mathf.Min(skipIndex + lookAheadNodes, currentPath.Count - 1);
        PathNode futureNode = currentPath[steeringLookAhead];
        HexData futureHexData = futureNode.GridBaseReference.GetHexData(futureNode.GridCoordinates);
        Vector3 rawSteeringDestination = futureNode.GridBaseReference.GetHexWorldPosition(futureNode.GridCoordinates, futureHexData.Height);
    
        // 1. Calculate direction to the destination
        // 1. Calculate direction to the destination
        Vector3 directionToTarget = rawSteeringDestination - transform.position;
        directionToTarget.y = 0;

        if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
        {
            Vector3 normalizedDir = directionToTarget.normalized;
        
            float angleDot = Vector3.Dot(transform.forward, normalizedDir);
            bool isReversing = angleDot < -0.5f;
            Vector3 rawSteeringDirection = isReversing ? -normalizedDir : normalizedDir;

            // DYNAMIC ROTATION SPEED:
            // Calculate distance to current target node to scale down rotation speed
            float distToTarget = Vector3.Distance(transform.position, targetHexWorldPos);
            // If within 5 units, we start slowing down the rotation to prevent "snap-over"
            float rotationMultiplier = Mathf.Clamp01(distToTarget / 5.0f);
            float dynamicTurnSpeed = turnSpeed * rotationMultiplier;

            // Apply rotation with the dynamic speed
            Quaternion targetRotation = Quaternion.LookRotation(rawSteeringDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, dynamicTurnSpeed * turningArc * Time.deltaTime);
            
            // 2. Handle Movement: Blend forward drive with target drift so it handles wide turns naturally
            float currentMoveDirection = isReversing ? -1f : 1f;
        
            // Blend 80% vehicle nose direction with 20% direct path pull to compensate for the wide arc
            Vector3 movementVelocity = Vector3.Lerp(transform.forward * currentMoveDirection, normalizedDir, 0.2f);
        
            transform.position += movementVelocity.normalized * moveSpeed * Time.deltaTime;
        }
    
        // 3. Check if we have reached OR passed the current target node.
        Vector3 toTarget = targetHexWorldPos - transform.position;
        toTarget.y = 0;
    
        // Use a slightly larger radius (e.g., 2.0f) so the vehicle "consumes" 
        // the node as it passes by, without needing to hit the center.
        if (toTarget.magnitude < 5.0f)
        {
            if (entity != null)
            {
                entity.currentGridBase = targetNode.GridBaseReference;
                entity.CurrentGridCoordinates = targetNode.GridCoordinates;
            }

            currentNodeIndex += 2;
        }
        
        // PROGRESS WATCHDOG 
        Vector3 distanceToFinal = (currentPath[currentPath.Count - 1].GridBaseReference.GetHexWorldPosition(currentPath[currentPath.Count - 1].GridCoordinates, 0) - transform.position);
        float currentDist = distanceToFinal.magnitude;

        // If we are not moving closer (threshold of 0.001f prevents jitter issues)
        if (Mathf.Abs(lastDistanceToTarget - currentDist) < 0.001f)
        {
            timeSinceLastProgress += Time.deltaTime;
        }
        else
        {
            timeSinceLastProgress = 0f;
        }

        lastDistanceToTarget = currentDist;

        if (timeSinceLastProgress > STUCK_THRESHOLD)
        {
            Debug.LogError($"[PATH FAILURE] Vehicle '{gameObject.name}' is STUCK at node {currentNodeIndex}. Declaring path failed.");
            StopMoving();
        }
    }


    /// <summary>
    /// Inserts additional path nodes to create a smoother path for vehicles.
    /// </summary>
    public List<PathNode> GetSmoothPathForVehicle(List<PathNode> originalPath)
    {
        if (originalPath == null || originalPath.Count <= 1)
        {
            return originalPath;
        }
    
        List<PathNode> smoothedPath = new List<PathNode>();
        smoothedPath.Add(originalPath[0]);

        for (int i = 1; i < originalPath.Count - 1; i++)
        {
            PathNode previousNode = originalPath[i - 1];
            PathNode currentNode = originalPath[i];
            
            HexData previousHexData = previousNode.GridBaseReference.GetHexData(previousNode.GridCoordinates);
            HexData currentHexData = currentNode.GridBaseReference.GetHexData(currentNode.GridCoordinates);
            
            if (!previousHexData.IsWalkable || !currentHexData.IsWalkable || !previousHexData.IsOccupied || !currentHexData.IsOccupied)
            {
                smoothedPath.Add(currentNode);
                continue;
            }
            
            PathNode nextNode = originalPath[i + 1];

            // A turn is detected if the direction from the previous node to the current
            // is different from the direction from the current to the next.
            Vector2Int incomingDirection = currentNode.GridCoordinates - previousNode.GridCoordinates;
            Vector2Int outgoingDirection = nextNode.GridCoordinates - currentNode.GridCoordinates;

            if (incomingDirection != outgoingDirection)
            {
                // Add a waypoint to smooth the turn.
                smoothedPath.Add(new PathNode(currentNode.GridCoordinates, currentNode.GridBaseReference));
            }

            smoothedPath.Add(currentNode);
        }
        
        // Always add the last node.
        smoothedPath.Add(originalPath[originalPath.Count - 1]);

        return smoothedPath;
    }
}