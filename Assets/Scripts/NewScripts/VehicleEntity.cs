using UnityEngine;

public class VehicleEntity : Entity
{
    [SerializeField] private HexagonCollider leftArc;
    [SerializeField] private HexagonCollider rightArc;

    [SerializeField] private bool showArcs;

    public virtual void SetSelected(bool isSelected)
    {
        // If we are selected, show arcs. If deselected, hide them.
        ShowArcs = isSelected;
    }
    
    public bool ShowArcs 
    {
        get => showArcs;
        set 
        {
            showArcs = value;
            ApplyArcState();
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Clear any existing calls to avoid stacking errors
        UnityEditor.EditorApplication.delayCall -= ApplyArcState;
        UnityEditor.EditorApplication.delayCall += ApplyArcState;
#endif
    }

    public void ApplyArcState()
    {
        bool active = showArcs;

        if (leftArc != null)
        {
            leftArc.gameObject.SetActive(active);

            // If turning off, clear BEFORE deactivating the object
            if (!active) leftArc.ClearBlockedHexes();
        }

        if (rightArc != null)
        {
            rightArc.gameObject.SetActive(active);
            if (!active) rightArc.ClearBlockedHexes();
        }

        Physics.SyncTransforms();
    }

}