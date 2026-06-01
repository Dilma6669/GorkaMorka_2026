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

                // 1. Get the Rect for the button so we can handle manual events
                Rect r = GUILayoutUtility.GetRect(30, 20);

                // 2. Draw the button
                if (GUI.Button(r, label))
                {
                    // 3. Detect the mouse button
                    Event e = Event.current;
                    bool forward = (e.button == 0); // Left Click = Forward (your current logic)
                    bool backward = (e.button == 1); // Right Click = Backward

                    if (forward)
                    {
                        // --- YOUR FORWARD CYCLE LOGIC ---
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.IsWalkable = true; tile.IsClimbable = false; tile.Height = 0; }
                        else if (tile.IsWalkable && !tile.IsClimbable && tile.Height < 5) { tile.Height++; }
                        else if (tile.Height >= 5) { tile.IsClimbable = true; tile.IsWalkable = true; tile.Height = 0; }
                        else if (tile.IsClimbable) { tile.IsClimbable = false; tile.IsWalkable = false; }
                        else { tile.IsEnabled = false; }
                    }
                    else if (backward)
                    {
                        // --- BACKWARD CYCLE LOGIC ---
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.IsWalkable = false; tile.IsClimbable = false; } // Go from - to X
                        else if (!tile.IsWalkable) { tile.IsWalkable = true; tile.IsClimbable = true; } // Go from X to C
                        else if (tile.IsClimbable) { tile.IsClimbable = false; tile.IsWalkable = true; tile.Height = 5; } // Go from C to H:5
                        else if (tile.Height > 0) { tile.Height--; } // Decrease Height
                        else { tile.IsEnabled = false; } // Go from H:0 to -
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