using UnityEngine;
using UnityEngine.Serialization;

public class EntityData : ScriptableObject
{
    public EntitySpawner.EntityType entityType;
    
    public GameObject unitPrefab; // The visual template
    
    public string unitName;
    public int maxHealth;
    public int currentHealth;
    public float baseMoveSpeed;
    
    [Header("Spawn Settings")]
    public SimpleHexGridBase spawnGridBase;
    public Vector2Int spawnCoordinates;
}
