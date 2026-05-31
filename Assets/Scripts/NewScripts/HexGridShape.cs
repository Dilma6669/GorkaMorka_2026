using UnityEngine;

[CreateAssetMenu(fileName = "New HexGridShape", menuName = "HexGrid/Shape")]
public class HexGridShape : ScriptableObject
{
    // Now we use an array of strings to represent the shape.
    // This will be much easier to read in the inspector.
    [Tooltip("Define your hex grid shape using 0s (no hex) and 1s (hex).")]
    public string[] shapeRows;
}