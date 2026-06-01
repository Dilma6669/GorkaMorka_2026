using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewHexGridShape", menuName = "HexGrid/Shape")]
public class HexGridShape : ScriptableObject
{
    [System.Serializable]
    public class HexTileData
    {
        public int Height = 0;       // 0-5
        public bool IsWalkable = true;
        public bool IsClimbable = false;
        public bool IsEnabled = true; // Add this!
        // You can add more types later (e.g., 'IsWater', 'IsCover')
    }

    [System.Serializable]
    public class HexRow
    {
        public List<HexTileData> Tiles = new List<HexTileData>();
    }

    public List<HexRow> Rows = new List<HexRow>();
}