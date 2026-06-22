using UnityEngine;

public class EntityOccupancyTracker : MonoBehaviour
{
    private Entity entity;
    private Vector2Int lastCoords;
    private SimpleHexGridBase _lastGridBase;

    void Awake() => entity = GetComponent<Entity>() ??
                    GetComponentInParent<Entity>() ??
                    GetComponentInChildren<Entity>();

    void Update()
    {
        // If the unit has moved to a new coordinate/grid, update the data
        if (entity.CurrentGridCoordinates != lastCoords || entity.currentGridBase != _lastGridBase)
        {
            UpdateOccupancy(entity.currentGridBase, entity.CurrentGridCoordinates);
        }
    }

    void UpdateOccupancy(SimpleHexGridBase gridBase, Vector2Int newCoords)
    {
        // 1. Unclaim old hex
        if (_lastGridBase != null && _lastGridBase.HexagonsInGrid.TryGetValue(lastCoords, out HexData oldData))
        {
            oldData.SetIsOccupied(false);
            oldData.SetOccupier(null);
            _lastGridBase.HexagonsInGrid[lastCoords] = oldData;
        }

        // 2. Claim new hex
        if (gridBase != null && gridBase.HexagonsInGrid.TryGetValue(newCoords, out HexData newData))
        {
            newData.SetIsOccupied(true);
            newData.SetOccupier(entity.EntityGUID);
            gridBase.HexagonsInGrid[newCoords] = newData;
        }

        lastCoords = newCoords;
        _lastGridBase = gridBase;
    }
}