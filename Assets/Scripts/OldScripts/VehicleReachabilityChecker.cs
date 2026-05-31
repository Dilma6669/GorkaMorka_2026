
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Provides static methods to check the reachability and movement outcome of a vehicle
/// based on its current state and pathfinding parameters.
/// </summary>
public static class VehicleReachabilityChecker
{
    /// <summary>
    /// Predicts if a vehicle can reach its target directly without overshooting/looping,
    /// given its current state and movement parameters.
    /// </summary>
    /// <param name="startPos">The vehicle's current world position.</param>
    /// <param name="targetPos">The world position of the target destination.</param>
    /// <param name="currentFacing">The vehicle's current forward direction (normalized).</param>
    /// <param name="vehicleMovementSpeed">The vehicle's movement speed.</param>
    /// <param name="turningRadius">The turningRadius parameter from VehiclePathFinderHybrid.</param>
    /// <param name="headingAdjustmentSpeed">The headingAdjustmentSpeed parameter from VehiclePathFinderHybrid.</param>
    /// <param name="smoothTurningMinDistance">The smoothTurningMinDistance parameter from VehiclePathFinderHybrid.</param>
    /// <param name="directMovementThreshold">The directMovementThreshold parameter from VehiclePathFinderHybrid.</param>
    /// <returns>True if the vehicle can reach the target directly, false if it will overshoot and need to loop.</returns>
    public static bool CanReachDirectly(
        Vector3 startPos, Vector3 targetPos, Vector3 currentFacing, float vehicleMovementSpeed,
        float turningRadius, float headingAdjustmentSpeed, float smoothTurningMinDistance, float directMovementThreshold)
    {
        // If already very close, it's a direct hit by definition of directMovementThreshold
        if (Vector3.Distance(startPos, targetPos) <= directMovementThreshold)
        {
            return true;
        }

        // If the target is within the smoothTurningMinDistance, it will use direct movement.
        // We need to ensure it's not pointing completely away from the target.
        if (Vector3.Distance(startPos, targetPos) < smoothTurningMinDistance)
        {
            Vector3 initialDirectionToTarget = (targetPos - startPos).normalized;
            float initialAngle = Vector3.Angle(currentFacing, initialDirectionToTarget);
            // If the initial angle is greater than 90 degrees, it's likely to overshoot significantly
            // even if it switches to direct movement, as it would need to turn sharply first.
            // This threshold (90) can be tuned.
            if (initialAngle > 90f) 
            {
                return false; 
            }
            return true; // Otherwise, consider it direct within this threshold.
        }

        // Simulate the path to predict overshoot
        Vector3 simulatedPosition = startPos;
        Vector3 simulatedVelocity = currentFacing.normalized * vehicleMovementSpeed;
        
        // Calculate maxTurnRate based on headingAdjustmentSpeed and turningRadius
        // This is crucial for accurate simulation of the smooth turning logic.
        float maxTurnRate = (headingAdjustmentSpeed / turningRadius) * Mathf.Deg2Rad;

        // Simulate for a reasonable time, e.g., twice the time it would take to travel directly.
        // This prevents infinite loops for unreachable targets.
        float maxSimulationTime = Vector3.Distance(startPos, targetPos) / vehicleMovementSpeed * 2.0f; 
        float simulationTimeStep = 0.05f; // Smaller step for more accuracy
        float currentSimTime = 0f;

        float closestDistanceSoFar = Vector3.Distance(simulatedPosition, targetPos);
        
        while (currentSimTime < maxSimulationTime && Vector3.Distance(simulatedPosition, targetPos) > directMovementThreshold)
        {
            // Calculate desired velocity towards target
            Vector3 directionToTarget = (targetPos - simulatedPosition).normalized;
            directionToTarget.y = 0; // Keep on horizontal plane for turning calculations

            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                Vector3 desiredVelocity = directionToTarget * vehicleMovementSpeed;

                // Smoothly adjust simulated velocity towards desired velocity
                Vector3 velocityChange = Vector3.RotateTowards(
                    simulatedVelocity.normalized,
                    desiredVelocity.normalized,
                    maxTurnRate * simulationTimeStep,
                    0f
                );
                simulatedVelocity = velocityChange * vehicleMovementSpeed;
            }

            // Move the simulated vehicle
            simulatedPosition += simulatedVelocity * simulationTimeStep;

            float currentDistance = Vector3.Distance(simulatedPosition, targetPos);

            // Check for overshoot: If we start moving away from the target significantly
            // after initially getting closer, and we are still outside the direct movement threshold.
            // A small buffer (0.01f) is added to avoid false positives due to floating point inaccuracies.
            if (currentDistance > closestDistanceSoFar + 0.01f && currentDistance > directMovementThreshold) 
            {
                return false; // Overshoot detected
            }
            closestDistanceSoFar = Mathf.Min(closestDistanceSoFar, currentDistance);

            // Additional check: If the vehicle's simulated forward vector is pointing significantly
            // away from the target while still being relatively far. This catches cases where
            // the vehicle might be "circling" away.
            float angleToTarget = Vector3.Angle(simulatedVelocity, directionToTarget);
            if (angleToTarget > 90f && currentDistance > directMovementThreshold * 2f) 
            {
                return false; // Pointing away and still far, indicates overshoot
            }

            currentSimTime += simulationTimeStep;
        }

        // If the loop finished by reaching the directMovementThreshold, it's a direct hit.
        // If it exited due to maxSimulationTime, it means it couldn't reach directly within that time.
        return Vector3.Distance(simulatedPosition, targetPos) <= directMovementThreshold;
    }
}
