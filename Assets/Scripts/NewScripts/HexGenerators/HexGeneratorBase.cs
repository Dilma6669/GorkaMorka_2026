using UnityEngine;

public abstract class HexGeneratorBase : MonoBehaviour
{
    public int seed = 0;

    public float seedOffsetMultiplier = 1000f;
    
    public abstract float GenerateHeight(float xCoord, float zCoord);
}
