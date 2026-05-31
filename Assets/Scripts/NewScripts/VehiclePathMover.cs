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
    
    private Entity entity;
    
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
            Debug.LogWarning("VehiclePathMover: Cannot start moving, path is null or empty.");
            isMoving = false;
            return;
        }

        currentPath = SmoothPathForVehicle(path);
    
        // CHANGE THIS FROM 0 TO 1:
        // Skip the first node because it's the hex the vehicle is currently occupying
        currentNodeIndex = currentPath.Count > 1 ? 1 : 0; 
    
        isMoving = true;
    }
    
    // public void StartMoving(List<PathNode> path)
    // {
    //     if (path == null || path.Count == 0)
    //     {
    //         Debug.LogWarning("PathMover: Cannot start moving, path is null or empty.");
    //         isMoving = false;
    //         return;
    //     }
    //     
    //     // First, check if the path is valid and smooth it for the vehicle.
    //     currentPath = SmoothPathForVehicle(path);
    //     
    //     // Reset path tracking variables.
    //     currentNodeIndex = 0;
    //     isMoving = true;
    //     
    //     HexData hexData = currentPath[0].GridReference.GetHexData(currentPath[0].GridCoordinates);
    //     
    //     // Immediately snap to the start of the path
    //     // Ensure the mover's Y position is respected, just updating XZ for the snap point
    //     Vector3 startHexWorldPos = currentPath[0].GridReference.GetHexWorldPosition(currentPath[0].GridCoordinates, hexData.Height);
    //     transform.position = new Vector3(startHexWorldPos.x, transform.position.y, startHexWorldPos.z);
    //
    //     Debug.Log($"PathMover on '{name}': Started moving along path with {path.Count} nodes.");
    // }
    
    public void StopMoving()
    {
        isMoving = false;
        currentPath = null;
        currentNodeIndex = 0;
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
    
        // NEW RE-ASSESSMENT LAYER: 
        // If the vehicle can see the final destination or is close enough,
        // skip all intermediate breadcrumbs and head straight for the final tile!
        if (currentPath.Count > 1 && currentNodeIndex < currentPath.Count - 1)
        {
            int finalIndex = currentPath.Count - 1;
            PathNode finalNode = currentPath[finalIndex];
            HexData finalHexData = finalNode.GridReference.GetHexData(finalNode.GridCoordinates);
            Vector3 finalPos = finalNode.GridReference.GetHexWorldPosition(finalNode.GridCoordinates, finalHexData.Height);
    
            // Calculate distance to the final goal
            float distanceToGoal = Vector3.Distance(transform.position, finalPos);
    
            // If you are within a reasonable range of the final goal, drop the middle nodes
            if (distanceToGoal < 8.0f) 
            {
                currentNodeIndex = finalIndex;
            }
        }
    
        // --- NEW ANGLE-BASED WAYPOINT DISCARD LAYER ---
        // If we have future nodes left before the destination, check if our current target 
        // requires a weirdly sharp, awkward turn to get back to.
        while (currentNodeIndex > 1 && currentNodeIndex < currentPath.Count - 1)
        {
            PathNode checkNode = currentPath[currentNodeIndex];
            HexData checkData = checkNode.GridReference.GetHexData(checkNode.GridCoordinates);
            Vector3 checkWorldPos = checkNode.GridReference.GetHexWorldPosition(checkNode.GridCoordinates, checkData.Height);
    
            Vector3 dirToNode = (checkWorldPos - transform.position);
            dirToNode.y = 0;
    
            if (dirToNode.sqrMagnitude > Mathf.Epsilon)
            {
                // Calculate how well our current nose direction aligns with this node
                float alignmentToNode = Vector3.Dot(transform.forward, dirToNode.normalized);
    
                // If the node is behind our 90-degree shoulder line (alignment < 0)
                // or requires a harsh side-turn while we are moving fast, skip it!
                if (alignmentToNode < 0.1f) 
                {
                    currentNodeIndex++; // Discard this node and check the next one in the queue
                    continue;
                }
            }
            break; // The current node is safely in front of us, proceed to steer toward it
        }
        // ----------------------------------------------
    
        PathNode targetNode = currentPath[currentNodeIndex];
        HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
        Vector3 targetHexWorldPos = targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
    
        // Get the destination for steering (using lookAheadNodes)
        Vector3 rawSteeringDestination;
        if (currentNodeIndex + lookAheadNodes < currentPath.Count)
        {
            PathNode futureNode = currentPath[currentNodeIndex + lookAheadNodes];
            HexData futureHexData = futureNode.GridReference.GetHexData(futureNode.GridCoordinates);
            rawSteeringDestination = futureNode.GridReference.GetHexWorldPosition(futureNode.GridCoordinates, futureHexData.Height);
        }
        else
        {
            rawSteeringDestination = targetHexWorldPos;
        }
    
        // 1. Calculate direction to the destination
        Vector3 directionToTarget = rawSteeringDestination - transform.position;
        directionToTarget.y = 0;
    
        if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
        {
            Vector3 normalizedDir = directionToTarget.normalized;
        
            float angleDot = Vector3.Dot(transform.forward, normalizedDir);
            bool isReversing = angleDot < -0.5f;
            Vector3 rawSteeringDirection = isReversing ? -normalizedDir : normalizedDir;
    
            // Calculate the rotation step, but CLAMP the maximum angle change allowed in a single frame
            Quaternion targetRotation = Quaternion.LookRotation(rawSteeringDirection);
        
            // This ensures the nose can only turn a maximum of (turnSpeed * Time.deltaTime) degrees per frame
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * turningArc * Time.deltaTime);
            
            // 2. Handle Movement: Blend forward drive with target drift so it handles wide turns naturally
            float currentMoveDirection = isReversing ? -1f : 1f;
        
            // Blend 80% vehicle nose direction with 20% direct path pull to compensate for the wide arc
            Vector3 movementVelocity = Vector3.Lerp(transform.forward * currentMoveDirection, normalizedDir, 0.2f);
        
            transform.position += movementVelocity.normalized * moveSpeed * Time.deltaTime;
        }
    
        // 3. Check if we have reached OR passed the current target node.
        Vector3 toTarget = targetHexWorldPos - transform.position;
        toTarget.y = 0;
        
        float dotToTarget = Vector3.Dot(transform.forward, toTarget.normalized);
    
        // If we are close enough, or if we cross the checkpoint plane
        if (toTarget.magnitude < 1.0f || (dotToTarget < 0 && Vector3.Dot(transform.forward, toTarget.normalized) > -0.5f))
        {
            if (entity != null)
            {
                entity.currentGrid = targetNode.GridReference;
                entity.currentGridCoordinates = targetNode.GridCoordinates;
            }
    
            currentNodeIndex++;
        }
        
        // PROGRESS WATCHDOG 
        Vector3 distanceToFinal = (currentPath[currentPath.Count - 1].GridReference.GetHexWorldPosition(currentPath[currentPath.Count - 1].GridCoordinates, 0) - transform.position);
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

    
    // public void MoveAlongPath()
    // {
    //     if (currentNodeIndex >= currentPath.Count)
    //     {
    //         StopMoving();
    //         Debug.Log($"VehiclePathMover on '{name}': Path complete!");
    //         return;
    //     }
    //
    //     PathNode targetNode = currentPath[currentNodeIndex];
    //     HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
    //     Vector3 targetHexWorldPos = targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
    //
    //     // Get the destination for steering (using lookAheadNodes)
    //     Vector3 rawSteeringDestination;
    //     if (currentNodeIndex + lookAheadNodes < currentPath.Count)
    //     {
    //         PathNode futureNode = currentPath[currentNodeIndex + lookAheadNodes];
    //         HexData futureHexData = futureNode.GridReference.GetHexData(futureNode.GridCoordinates);
    //         rawSteeringDestination = futureNode.GridReference.GetHexWorldPosition(futureNode.GridCoordinates, futureHexData.Height);
    //     }
    //     else
    //     {
    //         rawSteeringDestination = targetHexWorldPos;
    //     }
    //
    //     // 1. Calculate direction to the destination
    //     Vector3 directionToTarget = rawSteeringDestination - transform.position;
    //     directionToTarget.y = 0;
    //
    //     if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
    //     {
    //         Vector3 normalizedDir = directionToTarget.normalized;
    //         
    //         // Check if the target node is deeply behind the vehicle (Angle > 120 degrees)
    //         float angleDot = Vector3.Dot(transform.forward, normalizedDir);
    //         bool isReversing = angleDot < -0.5f;
    //
    //         // Handle Steering: If reversing, invert the steering direction
    //         Vector3 rawSteeringDirection = isReversing ? -normalizedDir : normalizedDir;
    //         
    //         // SMOOTHING STEP: Blend current forward direction with the raw steering direction.
    //         // Lower values (e.g., 0.15f) create wider, sweeping curves. Higher values turn sharper.
    //         Vector3 smoothedSteeringDirection = Vector3.Slerp(transform.forward, rawSteeringDirection, 0.25f);
    //         smoothedSteeringDirection.y = 0;
    //
    //         Quaternion targetRotation = Quaternion.LookRotation(smoothedSteeringDirection.normalized);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    //
    //         // 2. Handle Movement: Drive forward or backward based on target placement
    //         float currentMoveDirection = isReversing ? -1f : 1f;
    //         
    //         float speedFactor = 1f;
    //     
    //         // AMENDED LINE: Check if the target is NOT the final node in the path before braking
    //         bool isFinalNode = (currentNodeIndex == currentPath.Count - 1);
    //
    //         if (!isReversing && angleDot < 0.3f && !isFinalNode) 
    //         {
    //             // Only scale down speed for intermediate sharp turns, not the final stop
    //             speedFactor = Mathf.Max(0.15f, angleDot); 
    //         }
    //
    //         transform.position += transform.forward * (moveSpeed * speedFactor * currentMoveDirection) * Time.deltaTime;
    //         
    //     }
    //
    //     // 3. Check if we have reached OR passed the current target node.
    //     Vector3 toTarget = targetHexWorldPos - transform.position;
    //     toTarget.y = 0;
    //     
    //     float dotToTarget = Vector3.Dot(transform.forward, toTarget.normalized);
    //
    //     // If we are close enough, or if we cross the checkpoint plane
    //     if (toTarget.magnitude < 1.0f || (dotToTarget < 0 && Vector3.Dot(transform.forward, toTarget.normalized) > -0.5f))
    //     {
    //         if (entity != null)
    //         {
    //             entity.currentGrid = targetNode.GridReference;
    //             entity.currentGridCoordinates = targetNode.GridCoordinates;
    //         }
    //
    //         currentNodeIndex++;
    //     }
    // }
    
    
    // public void MoveAlongPath()
    // {
    //     if (currentNodeIndex >= currentPath.Count)
    //     {
    //         StopMoving();
    //         Debug.Log($"VehiclePathMover on '{name}': Path complete!");
    //         return;
    //     }
    //
    //     PathNode targetNode = currentPath[currentNodeIndex];
    //     HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
    //     Vector3 targetHexWorldPos = targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
    //
    //     // Get the destination for steering (using lookAheadNodes)
    //     Vector3 steeringDestination;
    //     if (currentNodeIndex + lookAheadNodes < currentPath.Count)
    //     {
    //         PathNode futureNode = currentPath[currentNodeIndex + lookAheadNodes];
    //         HexData futureHexData = futureNode.GridReference.GetHexData(futureNode.GridCoordinates);
    //         steeringDestination = futureNode.GridReference.GetHexWorldPosition(futureNode.GridCoordinates, futureHexData.Height);
    //     }
    //     else
    //     {
    //         steeringDestination = targetHexWorldPos;
    //     }
    //
    //     // 1. Calculate direction to the destination
    //     Vector3 directionToTarget = steeringDestination - transform.position;
    //     directionToTarget.y = 0;
    //
    //     if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
    //     {
    //         Vector3 normalizedDir = directionToTarget.normalized;
    //         
    //         // Check if the target node is behind the vehicle (Angle > 90 degrees)
    //         float angleDot = Vector3.Dot(transform.forward, normalizedDir);
    //         bool isReversing = angleDot < -0.5f;
    //
    //         // 2. Handle Steering: If reversing, invert the steering direction so the rear backs in
    //         Vector3 steeringDirection = isReversing ? -normalizedDir : normalizedDir;
    //         
    //         Quaternion targetRotation = Quaternion.LookRotation(steeringDirection);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    //
    //         // 3. Handle Movement: Drive forward or backward based on target placement
    //         float currentMoveDirection = isReversing ? -1f : 1f;
    //         transform.position += transform.forward * (moveSpeed * currentMoveDirection) * Time.deltaTime;
    //     }
    //
    //     // 4. Check if we have reached OR passed the current target node.
    //     Vector3 toTarget = targetHexWorldPos - transform.position;
    //     toTarget.y = 0;
    //     
    //     float dotToTarget = Vector3.Dot(transform.forward, toTarget.normalized);
    //
    //     // If we are close enough, or if we cross the checkpoint plane
    //     if (toTarget.magnitude < 1.0f || (dotToTarget < 0 && Vector3.Dot(transform.forward, toTarget.normalized) > -0.5f))
    //     {
    //         if (entity != null)
    //         {
    //             entity.currentGrid = targetNode.GridReference;
    //             entity.currentGridCoordinates = targetNode.GridCoordinates;
    //         }
    //
    //         currentNodeIndex++;
    //     }
    // }
    
        // public void MoveAlongPath()
    // {
    //     if (currentNodeIndex >= currentPath.Count)
    //     {
    //         StopMoving();
    //         Debug.Log($"VehiclePathMover on '{name}': Path complete!");
    //         return;
    //     }
    //
    //     PathNode targetNode = currentPath[currentNodeIndex];
    //     HexData hexData = targetNode.GridReference.GetHexData(targetNode.GridCoordinates);
    //     Vector3 targetHexWorldPos = targetNode.GridReference.GetHexWorldPosition(targetNode.GridCoordinates, hexData.Height);
    //
    //     // Get the destination for steering (using lookAheadNodes)
    //     Vector3 steeringDestination;
    //     if (currentNodeIndex + lookAheadNodes < currentPath.Count)
    //     {
    //         PathNode futureNode = currentPath[currentNodeIndex + lookAheadNodes];
    //         HexData futureHexData = futureNode.GridReference.GetHexData(futureNode.GridCoordinates);
    //         steeringDestination = futureNode.GridReference.GetHexWorldPosition(futureNode.GridCoordinates, futureHexData.Height);
    //     }
    //     else
    //     {
    //         steeringDestination = targetHexWorldPos;
    //     }
    //
    //     // 1. Handle Steering: Use a smoothly interpolated steering target.
    //     Vector3 direction = steeringDestination - transform.position;
    //     direction.y = 0;
    //
    //     if (direction.sqrMagnitude > Mathf.Epsilon)
    //     {
    //         Quaternion targetRotation = Quaternion.LookRotation(direction);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    //     }
    //
    //     // 2. Handle Movement: Always move forward at a constant speed.
    //     transform.position += transform.forward * moveSpeed * Time.deltaTime;
    //
    //     // 3. Check if we have reached OR passed the current target node.
    //     Vector3 toTarget = targetHexWorldPos - transform.position;
    //     toTarget.y = 0;
    //     
    //     // Check if we are close enough, OR if we have driven past the node (Dot product goes negative)
    //     float dotToTarget = Vector3.Dot(transform.forward, toTarget.normalized);
    //
    //     if (toTarget.magnitude < 1.0f || dotToTarget < 0)
    //     {
    //         if (entity != null)
    //         {
    //             //entity.SnapToHex(targetNode.GridReference, targetNode.GridCoordinates);
    //             
    //             entity.currentGrid = targetNode.GridReference;
    //             entity.currentGridCoordinates = targetNode.GridCoordinates;
    //         }
    //
    //         currentNodeIndex++;
    //     }
    // }

    /// <summary>
    /// Inserts additional path nodes to create a smoother path for vehicles.
    /// </summary>
    public List<PathNode> SmoothPathForVehicle(List<PathNode> originalPath)
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
            
            HexData previousHexData = previousNode.GridReference.GetHexData(previousNode.GridCoordinates);
            HexData currentHexData = currentNode.GridReference.GetHexData(currentNode.GridCoordinates);

            if (!previousHexData.GetIsWalkable() || !currentHexData.GetIsWalkable())
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
                smoothedPath.Add(new PathNode(currentNode.GridCoordinates, currentNode.GridReference));
            }

            smoothedPath.Add(currentNode);
        }
        
        // Always add the last node.
        smoothedPath.Add(originalPath[originalPath.Count - 1]);

        return smoothedPath;
    }
}