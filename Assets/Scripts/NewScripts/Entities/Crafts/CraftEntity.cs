using System.Collections.Generic;
using UnityEngine;

public class CraftEntity : Entity
{
    public SimpleHexGridBase InteriorGridBase;
    
    [SerializeField] private List<HexagonCollider> shadowHexColliders;

    [SerializeField] private Entity Driver;
    
    CraftHeightController craftHeightController;

    public override EntityData ExportData()
    {
        CraftData data = ScriptableObject.CreateInstance<CraftData>();
        PopulateBaseData(data); // Fill name, health, etc.
    
        // Explicitly copy the unit-specific field
        //data.isDriver = this.isDriver; 
    
        return data;
    }
    
    private void Awake()
    {
        craftHeightController = GetComponent<CraftHeightController>();
    }
    
    public override void SnapToHex(SimpleHexGridBase gridBase, Vector2Int coords)
    {
        if (GetComponent<CraftHeightController>().isFlying)
        {
            currentGridBase = gridBase;
            CurrentGridCoordinates = coords;
            return; 
        }
        
        currentGridBase = gridBase;
        CurrentGridCoordinates = coords;
        transform.SetParent(gridBase.EntityContainer.transform);

        HexData hexData = gridBase.GetHexData(CurrentGridCoordinates);
        Vector3 hexSurfacePosition = gridBase.GetHexTopSurfacePosition(coords, hexData.Height);
    
        // Update our ground reference for the HeightController
        CurrentGroundY = hexSurfacePosition.y; 

        // Determine Y: 
        // If flying, preserve current transform.position.y (ignore the snap)
        // If NOT flying, snap to ground (plus offset)
        float newY = (craftHeightController.isFlying) ? transform.position.y : (hexSurfacePosition.y + entityHeightOffset);

        transform.position = new Vector3(
            hexSurfacePosition.x, 
            newY, 
            hexSurfacePosition.z
        );
    }
    
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
