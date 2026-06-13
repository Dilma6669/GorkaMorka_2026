using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    [Tooltip("Sensitivity for horizontal mouse movement (yaw).")]
    public float mouseSensitivityX = 100f;
    [Tooltip("Sensitivity for vertical mouse movement (pitch).")]
    public float mouseSensitivityY = 100f;
    [Tooltip("Clamps the vertical camera rotation (pitch) to prevent flipping.")]
    public float minYRotation = -90f;
    public float maxYRotation = 90f;

    [Header("Keyboard Movement Settings")]
    [Tooltip("Speed at which the camera moves forward/backward and strafes.")]
    public float movementSpeed = 5f;

    private float rotationX = 0f; // Stores the current horizontal rotation (yaw)
    private float rotationY = 0f; // Stores the current vertical rotation (pitch)

    void Start()
    {
        // Initially, the cursor is visible and unlocked, as movement requires holding Left Shift.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // NEW: Capture the camera's initial rotation from the Transform component
        // This ensures the camera starts at the rotation set in the editor.
        Vector3 initialEulerAngles = transform.localRotation.eulerAngles;
        rotationX = initialEulerAngles.y;
        rotationY = initialEulerAngles.x;

        // Ensure rotationY is clamped to the specified min/max values at start
        rotationY = Mathf.Clamp(rotationY, minYRotation, maxYRotation);
    }

    void Update()
    {
        // Check if the Left Shift key is being held down.
        if (Input.GetKey(KeyCode.LeftShift))
        {
            // If Left Shift is held, lock the cursor and hide it for camera control.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // --- Mouse Look ---
            // Get mouse input for X and Y axes.
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;

            // Update vertical rotation (pitch).
            // Subtract mouseY because moving the mouse up should rotate the camera down (looking up decreases pitch).
            rotationY -= mouseY;
            // Clamp the vertical rotation to prevent the camera from flipping over.
            rotationY = Mathf.Clamp(rotationY, minYRotation, maxYRotation);

            // Update horizontal rotation (yaw).
            rotationX += mouseX;

            // Apply the rotations to the camera's transform.
            // Quaternion.Euler creates a rotation from Euler angles (pitch, yaw, roll).
            // We apply pitch around the X-axis and yaw around the Y-axis.
            transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);


            // --- Keyboard Movement ---
            // Get input for W, A, S, D keys.
            float horizontalInput = Input.GetAxis("Horizontal"); // A/D keys for strafing
            float verticalInput = Input.GetAxis("Vertical");     // W/S keys for forward/backward

            // Calculate movement direction based on camera's orientation.
            // transform.forward moves the camera along its local Z-axis (forward/backward).
            // transform.right moves the camera along its local X-axis (left/right strafing).
            Vector3 moveDirection = transform.right * horizontalInput + transform.forward * verticalInput;

            // Normalize the moveDirection if the magnitude is greater than 1.
            // This prevents faster diagonal movement.
            if (moveDirection.magnitude > 1f)
            {
                moveDirection.Normalize();
            }

            // Apply movement to the camera's position.
            // Multiply by movementSpeed and Time.deltaTime for frame-rate independent movement.
            transform.position += moveDirection * movementSpeed * Time.deltaTime;
        }
        else // If Left Shift is not held
        {
            // Unlock the cursor and make it visible.
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    // This method is called when the application gains or loses focus.
    // It ensures the cursor state is correctly managed even if the window loses focus.
    void OnApplicationFocus(bool hasFocus)
    {
        // If the application loses focus AND Left Shift is not currently held, unlock the cursor.
        // This prevents the cursor from getting stuck if the user alt-tabs out while holding shift.
        if (!hasFocus && !Input.GetKey(KeyCode.LeftShift))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // If the application regains focus and Left Shift is held, re-lock the cursor.
        else if (hasFocus && Input.GetKey(KeyCode.LeftShift))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
