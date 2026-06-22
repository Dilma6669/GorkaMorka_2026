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
            // Set active only if it matches, disable everything else
            bool isMatch = (data.type == targetLevel);
            if (isMatch)
            {
                data.levelRoot.SetActive(true);
                ActiveGrid = data.levelRoot.GetComponent<SimpleHexGridBase>();
                currentLevel = targetLevel;
                return;
            }
        }

        currentLevel = targetLevel;
        Debug.Log($"Switched to: {targetLevel}");
    }
}