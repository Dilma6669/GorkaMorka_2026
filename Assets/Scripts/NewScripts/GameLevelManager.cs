using UnityEngine;

public class GameLevelManager : MonoBehaviour
{
    public SimpleHexGridBase ActiveGrid;
    
    public HexGridManager.GridType currentLevel;

    private void Awake()
    {
        currentLevel = ActiveGrid.GridType;
    }
    
    public void ToggleLevel(HexGridManager.GridType newLevel)
    {
        if (newLevel == HexGridManager.GridType.Terrain)
        {
            // 1. Hide High-Res Assets (Trees, shacks, barrels)
            // 2. Generate/Show Level 2 Low-Res Hexes
            // 3. Set Camera to Overview Mode
        }
        else
        {
            // 1. Hide Level 2 Hexes
            // 2. Load/Show Level 3 High-Res Terrain
            // 3. Show Props (Trees, shacks, barrels)
        }
        currentLevel = newLevel;
    }
}
