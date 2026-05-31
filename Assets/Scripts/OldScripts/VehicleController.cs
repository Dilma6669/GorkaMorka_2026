using UnityEngine;
using System.Collections.Generic; // Make sure this is included for List

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Settings")]
    public string vehicleName = "Vehicle";
    public bool canMove = true;
    public bool canBeSelected = true;
    
    [Header("Movement Feedback")]
    public GameObject movementParticles; // Optional: particles to show when moving
    public AudioClip selectionSound;     // Optional: sound when selected
    public AudioClip movementSound;      // Optional: sound when starting to move

    [Header("Initial Placement")]
    [Tooltip("The grid position (X, Z) where the vehicle will be placed at start.")]
    public Vector2Int startGridPosition = new Vector2Int(0, 0); // New: Default to (0,0)
    [Tooltip("If true, the vehicle will snap to the center of its starting hexagon and align its rotation.")]
    public bool snapToHexOnStart = true;
    [Tooltip("The desired grid direction the vehicle should face on start (e.g., (0,1) for 'north' on a flat-top hex grid).")]
    public Vector2Int initialHexDirection = new Vector2Int(0, 1); // Default to a common 'north' direction
    [Tooltip("Height offset above the hexagon's top surface for the vehicle.")]
    public float heightOffset = 0.75f; 

    private bool isSelected = false;
    private bool isMoving = false;
    private AudioSource audioSource;
    private HexagonController hexController; // Reference to the HexagonController

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        hexController = FindObjectOfType<HexagonController>();

        if (hexController == null)
        {
            Debug.LogError("VehicleController: No HexagonController found in scene! Cannot snap vehicle to hex grid or find paths.");
            return; // Exit if no hex controller is found
        }
        
        if (snapToHexOnStart)
        {
            SnapVehicleToGridOnStart();
        }
        
        // Make sure this object has a collider for mouse clicks
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"VehicleController '{name}' has no collider! Adding a BoxCollider.");
            gameObject.AddComponent<BoxCollider>();
        }

        // --- Debugging pathfinding setup (Example Usage) ---
        // You can uncomment and modify this block to test pathfinding
        // Make sure you have a VehiclePathFinder component on this GameObject
        /*
        VehiclePathFinder pathFinder = GetComponent<VehiclePathFinder>();
        if (pathFinder != null)
        {
            // Example target: a hex 10 units "north" (positive Z) from start
            // This assumes the HexagonController's world position for (0,1) is truly North.
            Vector3 startWorldPos = transform.position;
            Vector2Int targetGridPos = hexController.WorldToGridPosition(startWorldPos) + new Vector2Int(0, 10); 
            Vector3 targetWorldPos = hexController.GridToWorldPosition(targetGridPos.x, targetGridPos.y);

            // Get the initial facing direction as an integer index (0-5)
            int currentFacingIndex = GetDirectionIndex(initialHexDirection);
            if (currentFacingIndex == -1)
            {
                Debug.LogError($"VehicleController: Could not determine initial facing index for {initialHexDirection}. Defaulting to 0.");
                currentFacingIndex = 0; // Fallback
            }

            Debug.Log("--- Initiating Pathfinding Debug from VehicleController ---");
            pathFinder.DebugPathfinding(startWorldPos, targetWorldPos, currentFacingIndex);

            List<VehiclePathFinder.VehiclePathStep> path = pathFinder.FindPath(startWorldPos, targetWorldPos, currentFacingIndex);
            if (path != null && path.Count > 0)
            {
                Debug.Log($"VehicleController: Path found with {path.Count} steps!");
                // Optionally, visualize path for debugging in editor
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Debug.DrawLine(path[i].targetPosition, path[i+1].targetPosition, Color.blue, 10f);
                }
                Debug.DrawLine(transform.position, path[0].targetPosition, Color.cyan, 10f); // From current pos to first step
            }
            else
            {
                Debug.LogError("VehicleController: No path found for test!");
            }
        }
        */
    }

    /// <summary>
    /// Snaps the vehicle's position and rotation to its starting hexagon.
    /// </summary>
    private void SnapVehicleToGridOnStart()
    {
        // 1. Use the specified startGridPosition to get the target world position
        Vector2Int currentGridPos = startGridPosition; // Use the inspector-set startGridPosition

        // 2. Get the exact world position for the center of that hexagon (top surface)
        // Using the new GridToWorldPosition which also gets height
        Vector3 targetWorldPos = hexController.GridToWorldPosition(currentGridPos.x, currentGridPos.y);
        // Add hexMeshHeight * 0.5f to get to the top surface of the hex
        targetWorldPos.y += hexController.hexMeshHeight * 0.5f; 
        targetWorldPos.y += heightOffset; // Add the vehicle's height offset

        // 3. Snap the vehicle's position
        transform.position = targetWorldPos;
        Debug.Log($"Vehicle '{vehicleName}' snapped position to hex {currentGridPos} at world {targetWorldPos}");

        // 4. Snap the vehicle's rotation to the initialHexDirection
        // Get the world vector corresponding to the initialHexDirection
        // This requires knowing the world positions of two adjacent hexes in the desired direction.
        
        // Get the world position of the current hex
        Vector3 currentHexWorldPos = hexController.GridToWorldPosition(currentGridPos.x, currentGridPos.y);

        // Calculate the target neighbor grid position based on initialHexDirection
        Vector2Int targetNeighborGridPos = currentGridPos + initialHexDirection;

        // Get the world position of the target neighbor hex
        // Ensure the target neighbor is a valid grid position before getting its world position
        if (!hexController.IsValidGridPosition(targetNeighborGridPos.x, targetNeighborGridPos.y))
        {
            Debug.LogWarning($"Vehicle '{vehicleName}': initialHexDirection {initialHexDirection} points to an invalid grid position {targetNeighborGridPos}. Cannot align rotation accurately.");
            // Fallback: Use a generic forward direction if the specific neighbor is out of bounds
            transform.rotation = Quaternion.LookRotation(Vector3.forward); 
            return;
        }

        Vector3 targetNeighborWorldPos = hexController.GridToWorldPosition(targetNeighborGridPos.x, targetNeighborGridPos.y);
        
        Vector3 targetForwardDirection = (targetNeighborWorldPos - currentHexWorldPos).normalized;
        targetForwardDirection.y = 0; // Flatten the direction for rotation

        if (targetForwardDirection.sqrMagnitude > 0.001f) // Ensure it's not a zero vector
        {
            transform.rotation = Quaternion.LookRotation(targetForwardDirection);
            Debug.Log($"Vehicle '{vehicleName}' snapped rotation to face grid direction {initialHexDirection} (world: {targetForwardDirection})");
        }
        else
        {
            Debug.LogWarning($"Vehicle '{vehicleName}': initialHexDirection {initialHexDirection} resulted in a zero world direction (likely start and target neighbor are same or too close). Rotation not snapped.");
        }
    }
    
    // Called when this vehicle is selected
    public void OnSelected()
    {
        isSelected = true;
        
        // Play selection sound
        if (selectionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(selectionSound);
        }
        
        // You can add more selection effects here
        // Like scaling up slightly, glowing, etc.
    }
    
    // Called when this vehicle is deselected
    public void OnDeselected()
    {
        isSelected = false;
        
        // You can add deselection effects here
    }
    
    // Called when movement starts
    public void OnMovementStarted()
    {
        isMoving = true;
        
        // Play movement sound
        if (movementSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(movementSound);
        }
        
        // Enable movement particles
        if (movementParticles != null)
        {
            movementParticles.SetActive(true);
        }
        
        Debug.Log($"{vehicleName} started moving!");
    }
    
    // Called when movement is complete
    public void OnMovementComplete()
    {
        isMoving = false;
        
        // Disable movement particles
        if (movementParticles != null)
        {
            movementParticles.SetActive(false);
        }
        
        Debug.Log($"{vehicleName} finished moving!");
    }
    
    // Public getters
    public bool IsSelected => isSelected;
    public bool IsMoving => isMoving;
    public bool CanMove => canMove;
    public bool CanBeSelected => canBeSelected;
    
    // Optional: Allow external scripts to control this vehicle
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }
    
    public void SetCanBeSelected(bool canBeSelected)
    {
        this.canBeSelected = canBeSelected;
    }

    // --- UPDATED HELPER METHOD ---
    /// <summary>
    /// Converts a Vector2Int grid direction to its corresponding integer index (0-5)
    /// based on the HexagonController's HexDirections array for the given column.
    /// </summary>
    /// <param name="direction">The Vector2Int representing the grid direction.</param>
    /// <returns>The integer index (0-5) or -1 if not found.</returns>
    public int GetDirectionIndex(Vector2Int direction)
    {
        if (hexController == null)
        {
            Debug.LogError("HexagonController not accessible for GetDirectionIndex.");
            return -1;
        }
        
        // Get current hex for parity to retrieve the correct hex directions array
        Vector2Int currentGridPos = hexController.WorldToGridPosition(transform.position);
        Vector2Int[] hexDirectionsForColumn = hexController.GetHexDirectionsForColumn(currentGridPos.x);

        for (int i = 0; i < hexDirectionsForColumn.Length; i++)
        {
            if (hexDirectionsForColumn[i] == direction)
            {
                return i;
            }
        }
        return -1; // Direction not found
    }

    // --- UPDATED HELPER METHOD ---
    /// <summary>
    /// Gets the current facing direction of a vehicle based on its rotation,
    /// converting it to the 0-5 integer index used by the hex grid.
    /// </summary>
    public int GetCurrentFacingDirection()
    {
        if (hexController == null)
        {
            Debug.LogError("HexagonController not accessible for GetCurrentFacingDirection.");
            return 0; // Default to 0 if not initialized
        }

        Vector3 forward = transform.forward;
        forward.y = 0; // Flatten the vector to only consider horizontal rotation
        forward.Normalize();

        float minAngleDiff = float.MaxValue;
        int bestDirectionIndex = 0;

        // Get the correct hex directions array for the current column
        Vector2Int currentGridPos = hexController.WorldToGridPosition(transform.position);
        Vector2Int[] hexDirectionsForColumn = hexController.GetHexDirectionsForColumn(currentGridPos.x);

        for (int i = 0; i < hexDirectionsForColumn.Length; i++)
        {
            Vector2Int gridDir = hexDirectionsForColumn[i];
            // Convert grid direction to a world direction vector (assuming hexes are roughly on XZ plane)
            // This is a simplified conversion. For perfect accuracy, you might need to get world positions
            // of a hex and its neighbor as done in SnapVehicleToGridOnStart.
            Vector3 worldDir = new Vector3(gridDir.x, 0, gridDir.y).normalized; 
            
            float angleDiff = Vector3.Angle(forward, worldDir);

            if (angleDiff < minAngleDiff)
            {
                minAngleDiff = angleDiff;
                bestDirectionIndex = i;
            }
        }
        return bestDirectionIndex;
    }
}




// using UnityEngine;
//
// public class VehicleController : MonoBehaviour
// {
//     [Header("Vehicle Settings")]
//     public string vehicleName = "Vehicle";
//     public bool canMove = true;
//     public bool canBeSelected = true;
//     
//     [Header("Movement Feedback")]
//     public GameObject movementParticles; // Optional: particles to show when moving
//     public AudioClip selectionSound;     // Optional: sound when selected
//     public AudioClip movementSound;      // Optional: sound when starting to move
//
//     [Header("Initial Placement")] // Changed from (Optional)
//     [Tooltip("The grid position (X, Z) where the vehicle will be placed at start.")]
//     public Vector2Int startGridPosition = new Vector2Int(0, 0); // New: Default to (0,0)
//     [Tooltip("If true, the vehicle will snap to the center of its starting hexagon and align its rotation.")]
//     public bool snapToHexOnStart = true;
//     [Tooltip("The desired grid direction the vehicle should face on start (e.g., (0,1) for 'north' on a flat-top hex grid).")]
//     public Vector2Int initialHexDirection = new Vector2Int(0, 1); // Default to a common 'north' direction
//     [Tooltip("Height offset above the hexagon's top surface for the vehicle.")]
//     public float heightOffset = 0.75f; 
//
//     private bool isSelected = false;
//     private bool isMoving = false;
//     private AudioSource audioSource;
//     private HexagonController hexController; // Reference to the HexagonController
//
//     void Start()
//     {
//         audioSource = GetComponent<AudioSource>();
//         hexController = FindObjectOfType<HexagonController>();
//
//         if (hexController == null)
//         {
//             Debug.LogError("VehicleController: No HexagonController found in scene! Cannot snap vehicle to hex grid.");
//         }
//         else if (snapToHexOnStart)
//         {
//             SnapVehicleToGridOnStart();
//         }
//         
//         // Make sure this object has a collider for mouse clicks
//         if (GetComponent<Collider>() == null)
//         {
//             Debug.LogWarning($"VehicleController '{name}' has no collider! Adding a BoxCollider.");
//             gameObject.AddComponent<BoxCollider>();
//         }
//     }
//
//     /// <summary>
//     /// Snaps the vehicle's position and rotation to its starting hexagon.
//     /// </summary>
//     private void SnapVehicleToGridOnStart()
//     {
//         // 1. Use the specified startGridPosition to get the target world position
//         Vector2Int currentGridPos = startGridPosition; // Use the inspector-set startGridPosition
//
//         // 2. Get the exact world position for the center of that hexagon (top surface)
//         Vector3 targetWorldPos = hexController.GetHexWorldPosition(currentGridPos.x, currentGridPos.y);
//         targetWorldPos.y += hexController.hexMeshHeight * 0.5f; // Adjust to top surface
//         targetWorldPos.y += heightOffset; // Add the vehicle's height offset
//
//         // 3. Snap the vehicle's position
//         transform.position = targetWorldPos;
//         Debug.Log($"Vehicle '{vehicleName}' snapped position to hex {currentGridPos} at world {targetWorldPos}");
//
//         // 4. Snap the vehicle's rotation to the initialHexDirection
//         // Get the world vector corresponding to the initialHexDirection
//         // We need to use a dummy hex to get the world vector for a grid direction.
//         Vector2Int dummyCurrentGridPos = new Vector2Int(0, 0); 
//         if (currentGridPos.x % 2 != 0) dummyCurrentGridPos = new Vector2Int(1, 0); // Use (1,0) as a reference for odd rows
//
//         Vector3 dummyCurrentWorldPos = hexController.GetHexWorldPosition(dummyCurrentGridPos.x, dummyCurrentGridPos.y);
//         Vector3 dummyNeighborWorldPos = hexController.GetHexWorldPosition(dummyCurrentGridPos.x + initialHexDirection.x, dummyCurrentGridPos.y + initialHexDirection.y);
//         
//         Vector3 targetForwardDirection = (dummyNeighborWorldPos - dummyCurrentWorldPos).normalized;
//         targetForwardDirection.y = 0; // Flatten the direction for rotation
//
//         if (targetForwardDirection.sqrMagnitude > 0.001f) // Ensure it's not a zero vector
//         {
//             transform.rotation = Quaternion.LookRotation(targetForwardDirection);
//             Debug.Log($"Vehicle '{vehicleName}' snapped rotation to face grid direction {initialHexDirection} (world: {targetForwardDirection})");
//         }
//         else
//         {
//             Debug.LogWarning($"Vehicle '{vehicleName}': initialHexDirection {initialHexDirection} resulted in a zero world direction. Rotation not snapped.");
//         }
//     }
//     
//     // Called when this vehicle is selected
//     public void OnSelected()
//     {
//         isSelected = true;
//         
//         // Play selection sound
//         if (selectionSound != null && audioSource != null)
//         {
//             audioSource.PlayOneShot(selectionSound);
//         }
//         
//         // You can add more selection effects here
//         // Like scaling up slightly, glowing, etc.
//     }
//     
//     // Called when this vehicle is deselected
//     public void OnDeselected()
//     {
//         isSelected = false;
//         
//         // You can add deselection effects here
//     }
//     
//     // Called when movement starts
//     public void OnMovementStarted()
//     {
//         isMoving = true;
//         
//         // Play movement sound
//         if (movementSound != null && audioSource != null)
//         {
//             audioSource.PlayOneShot(movementSound);
//         }
//         
//         // Enable movement particles
//         if (movementParticles != null)
//         {
//             movementParticles.SetActive(true);
//         }
//         
//         Debug.Log($"{vehicleName} started moving!");
//     }
//     
//     // Called when movement is complete
//     public void OnMovementComplete()
//     {
//         isMoving = false;
//         
//         // Disable movement particles
//         if (movementParticles != null)
//         {
//             movementParticles.SetActive(false);
//         }
//         
//         Debug.Log($"{vehicleName} finished moving!");
//     }
//     
//     // Public getters
//     public bool IsSelected => isSelected;
//     public bool IsMoving => isMoving;
//     public bool CanMove => canMove;
//     public bool CanBeSelected => canBeSelected;
//     
//     // Optional: Allow external scripts to control this vehicle
//     public void SetCanMove(bool canMove)
//     {
//         this.canMove = canMove;
//     }
//     
//     public void SetCanBeSelected(bool canBeSelected)
//     {
//         this.canBeSelected = canBeSelected;
//     }
// }
