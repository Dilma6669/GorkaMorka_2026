using UnityEngine;
using UnityEngine.Serialization;


[CreateAssetMenu(fileName = "NewMapSettings", menuName = "MapSettings/Map Settings Base")]
public class MapSettingsBase : ScriptableObject
{
    [Tooltip(
        "The radius of the hexagonal grid. A radius of 0 is just the center hex. A radius of 1 includes the 6 direct neighbors.")]
    public int gridRadius = 5;

    // ADD THIS: An ID or Name for the level it belongs to
    public int worldLevel = 3;

    [Tooltip("The size (radius) of each hexagon.")]
    public float hexSize = 1f;

    [Tooltip("An optional vertical offset for the entire grid relative to its GameObject's Y position.")]
    public float entireGridHeightOffset = 0f;
    public float singleHexHeightAdjustment = 1f; // Add this new field
    
    public int meshChunkSize = 10;
}
