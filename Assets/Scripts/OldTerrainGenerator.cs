using UnityEngine;

public class OldTerrainGenerator : MonoBehaviour
{
    public TerrainSettings terrainSettings;

    public int seed = 0;
    
    public float seedOffsetMultiplier = 1000f;
    
    // The xCoord and zCoord here are already offset by the seed
    public float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock) 
    {
        // Generate smooth sand dunes using multiple octaves of Perlin noise
        float duneNoise = GenerateOctaveNoise(xCoord, zCoord, terrainSettings.duneScale, terrainSettings.duneOctaves, terrainSettings.dunePersistence);
        float duneHeight = duneNoise * terrainSettings.duneHeight;

        // Generate rocky outcropping noise
        float rockNoise = Mathf.PerlinNoise(xCoord * terrainSettings.rockScale, zCoord * terrainSettings.rockScale);

        // Create sharp rock transitions - only areas above threshold become rocks
        float rockHeight = 0f;
        isRock = false;

        if (rockNoise > terrainSettings.rockThreshold)
        {
            // Sharpen the transition using power function
            float rockIntensity = Mathf.Pow((rockNoise - terrainSettings.rockThreshold) / (1f - terrainSettings.rockThreshold), terrainSettings.rockSharpness);
            rockHeight = rockIntensity * terrainSettings.rockHeight;
            isRock = true;
        }

        // Create a blending mask to determine where rocks vs dunes appear
        // Add another large arbitrary offset to this noise for a different pattern
        float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);

        // Blend between dunes and rocks based on terrain smoothness and blend noise
        float blendFactor = Mathf.Lerp(0.3f, 1f, terrainSettings.terrainSmoothness);
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