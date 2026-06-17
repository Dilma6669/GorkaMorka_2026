using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for .Any() in VehiclePathFinder, good to have here too

public class HexagonController : MonoBehaviour
{
    [Header("Grid Settings")] public int gridWidth = 100;
    public int gridHeight = 100;
    public float hexSize = 1f;

    [Tooltip("Adjusts the vertical height of the hexagon mesh. Increase to make hexagons appear taller.")]
    public float hexMeshHeight = 1.0f;
    
    [Header("Sand Dunes (Smooth Hills)")] public float duneScale = 0.05f; // Large scale for rolling hills
    public float duneHeight = 8f; // Maximum dune height
    public float duneOctaves = 3; // Number of noise layers for dunes
    public float dunePersistence = 0.5f; // How much each octave contributes

    [Header("Rocky Outcroppings (Sharp Peaks)")]
    public float rockScale = 0.08f; // Larger scale for bigger rock clusters

    public float rockHeight = 15f; // Maximum rock height
    public float rockThreshold = 0.6f; // Only heights above this become rocks
    public float rockSharpness = 2f; // How sharp the rock transitions are

    [Header("Noise Mixing")] public float terrainSmoothness = 0.8f; // 0 = all rocks, 1 = all dunes

    // Data structure to store hex information without GameObjects
    [System.Serializable]
    public struct HexData
    {
        public Vector3 position;
        public float height;
        public float rawHeight; // Height before neighbor limiting
        public int gridX, gridZ;
        public bool isWalkable;
        public bool isRock; // True if this hex is rocky terrain

        public HexData(Vector3 pos, float h, float rawH, int x, int z, bool rock)
        {
            position = pos;
            height = h;
            rawHeight = rawH;
            gridX = x;
            gridZ = z;
            isWalkable = true;
            isRock = rock;
        }
    }
    
    
    // The xCoord and zCoord here are already offset by the seed
    float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock)
    {
        // Generate smooth sand dunes using multiple octaves of Perlin noise
        float duneNoise = GenerateOctaveNoise(xCoord, zCoord, duneScale, duneOctaves, dunePersistence);
        float duneHeight = duneNoise * this.duneHeight;

        // Generate rocky outcropping noise
        float rockNoise = Mathf.PerlinNoise(xCoord * rockScale, zCoord * rockScale);

        // Create sharp rock transitions - only areas above threshold become rocks
        float rockHeight = 0f;
        isRock = false;

        if (rockNoise > rockThreshold)
        {
            // Sharpen the transition using power function
            float rockIntensity = Mathf.Pow((rockNoise - rockThreshold) / (1f - rockThreshold), rockSharpness);
            rockHeight = rockIntensity * this.rockHeight;
            isRock = true;
        }

        // Create a blending mask to determine where rocks vs dunes appear
        // Add another large arbitrary offset to this noise for a different pattern
        float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);

        // Blend between dunes and rocks based on terrain smoothness and blend noise
        float blendFactor = Mathf.Lerp(0.3f, 1f, terrainSmoothness);
        float finalBlend = Mathf.Clamp01(blendNoise + blendFactor - 0.5f);

        // Final height is a mix of smooth dunes and sharp rocks
        return Mathf.Lerp(duneHeight + rockHeight, duneHeight, finalBlend);
    }

    // The xCoord and zCoord here are already offset by the seed
    float GenerateOctaveNoise(float xCoord, float zCoord, float scale, float octaves, float persistence)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = scale;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(xCoord * frequency, zCoord * frequency) * amplitude;

            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        return value / maxValue; // Normalize to 0-1 range
    }
}