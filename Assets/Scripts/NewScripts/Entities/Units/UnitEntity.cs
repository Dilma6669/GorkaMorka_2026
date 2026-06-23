
using UnityEngine;

public class UnitEntity : Entity
{
    public bool isDriver = false;

    public override EntityData ExportData()
    {
        UnitData data = ScriptableObject.CreateInstance<UnitData>();
        PopulateBaseData(data); // Fill name, health, etc.
    
        // Explicitly copy the unit-specific field
        data.isDriver = this.isDriver; 
    
        return data;
    }
    
    private void Awake()
    {
        
    }
}