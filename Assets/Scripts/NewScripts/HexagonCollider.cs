using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class HexagonCollider : MonoBehaviour
{
    public Entity Entity;
    public List<HexVisualTile> currentlyBlockedTiles = new List<HexVisualTile>();

    public string SetUniqueGUID;
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object we hit is a hex tile
        if (other.TryGetComponent(out HexVisualTile tile))
        {
            // Block the tile
            if (!currentlyBlockedTiles.Contains(tile))
            {
                // IMPORTANT: You need to tell the Pathfinding that this hex is blocked
                // Update the HexData directly
                if (tile.gridBaseReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    if (data.GetIsOccupied())
                    {
                        Debug.Log($"fuck hex is occupied by = {data.GetOccupier()}");
                        return;
                    }
                    
                    data.SetIsOccupied(true);
                    data.SetOccupier(string.IsNullOrEmpty(SetUniqueGUID) ? Entity.EntityGUID : SetUniqueGUID);
                    tile.SetIsOccupied(true);
                    tile.SetBaseColor(Color.red);
                    tile.gridBaseReference.HexagonsInGrid[tile.GridCoordinates] = data;
                    currentlyBlockedTiles.Add(tile);
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
                if (tile.gridBaseReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
                {
                    if (data.GetIsOccupied())
                    {
                        // Sometimes units trigger vehicle shadow hexagons and they fight to be the occupier, this stops that.
                        string occupierGUID = data.GetOccupier();
                        
                        if ((string.IsNullOrEmpty(SetUniqueGUID) ? Entity.EntityGUID : SetUniqueGUID) != occupierGUID)
                        {
                            return;
                        }

                        // This miiiight cause issues....
                        if(string.IsNullOrEmpty(SetUniqueGUID) == false)
                        {
                            if (occupierGUID == Entity.EntityGUID)
                            {
                                return;
                            }
                        }
                    }

                    OnClearTile(tile, data);
                    currentlyBlockedTiles.Remove(tile);
                }
            }
        }
    }
    
    public void ClearBlockedHexes()
    {
        // Optional: Reset any visual highlighting on the hexes before clearing
        foreach (var tile in currentlyBlockedTiles)
        {
            if (tile.gridBaseReference.HexagonsInGrid.TryGetValue(tile.GridCoordinates, out HexData data))
            {
                if (data.GetIsOccupied())
                {
                    // Sometimes units trigger vehicle shadow hexagons and they fight to be the occupier, this stops that.
                    string occupierGUID = data.GetOccupier();
                        
                    if ((string.IsNullOrEmpty(SetUniqueGUID) ? Entity.EntityGUID : SetUniqueGUID) != occupierGUID)
                    {
                        continue;
                    }

                    // This miiiight cause issues....
                    if(string.IsNullOrEmpty(SetUniqueGUID) == false)
                    {
                        Debug.Log($"fuck occupierGUID = {occupierGUID}");
                        Debug.Log($"fuck Entity.EntityGUID = {Entity.EntityGUID}");
                        
                        if (occupierGUID == Entity.EntityGUID)
                        {
                            Debug.Log($"fuck here");
                            continue;
                        }
                    }
                }

                OnClearTile(tile, data);
            }
        }
        
        currentlyBlockedTiles.Clear();
    }

    private void OnClearTile(HexVisualTile tile, HexData data)
    {
        data.SetIsOccupied(false);
        data.SetOccupier(null);
        tile.SetIsOccupied(false);
        tile.ResetBaseColor();
        tile.gridBaseReference.HexagonsInGrid[tile.GridCoordinates] = data;
    }
}