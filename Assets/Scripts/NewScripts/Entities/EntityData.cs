using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EntityData : ScriptableObject
{
    public string entityGUID;
    public EntitySpawner.EntityType entityType;

    public string entityName;
    public int maxHealth;
    public int currentHealth;
    public float baseMoveSpeed;

    public List<LevelPositionPair> levelCoords = new List<LevelPositionPair>();
    public List<LevelPositionPair> lastJumpLevelCoords = new List<LevelPositionPair>();
    
    public void SetLevelCoords(LevelPositionPair levelPair)
    {
        Debug.Log($"Setting LEVEL coords for: {entityName} : {levelPair.level} : {levelPair.coords}");
        var pair = levelCoords.Find(p => p.level == levelPair.level);
        if (pair != null) 
        {
            pair.coords = levelPair.coords;
        }
        else 
        {
            levelCoords.Add(new LevelPositionPair { level = levelPair.level, coords = levelPair.coords });
        }
    }

    public Vector2Int? GetLevelCoords(HexGridManager.GridType level)
    {
        var pair = levelCoords.Find(p => p.level == level);
        return pair != null ? pair.coords : Vector2Int.zero;
    }
    
    public void SetLastJumpLevelCoords(LevelPositionPair levelPair)
    {
        Debug.Log($"Setting JUMP coords for: {entityName} :  {levelPair.level} {levelPair.coords}");
        var pair = lastJumpLevelCoords.Find(p => p.level == levelPair.level);
        if (pair != null) 
        {
            pair.coords = levelPair.coords;
        }
        else 
        {
            lastJumpLevelCoords.Add(new LevelPositionPair { level = levelPair.level, coords = levelPair.coords });
        }
    }

    public Vector2Int? GetLastJumpLevelCoords(HexGridManager.GridType level)
    {
        var pair = lastJumpLevelCoords.Find(p => p.level == level);
        return pair != null ? pair.coords : Vector2Int.zero;
    }
}

[System.Serializable]
public class LevelPositionPair
{
    public HexGridManager.GridType level;
    public Vector2Int? coords;
}

[System.Serializable]
public class LevelSeedPair {
    public HexGridManager.GridType level;
    public int seed;
}

