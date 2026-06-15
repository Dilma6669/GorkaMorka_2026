using UnityEngine;
using System.Collections.Generic;

public class HexOverlayManager : MonoBehaviour
{
    [Header("Overlay Settings")]
    public GameObject overlayPrefab; // Drag your highlight prefab here
    public Transform container;     // Assign a parent object for organization
    
    // Tracks active GameObjects so we don't duplicate them
    private Dictionary<HexGridManager.HexGridAndCoords, GameObject> activeOverlays = new();

    void Awake()
    {

    }

    /// <summary>
    /// Shows or hides an overlay on a specific hex.
    /// </summary>
    /// <param name="coords">The axial coordinates.</param>
    /// <param name="color">The color to tint the overlay.</param>s
    /// <param name="show">True to spawn/update, False to remove.</param>
    public void SetOverlay(HexGridManager.HexGridAndCoords gridAndCoords, Color color, bool show)
    {
        if (show)
        {
            if (activeOverlays.TryGetValue(gridAndCoords, out GameObject existingOverlay))
            {
                // Just update the color if it already exists
                existingOverlay.GetComponent<Renderer>().material.color = color;
            }
            else
            {
                // Spawn a new one
                HexData hexData = gridAndCoords.GridBase.GetHexData(gridAndCoords.Coords);
                Vector3 pos = gridAndCoords.GridBase.GetHexTopSurfacePosition(gridAndCoords.Coords, hexData.Height); 
                GameObject newOverlay = Instantiate(overlayPrefab, pos, Quaternion.identity, container);
                newOverlay.GetComponent<Renderer>().material.color = color;
                activeOverlays[gridAndCoords] = newOverlay;
            }
        }
        else if (activeOverlays.TryGetValue(gridAndCoords, out GameObject overlay))
        {
            Destroy(overlay);
            activeOverlays.Remove(gridAndCoords);
        }
    }

    public void ClearAll()
    {
        foreach (var overlay in activeOverlays.Values)
        {
            Destroy(overlay);
        }
        activeOverlays.Clear();
    }
}