using UnityEngine;
using UnityEngine.Serialization;

public class VehicleEntity : Entity
{
    public SimpleHexGrid VehicleInteriorGrid;
    
    [SerializeField] private HexagonCollider shadowHexCollider;
    [SerializeField] private HexagonCollider leftArcHexCollider;
    [SerializeField] private HexagonCollider rightArcHexCollider;

    [SerializeField] private Entity Driver;
    [SerializeField] private bool showArcs;

    public override void EntitySelected(bool isSelected)
    {
        ShowArcs = isSelected;
        RefreshShadowHexCollider();
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
    
    public Entity GetDriver()
    {
        return Driver;
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

        if (leftArcHexCollider != null)
        {
            leftArcHexCollider.gameObject.SetActive(active);
            if (!active) leftArcHexCollider.ClearBlockedHexes();
        }

        if (rightArcHexCollider != null)
        {
            rightArcHexCollider.gameObject.SetActive(active);
            if (!active) rightArcHexCollider.ClearBlockedHexes();
        }

        Physics.SyncTransforms();
    }

    public void RefreshShadowHexCollider()
    {
        shadowHexCollider.gameObject.SetActive(false);
        shadowHexCollider.gameObject.SetActive(true);
    }
    
    public void RefreshArcsHexColliders()
    {
        ShowArcs = false;
        ShowArcs = true;
    }
}