using UnityEngine;

public class HexGeneratorWorld : HexGeneratorBase
{
    private SimpleHexGridWorld simpleHexGridTerrain;

    private void Awake()
    {
        simpleHexGridTerrain = GetComponent<SimpleHexGridWorld>();
    }

    // The xCoord and zCoord here are already offset by the seed
    public override float GenerateHeight(float xCoord, float zCoord)
    {
        return 0;
    }
}