using System.Collections.Generic;
using UnityEngine;

public class CraftEntity : Entity
{
    public SimpleHexGridBase InteriorGridBase;
    
    [SerializeField] private List<HexagonCollider> shadowHexColliders;

    [SerializeField] private Entity Driver;
    
    public override void EntitySelected(bool isSelected)
    {
        RefreshShadowHexCollider();
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


    public void RefreshShadowHexCollider()
    {
        foreach (var shadowHexCollider in shadowHexColliders)
        {
            shadowHexCollider.gameObject.SetActive(false);
            shadowHexCollider.gameObject.SetActive(true);
        }
    }
}
