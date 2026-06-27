using UnityEngine;

public class HexGeneratorSystem : HexGeneratorBase
{
    private SimpleHexGridSystem simpleHexGridTerrain;

    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridSystem>();
    }

    // The xCoord and zCoord here are already offset by the seed
    public override float GenerateHeight(float xCoord, float zCoord)
    {
        return 0;
    }
}