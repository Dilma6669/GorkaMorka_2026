using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    private SimpleHexGridBase simpleHexGridBase;

    public int seed = 0;
    
    public float seedOffsetMultiplier = 1000f;
    
    private void Awake()
    {
        simpleHexGridBase = GetComponent<SimpleHexGridBase>();
    }
    
    // The xCoord and zCoord here are already offset by the seed
    public float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock) 
    {
        // Generate smooth sand dunes using multiple octaves of Perlin noise
        float duneNoise = GenerateOctaveNoise(xCoord, zCoord, simpleHexGridBase.terrainSettings.duneScale, 
            simpleHexGridBase.terrainSettings.duneOctaves, simpleHexGridBase.terrainSettings.dunePersistence);
        float duneHeight = duneNoise * simpleHexGridBase.terrainSettings.duneHeight;

        // Generate rocky outcropping noise
        float rockNoise = Mathf.PerlinNoise(xCoord * simpleHexGridBase.terrainSettings.rockScale, zCoord * simpleHexGridBase.terrainSettings.rockScale);

        // Create sharp rock transitions - only areas above threshold become rocks
        float rockHeight = 0f;
        isRock = false;

        if (rockNoise > simpleHexGridBase.terrainSettings.rockThreshold)
        {
            // Sharpen the transition using power function
            float rockIntensity = Mathf.Pow((rockNoise - simpleHexGridBase.terrainSettings.rockThreshold) 
                   / (1f - simpleHexGridBase.terrainSettings.rockThreshold), 
                simpleHexGridBase.terrainSettings.rockSharpness);
            rockHeight = rockIntensity * simpleHexGridBase.terrainSettings.rockHeight;
            isRock = true;
        }

        // Create a blending mask to determine where rocks vs dunes appear
        // Add another large arbitrary offset to this noise for a different pattern
        float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);

        // Blend between dunes and rocks based on terrain smoothness and blend noise
        float blendFactor = Mathf.Lerp(0.3f, 1f, simpleHexGridBase.terrainSettings.terrainSmoothness);
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