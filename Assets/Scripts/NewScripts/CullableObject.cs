using UnityEngine;

public class CullableObject : MonoBehaviour
{
    public Vector2Int parentChunkID; // Stores the chunk it belongs to

    private int defaultLayer;
    private int hiddenLayer;

    void Awake() 
    {
        defaultLayer = gameObject.layer;
        hiddenLayer = LayerMask.NameToLayer("Hidden");
        
        
    }

    private void Start()
    {
        SetVisibility(false);
    }

    public void SetVisibility(bool isVisible)
    {
        int targetLayer = isVisible ? LayerMask.NameToLayer("WorldObjects") : LayerMask.NameToLayer("Hidden");
    
        // Apply to self and all children
        foreach (Transform t in gameObject.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = targetLayer;
        }
    }
}