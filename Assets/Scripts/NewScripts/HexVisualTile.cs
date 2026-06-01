using UnityEngine;

public class HexVisualTile : MonoBehaviour
{
    [HideInInspector] public SimpleHexGrid GridReference;
    
    private Renderer hexRenderer;
    private MaterialPropertyBlock propBlock;
    // Use "_BaseColor" if you are using URP/HDRP shaders, use "_Color" for standard Legacy shaders
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); 
    
    public Vector2Int GridCoordinates;
    public float Height; 
    public bool IsWalkable;
    public bool IsClimbable;
    public bool ColourLocked;
    
    public void Initialize(SimpleHexGrid grid, Vector2Int coords, float height, bool isWalkable, bool isClimbable)
    {
        GridReference = grid;
        GridCoordinates = coords;
        Height = height;
        IsWalkable = isWalkable;
        IsClimbable = isClimbable;

        hexRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        hexRenderer.SetPropertyBlock(null);
    }

    public void SetColor(Color newColor)
    {
        if (ColourLocked || hexRenderer == null) return;
        
        // Apply color via property block to avoid material leaks and shared material changes
        hexRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorID, newColor);
        hexRenderer.SetPropertyBlock(propBlock);
    }

    public void ResetColor()
    {
        if (ColourLocked || hexRenderer == null) return;
        
        // Setting the property block to null removes the override, 
        // reverting the tile to the material's default color.
        hexRenderer.SetPropertyBlock(null);
    }
}