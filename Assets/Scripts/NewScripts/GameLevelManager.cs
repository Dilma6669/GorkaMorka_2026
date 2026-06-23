using UnityEngine;
using System.Collections.Generic;

public class GameLevelManager : MonoBehaviour
{
    public HexGridManager.GridType startingLevel;
    
    public SimpleHexGridBase ActiveGrid;
    
    public HexGridManager.GridType currentLevel;

    [System.Serializable]
    public struct LevelData
    {
        public HexGridManager.GridType type;
        public GameObject levelRoot;
    }

    public List<LevelData> levels;

    private void Start()
    {
        SwitchToLevel(startingLevel);
    }
    
    public void SwitchToLevel(HexGridManager.GridType targetLevel)
    {
        foreach (var data in levels)
        {
            data.levelRoot.SetActive(false);
            
            // Set active only if it matches, disable everything else
            bool isMatch = (data.type == targetLevel);
            if (isMatch)
            {
                data.levelRoot.SetActive(true);
                ActiveGrid = data.levelRoot.GetComponent<SimpleHexGridBase>();
                currentLevel = targetLevel;
            }
        }

        currentLevel = targetLevel;
        Debug.Log($"Switched to: {targetLevel}");
    }
    
    // Inside GameLevelManager.cs
    [ContextMenu("Switch to Terrain")]
    private void MenuSwitchToTerrain() => SwitchToLevel(HexGridManager.GridType.Terrain);

    [ContextMenu("Switch to World")]
    private void MenuSwitchToWorld() => SwitchToLevel(HexGridManager.GridType.World);
}