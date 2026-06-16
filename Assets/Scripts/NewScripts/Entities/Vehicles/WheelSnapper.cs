using UnityEngine;

public class WheelSnapper : MonoBehaviour
{
    public float raycastHeight = 2.0f;
    public float verticalOffset = 0.5f;
    public LayerMask groundLayer;
    
    [Header("Movement")]
    public float lerpSpeed = 10.0f; // Higher is snappier, lower is smoother
    
    [Header("Limits")]
    public float maxUp = 0.2f;
    public float maxDown = 0.5f;

    private Vector3 defaultLocalPos;

    void Start()
    {
        defaultLocalPos = transform.localPosition;
    }

    void Update()
    {
        Vector3 rayStart = new Vector3(transform.position.x, transform.parent.position.y + raycastHeight, transform.position.z);
        Debug.DrawRay(rayStart, Vector3.down * (raycastHeight * 2f), Color.red);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
        {
            float targetLocalY = (hit.point.y - transform.parent.position.y) + verticalOffset;
            float clampedY = Mathf.Clamp(targetLocalY, defaultLocalPos.y - maxDown, defaultLocalPos.y + maxUp);
            
            // Create the target position
            Vector3 targetLocalPos = new Vector3(defaultLocalPos.x, clampedY, defaultLocalPos.z);

            // Smoothly interpolate towards the target position
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPos, lerpSpeed * Time.deltaTime);
        }
    }
}
