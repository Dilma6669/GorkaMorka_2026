// Optional Script 3: SimpleSelectionIndicator.cs
// Create a simple ring/circle prefab and put this script on it for a selection indicator

using UnityEngine;

public class SelectionController : MonoBehaviour
{
    [Header("Animation")]
    public float rotationSpeed = 45f;
    public float pulseSpeed = 2f;
    public float minScale = 0.9f;
    public float maxScale = 1.1f;
    
    private Vector3 baseScale;
    
    void Start()
    {
        baseScale = transform.localScale;
    }
    
    void Update()
    {
        // Rotate the indicator
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        
        // Pulse the scale
        float pulse = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.time * pulseSpeed) + 1) / 2);
        transform.localScale = baseScale * pulse;
    }
}