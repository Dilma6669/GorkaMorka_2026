using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class VehicleObstacle : MonoBehaviour
{
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
                
                // Use your existing grid logic to disable walking
                // We'll need a way to modify the HexData, 
                // but for visualization, we just highlight it
                tile.SetHighlightColour(Color.red, true);
                
                // IMPORTANT: You need to tell the Pathfinding that this hex is blocked
                // Update the HexData directly
                if (tile.GridReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    data.SetIsWalkable(false);
                    tile.GridReference.HexagonsInGrid[tile.GridCoordinates] = data;
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
                tile.SetHighlightColour(Color.clear, false);
                
                // Re-enable walking
                if (tile.GridReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    data.SetIsWalkable(true);
                    tile.GridReference.HexagonsInGrid[tile.GridCoordinates] = data;
                }
            }
        }
    }
}