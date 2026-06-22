using UnityEngine;

public class HexGeneratorWorld : HexGeneratorBase
{
    private SimpleHexGridWorld simpleHexGridTerrain;
    
    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridWorld>();
    }
    
    // The xCoord and zCoord here are already offset by the seed
    public float GenerateHeight(float xCoord, float zCoord)
    {
        return 0;
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