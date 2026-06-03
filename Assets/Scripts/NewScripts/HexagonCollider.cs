using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class HexagonCollider : MonoBehaviour
{
    public Entity Entity;
    public List<HexVisualTile> currentlyBlockedTiles = new List<HexVisualTile>();

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object we hit is a hex tile
        if (other.TryGetComponent(out HexVisualTile tile))
        {
            // Block the tile
            if (!currentlyBlockedTiles.Contains(tile))
            {
                currentlyBlockedTiles.Add(tile);
                
                // IMPORTANT: You need to tell the Pathfinding that this hex is blocked
                // Update the HexData directly
                if (tile.GridReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    data.SetIsOccupied(true);
                    data.SetOccupier(Entity.EntityGUID);
                    tile.GridReference.HexagonsInGrid[tile.GridCoordinates] = data;
                    tile.SetBaseColor(Color.red);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out HexVisualTile tile))
        {
            if (currentlyBlockedTiles.Contains(tile))
            {
                currentlyBlockedTiles.Remove(tile);
                
                // Re-enable walking
                if (tile.GridReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    data.SetIsOccupied(false);
                    data.SetOccupier(null);
                    tile.GridReference.HexagonsInGrid[tile.GridCoordinates] = data;
                    tile.ResetBaseColor();
                }
            }
        }
    }
}