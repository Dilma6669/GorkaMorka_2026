using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainSettings", menuName = "Terrain/Terrain Settings")]
public class TerrainSettings : ScriptableObject
{
    [Tooltip(
        "The radius of the hexagonal grid. A radius of 0 is just the center hex. A radius of 1 includes the 6 direct neighbors.")]
    public int gridRadius = 5;
    // ADD THIS: A flag to tell the generator how to behave
    public bool isFlatWorldMap = false; 
    
    // ADD THIS: An ID or Name for the level it belongs to
    public int targetLayer = 3;
    
    [Tooltip("The size (radius) of each hexagon.")]
    public float hexSize = 1f;

    [Tooltip("An optional vertical offset for the entire grid relative to its GameObject's Y position.")]
    public float entireGridHeightOffset = 0f;
    public float singleHexHeightAdjustment = 1f; // Add this new field
    
    [Header("Dunes (Smooth)")]
    [Tooltip("Large scale for rolling hills")]
    public float duneScale = 0.01f;
    [Tooltip("Maximum dune height")]
    public float duneHeight = 11f;
    [Tooltip("Number of noise layers for dunes")]
    public int duneOctaves = 3;
    [Tooltip("How much each octave contributes")]
    public float dunePersistence = 0.01f;

    [Header("Rocks (Jagged)")]
    [Tooltip("Larger for smaller/more rock clusters, Less for larger/fewer rock clusters")]
    public float rockScale = 0.02f;
    [Tooltip("Maximum rock height")]
    public float rockHeight = 7f;
    [Tooltip("Only heights above this become rocks")]
    public float rockThreshold = 0.7f;
    [Tooltip("How sharp the rock transitions are, Less more sharp")]
    public float rockSharpness = 0.1f;
    
    [Header("Mixing")]
    [Tooltip("0 = all rocks, 1 = all dunes")]
    public float terrainSmoothness = 0.8f;
    
    [Header("Population Settings")]
    [Range(0f, 1f)]
    [Tooltip("Percentage of total hexes to have trees (0.0 to 1.0)")]
    public float treePercentage = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Percentage of total hexes to have rocks (0.0 to 1.0)")]
    public float rockPercentage = 0.1f;
    
    [Header("Water Settings")]
    public float waterLevel = 1.0f;
}