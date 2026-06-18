using UnityEngine;

public class ChunkDataComponent : MonoBehaviour
{
    public Vector2Int chunkID; // Add this!
    public Vector3 worldCenter;
    public bool visible = false; // Track the previous state
}