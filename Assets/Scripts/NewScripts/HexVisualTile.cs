using UnityEngine;

public class HexVisualTile : MonoBehaviour
{
    [HideInInspector] public SimpleHexGrid GridReference;

    public HexData hexData => GridReference.GetHexData(GridCoordinates);
    
    public Renderer pathOverlayRenderer;
    private Renderer hexRenderer;
    private MaterialPropertyBlock propBlock;
    // Use "_BaseColor" if you are using URP/HDRP shaders, use "_Color" for standard Legacy shaders
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); 
    
    public Vector2Int GridCoordinates;
    public float Height; 
    public bool IsWalkable;
    public bool IsOccupied;
    public bool IsClimbable;
    public bool ColourLocked;
    public bool IsCommandSeat;
    
    public void Initialize(SimpleHexGrid grid, Vector2Int coords, float height, bool isWalkable, bool isClimbable, bool isOccupied, bool isCommandSeat)
    {
        GridReference = grid;
        GridCoordinates = coords;
        Height = height;
        IsWalkable = isWalkable;
        IsClimbable = isClimbable;
        IsOccupied = isOccupied;
        IsCommandSeat = isCommandSeat;

        hexRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        hexRenderer.SetPropertyBlock(null);
    }
    
    public void SetIsOccupied(bool occupied)
    {
        IsOccupied = occupied;
    }
    

    public void SetBaseColor(Color newColor)
    {
        if (ColourLocked || hexRenderer == null) return;
        
        // Apply color via property block to avoid material leaks and shared material changes
        hexRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorID, newColor);
        hexRenderer.SetPropertyBlock(propBlock);
    }

    public void ResetBaseColor()
    {
        if (ColourLocked || hexRenderer == null) return;
        
        // Setting the property block to null removes the override, 
        // reverting the tile to the material's default color.
        hexRenderer.SetPropertyBlock(null);
        
        if(IsWalkable == false) SetBaseColor(Color.red);
        if(IsOccupied) SetBaseColor(Color.red);
        if(IsClimbable) SetBaseColor(Color.blue);
        if(IsCommandSeat) SetBaseColor(Color.green);
    }
    

    public void SetHighlightColour(Color color, bool active)
    {
        if (pathOverlayRenderer == null) return;
    
        pathOverlayRenderer.enabled = active;
        if (active)
        {
            pathOverlayRenderer.material.color = color;
        }
    }

    private void OnDrawGizmos()
    {
        // Ensure you have a reference to your HexData
        string occupied = hexData.GetIsOccupied() ? "Occupied" : "Free";
        string occupier = string.IsNullOrEmpty(hexData.GetOccupier()) ? "-" : hexData.GetOccupier();
        // Assuming your properties are named isClimbable and isWalkable
        string climbable = hexData.GetIsClimbable() ? "Climbable" : "Not Climbable";
        string walkable = hexData.GetIsWalkable() ? "Walkable" : "Blocked";

        Color textColor = hexData.GetIsOccupied() || !hexData.GetIsWalkable() ? Color.red : Color.green;

        // Draw the text in the Scene View
        UnityEditor.Handles.color = textColor;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            $"Coords: {hexData.GridCoordinates}\n" +
            $"Occupied: {occupied}\n" +
            $"Occupier: {occupier}\n" +
            $"Move: {walkable}\n" +
            $"Climb: {climbable}");
    }
}