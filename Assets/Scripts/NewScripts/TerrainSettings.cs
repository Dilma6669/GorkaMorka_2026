using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainSettings", menuName = "Terrain/Terrain Settings")]
public class TerrainSettings : ScriptableObject
{
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
}