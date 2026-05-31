using UnityEngine;

// Phase 13.1: HexVisualTile Class
// Purpose: Attaches to an individual hexagon GameObject.
// Stores its grid data and provides methods to change its visual appearance (color).
public class HexVisualTile : MonoBehaviour
{
    [HideInInspector] public SimpleHexGrid GridReference;
    
    private Renderer hexRenderer;
    private Color originalColor; // To store the hex's default color
    
    public Vector2Int GridCoordinates;
    public float Height; 
    public bool IsWalkable;
    public bool IsClimbable;
    public bool ColourLocked;
    
    /// <summary>
    /// Initializes this visual tile with its grid data.
    /// Should be called immediately after instantiation by HexGridVisualizer.
    /// </summary>
    public void Initialize(SimpleHexGrid grid, Vector2Int coords, float height, bool isWalkable, bool isClimbable)
    {
        GridReference = grid;
        GridCoordinates = coords;
        Height = height;
        IsWalkable = isWalkable;
        IsClimbable = isClimbable;

        hexRenderer = GetComponent<Renderer>();
        if (hexRenderer != null)
        {
            // --- MODIFIED LOGIC HERE ---
            // If we are in Play Mode, create a new unique material instance for this hex.
            // If we are in Edit Mode, we only read the sharedMaterial's color to avoid material leaks.
            // The actual unique material instantiation will happen when Play Mode starts,
            // or when SetColor/ResetColor is first called in Play Mode if material hasn't been instantiated yet.
            if (Application.isPlaying)
            {
                hexRenderer.material = new Material(hexRenderer.sharedMaterial);
                originalColor = hexRenderer.material.color;
            }
            else // Editor Mode / Not playing
            {
                // In editor mode, just store the shared material's color for reference.
                // We avoid creating a new material instance here.
                originalColor = hexRenderer.sharedMaterial.color;
            }
            // --- END MODIFIED LOGIC ---
        }
        else
        {
            Debug.LogWarning($"HexVisualTile on {name}: No Renderer component found! Cannot change color.", this);
        }
    }

    /// <summary>
    /// Sets the color of this hexagon's material.
    /// Ensures a unique material instance is used if not already.
    /// </summary>
    /// <param name="newColor">The color to apply.</param>
    public void SetColor(Color newColor)
    {
        if (ColourLocked)
            return;
        
        if (hexRenderer != null)
        {
            hexRenderer.material.color = newColor;
        }
    }

    /// <summary>
    /// Resets the hexagon's color to its original, default color.
    /// Ensures a unique material instance is used if not already.
    /// </summary>
    public void ResetColor()
    {
        if (ColourLocked)
            return;
        
        if (hexRenderer != null)
        {
            hexRenderer.material.color = originalColor;
        }
    }
}