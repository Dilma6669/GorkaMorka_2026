// Script 2: SelectableUnit.cs
// Put this on every unit/object you want to be able to select and move

using UnityEngine;
using System.Collections.Generic; // Added for HexagonController.GetHexNeighbors, if needed for WorldDirectionToHexDirection

public class UnitController : MonoBehaviour
{
    [Header("Unit Settings")]
    public string unitName = "Unit";
    public bool canMove = true;
    public bool canBeSelected = true;
    
    [Header("Movement Feedback")]
    public GameObject movementParticles; // Optional: particles to show when moving
    public AudioClip selectionSound;     // Optional: sound when selected
    public AudioClip movementSound;      // Optional: sound when starting to move

    [Header("Initial Placement")]
    [Tooltip("The grid position (X, Z) where the unit will be placed at start.")]
    public Vector2Int startGridPosition = new Vector2Int(0, 0); // Default to (0,0)
    [Tooltip("The desired grid direction the unit should face on start (e.g., (0,1) for 'north' on a flat-top hex grid).")]
    public Vector2Int initialHexDirection = new Vector2Int(0, 1); // Default to a common 'north' direction
    [Tooltip("Height offset above the hexagon's top surface for the unit.")]
    public float heightOffset = 0.5f; 
    
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
            Debug.LogError("UnitController: No HexagonController found in scene! Cannot snap unit to hex grid.");
        }
        else
        {
            SnapUnitToGridOnStart();
        }
        
        // Make sure this object has a collider for mouse clicks
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"SelectableUnit '{name}' has no collider! Adding a BoxCollider.");
            gameObject.AddComponent<BoxCollider>();
        }
    }

    /// <summary>
    /// Snaps the unit's position and rotation to its starting hexagon.
    /// </summary>
    private void SnapUnitToGridOnStart()
    {
        // 1. Get the exact world position for the center of the startGridPosition (top surface)
        Vector3 targetWorldPos = hexController.GetHexWorldPosition(startGridPosition.x, startGridPosition.y);
        targetWorldPos.y += hexController.hexMeshHeight * 0.5f; // Adjust to top surface
        targetWorldPos.y += heightOffset; // Add the unit's height offset

        // 2. Snap the unit's position
        transform.position = targetWorldPos;
        Debug.Log($"Unit '{unitName}' snapped position to hex {startGridPosition} at world {targetWorldPos}");

        // 3. Snap the unit's rotation to the initialHexDirection
        // We need to use a dummy hex to get the world vector for a grid direction.
        // This accounts for the staggered grid's neighbor calculation.
        Vector2Int dummyCurrentGridPos = new Vector2Int(0, 0); 
        if (startGridPosition.x % 2 != 0) dummyCurrentGridPos = new Vector2Int(1, 0); 

        Vector3 dummyCurrentWorldPos = hexController.GetHexWorldPosition(dummyCurrentGridPos.x, dummyCurrentGridPos.y);
        Vector3 dummyNeighborWorldPos = hexController.GetHexWorldPosition(dummyCurrentGridPos.x + initialHexDirection.x, dummyCurrentGridPos.y + initialHexDirection.y);
        
        Vector3 targetForwardDirection = (dummyNeighborWorldPos - dummyCurrentWorldPos).normalized;
        targetForwardDirection.y = 0; // Flatten the direction for rotation

        if (targetForwardDirection.sqrMagnitude > 0.001f) // Ensure it's not a zero vector
        {
            transform.rotation = Quaternion.LookRotation(targetForwardDirection);
            Debug.Log($"Unit '{unitName}' snapped rotation to face grid direction {initialHexDirection} (world: {targetForwardDirection})");
        }
        else
        {
            Debug.LogWarning($"Unit '{unitName}': initialHexDirection {initialHexDirection} resulted in a zero world direction. Rotation not snapped.");
        }
    }
    
    // Called when this unit is selected
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
    
    // Called when this unit is deselected
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
        
        Debug.Log($"{unitName} started moving!");
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
        
        Debug.Log($"{unitName} finished moving!");
    }
    
    // Public getters
    public bool IsSelected => isSelected;
    public bool IsMoving => isMoving;
    public bool CanMove => canMove;
    public bool CanBeSelected => canBeSelected;
    
    // Optional: Allow external scripts to control this unit
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }
    
    public void SetCanBeSelected(bool canBeSelected)
    {
        this.canBeSelected = canBeSelected;
    }
}
