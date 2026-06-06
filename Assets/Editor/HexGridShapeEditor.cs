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
                string label = !tile.IsEnabled ? "-" 
                    : (tile.IsCommandSeat ? "D" 
                        : (tile.IsClimbable ? "C" 
                            : (!tile.IsWalkable ? "X" : tile.Height.ToString())));

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
                        // --- UPDATED FORWARD CYCLE LOGIC ---
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.IsWalkable = true; tile.IsClimbable = false; tile.IsCommandSeat = false; tile.Height = 0; }
                        else if (tile.IsWalkable && !tile.IsClimbable && !tile.IsCommandSeat && tile.Height < 5) { tile.Height++; }
                        else if (tile.Height >= 5) { tile.IsClimbable = true; tile.IsWalkable = true; tile.IsCommandSeat = false; tile.Height = 0; }
                        else if (tile.IsClimbable) { tile.IsClimbable = false; tile.IsCommandSeat = true; tile.IsWalkable = true; } // D is now also Walkable
                        else if (tile.IsCommandSeat) { tile.IsCommandSeat = false; tile.IsWalkable = false; }
                        else { tile.IsEnabled = false; }
                    }
                    else if (backward)
                    {
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.IsWalkable = true; tile.IsClimbable = false; tile.IsCommandSeat = true; } 
                        else if (tile.IsCommandSeat) { tile.IsCommandSeat = false; tile.IsClimbable = true; tile.IsWalkable = false; } 
                        else if (tile.IsClimbable) { tile.IsClimbable = false; tile.IsWalkable = true; tile.Height = 5; } 
                        else if (tile.IsWalkable && !tile.IsClimbable && !tile.IsCommandSeat && tile.Height > 0) { tile.Height--; } 
                        else if (tile.IsWalkable && tile.Height == 0) { tile.IsWalkable = false; tile.IsCommandSeat = false; } 
                        else { tile.IsEnabled = false; }
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