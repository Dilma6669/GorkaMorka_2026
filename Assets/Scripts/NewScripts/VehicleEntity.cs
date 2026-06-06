using UnityEngine;

public class VehicleEntity : Entity
{
    [SerializeField] private HexagonCollider leftArc;
    [SerializeField] private HexagonCollider rightArc;

    [SerializeField] private Entity Driver;
    [SerializeField] private bool showArcs;

    public override void SetSelected(bool isSelected)
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

    public void SetDriver(Entity driver)
    {
        Driver = driver;
    }

    public void ClearDriver()
    {
        Driver = null;
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
        if (Driver == null)
        {
            return;
        }
        
        bool active = showArcs;

        if (leftArc != null)
        {
            leftArc.gameObject.SetActive(active);
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