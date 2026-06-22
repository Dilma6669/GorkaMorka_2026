using UnityEngine;

public class HexGeneratorTerrain : MonoBehaviour
{
    private SimpleHexGridTerrain simpleHexGridTerrain;

    public int seed = 0;
    
    public float seedOffsetMultiplier = 1000f;
    
    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridTerrain>();
    }
    
    // The xCoord and zCoord here are already offset by the seed
    public float GenerateDesertHeight(float xCoord, float zCoord, out bool isRock) 
    {
        // Generate smooth sand dunes using multiple octaves of Perlin noise
        float duneNoise = GenerateOctaveNoise(xCoord, zCoord, simpleHexGridTerrain.TerrainSettings.duneScale, 
            simpleHexGridTerrain.TerrainSettings.duneOctaves, simpleHexGridTerrain.TerrainSettings.dunePersistence);
        float duneHeight = duneNoise * simpleHexGridTerrain.TerrainSettings.duneHeight;

        // Generate rocky outcropping noise
        float rockNoise = Mathf.PerlinNoise(xCoord * simpleHexGridTerrain.TerrainSettings.rockScale, 
            zCoord * simpleHexGridTerrain.TerrainSettings.rockScale);

        // Create sharp rock transitions - only areas above threshold become rocks
        float rockHeight = 0f;
        isRock = false;

        if (rockNoise > simpleHexGridTerrain.TerrainSettings.rockThreshold)
        {
            // Sharpen the transition using power function
            float rockIntensity = Mathf.Pow((rockNoise - simpleHexGridTerrain.TerrainSettings.rockThreshold) 
                   / (1f - simpleHexGridTerrain.TerrainSettings.rockThreshold), 
                simpleHexGridTerrain.TerrainSettings.rockSharpness);
            rockHeight = rockIntensity * simpleHexGridTerrain.TerrainSettings.rockHeight;
            isRock = true;
        }

        // Create a blending mask to determine where rocks vs dunes appear
        // Add another large arbitrary offset to this noise for a different pattern
        float blendNoise = Mathf.PerlinNoise((xCoord + 50000f) * 0.1f, (zCoord + 60000f) * 0.1f);

        // Blend between dunes and rocks based on terrain smoothness and blend noise
        float blendFactor = Mathf.Lerp(0.3f, 1f, simpleHexGridTerrain.TerrainSettings.terrainSmoothness);
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