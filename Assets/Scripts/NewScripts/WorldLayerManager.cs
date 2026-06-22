using UnityEngine;

public class WorldLayerManager : MonoBehaviour
{
    public enum GameLevel { WorldMap, Terrain }
    public GameLevel currentLevel;

    public void ToggleLevel(GameLevel newLevel)
    {
        if (newLevel == GameLevel.WorldMap)
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
