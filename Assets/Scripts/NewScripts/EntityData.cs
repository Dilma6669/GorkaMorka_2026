
using UnityEngine;

public class EntityData : ScriptableObject
{
    public GameObject unitPrefab; // The visual template
    
    public string unitName;
    public int maxHealth;
    public int currentHealth;
    public float baseMoveSpeed;
    
    [Header("Spawn Settings")]
    public SimpleHexGrid spawnGrid;
    public Vector2Int spawnCoordinates;
}
