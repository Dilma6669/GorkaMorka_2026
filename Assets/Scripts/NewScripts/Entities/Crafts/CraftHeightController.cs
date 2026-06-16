using Unity.Collections;
using UnityEngine;

public class CraftHeightController : MonoBehaviour
{
    public float currentAltitude = 0.0f;
    
    [Header("Flight Settings")]
    [SerializeField] private float hoverHeight = 2.0f;
    public float transitionSpeed = 2.0f;
    
    [Header("Debug")]
    [ReadOnly] public bool isFlying;
    [ReadOnly] public bool isLanded;

    private CraftPathMover mover;
    private CraftEntity entity; // We need this to access the grid/coords
    
    private Vector3 lastPosition;
    private bool isActuallyMoving;
    
    void Awake()
    {
        mover = GetComponent<CraftPathMover>();
        entity = GetComponent<CraftEntity>();
    }

    void Update()
    {
        if (entity == null || entity.currentGridBase == null) return;

        // 1. Calculate velocity
        Vector3 displacement = transform.position - lastPosition;
        isActuallyMoving = displacement.magnitude > 0.001f;
        lastPosition = transform.position;

        // 2. State logic: 
        // We stay in the air until we have physically sunk down to the landing height.
        if (isActuallyMoving) isFlying = true;
        else if (currentAltitude <= (entity.CurrentGroundY + 0.01f)) isFlying = false;

        // 3. Determine target:
        // CurrentGroundY is our "Landed" baseline. 
        // We add an extra flight height if we are in the air.
        float targetY = isFlying ? (entity.CurrentGroundY + hoverHeight) : entity.CurrentGroundY;
    
        // 4. Smooth transition
        currentAltitude = Mathf.MoveTowards(currentAltitude, targetY, transitionSpeed * Time.deltaTime);
    
        // 5. Apply only Y
        transform.position = new Vector3(transform.position.x, currentAltitude, transform.position.z);

        // 6. Landed status
        isLanded = !isFlying;
    }
}