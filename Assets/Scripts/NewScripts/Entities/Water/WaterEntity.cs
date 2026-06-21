using UnityEngine;

public class WaterEntity : MonoBehaviour
{
    public void SetVisibility(bool isVisible)
    {
        int targetLayer = isVisible ? LayerMask.NameToLayer("WaterCollider") : LayerMask.NameToLayer("Hidden");
    
        // Apply to self and all children
        foreach (Transform t in gameObject.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = targetLayer;
        }
    }
}
    
