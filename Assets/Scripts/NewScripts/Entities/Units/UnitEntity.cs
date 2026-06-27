
using UnityEngine;

public class UnitEntity : Entity
{
    public bool isDriver = false;

    // public override void SnapToHex(SimpleHexGridBase gridBase, Vector2Int coords)
    // {
    //     base.SnapToHex(gridBase, coords);
    //     
    //     if(DataManager.TryGetData(EntityGUID, out UnitData data))
    //     {
    //         data.SetLevelCoords(new LevelPositionPair()
    //         {
    //             level = gridBase.GridType,
    //             coords = coords
    //         });
    //         Debug.Log($"fuck entityData: {data.entityGUID}");
    //         DataManager.UpdateData(data.entityGUID, data);
    //     }
    // }
}