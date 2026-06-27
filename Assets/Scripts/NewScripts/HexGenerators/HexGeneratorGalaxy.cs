using UnityEngine;

public class HexGeneratorGalaxy : HexGeneratorBase
{
    private SimpleHexGridGalaxy simpleHexGridTerrain;

    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridGalaxy>();
    }

    // The xCoord and zCoord here are already offset by the seed
    public override float GenerateHeight(float xCoord, float zCoord)
    {
        return 0;
    }
}