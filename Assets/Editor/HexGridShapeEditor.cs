using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HexGridShape))]
public class HexGridShapeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HexGridShape shape = (HexGridShape)target;

        EditorGUILayout.LabelField("Grid Painter", EditorStyles.boldLabel);
        
        // This button creates a new row
        if (GUILayout.Button("Add Row"))
        {
            shape.Rows.Add(new HexGridShape.HexRow());
        }

        // This renders the rows as a visual grid of boxes
        for (int i = 0; i < shape.Rows.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("X", GUILayout.Width(20))) { shape.Rows.RemoveAt(i); break; }
            
            for (int j = 0; j < shape.Rows[i].Tiles.Count; j++)
            {
                var tile = shape.Rows[i].Tiles[j];
                // Visualizing the tile status with a simple button
                string label = !tile.IsEnabled ? "-" : (tile.IsClimbable ? "C" : (!tile.IsWalkable ? "X" : tile.Height.ToString()));

                if (GUILayout.Button(label, GUILayout.Width(30)))
                {
                    // 1. If Disabled, turn it on
                    if (!tile.IsEnabled) 
                    { 
                        tile.IsEnabled = true; 
                        tile.IsWalkable = true; 
                        tile.Height = 0; 
                    }
                    // 2. If it's Climbable, turn it off (and make it walkable flat)
                    else if (tile.IsClimbable) 
                    { 
                        tile.IsClimbable = false; 
                        tile.IsWalkable = true; 
                        tile.Height = 0;
                    }
                    // 3. If it's Unwalkable, make it Climbable (Height 0)
                    else if (!tile.IsWalkable)
                    {
                        tile.IsWalkable = true;
                        tile.IsClimbable = true;
                        tile.Height = 0;
                    }
                    // 4. If height is less than 5, increment
                    else if (tile.Height < 5) 
                    { 
                        tile.Height++; 
                    }
                    // 5. If height is 5, make it Unwalkable
                    else 
                    { 
                        tile.IsWalkable = false;
                    }

                    EditorUtility.SetDirty(shape);
                }
            }
            if (GUILayout.Button("+", GUILayout.Width(20))) { shape.Rows[i].Tiles.Add(new HexGridShape.HexTileData()); }
            EditorGUILayout.EndHorizontal();
        }

        if (GUI.changed) EditorUtility.SetDirty(shape);
    }
}