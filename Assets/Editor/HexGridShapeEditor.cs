using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HexGridShape))]
public class HexGridShapeEditor : Editor
{
    private HexGridShape.HexTileData selectedTile;
    
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
             
                Color buttonColor = Color.white; // Default
                if (!tile.IsEnabled) 
                {
                    buttonColor = Color.black;
                }
                // 3. Property Rules: Only color it if it's actually enabled
                else 
                {
                    if (tile.IsCommandSeat) buttonColor = Color.green;
                    else if (tile.IsClimbable) buttonColor = Color.cyan;
                    else if (tile.IsWalkable == false) buttonColor = Color.red; // Changed to Red as you requested
                }
                
                GUI.backgroundColor = buttonColor;
                
                // Visualizing the tile status with a simple button
                string label = !tile.IsEnabled ? "-" : tile.Height.ToString();

                // 1. Get the Rect for the button so we can handle manual events
                Rect r = GUILayoutUtility.GetRect(30, 30);

                // 2. Draw the button
                if (GUI.Button(r, label))
                {
                    // 3. Detect the mouse button
                    Event e = Event.current;

                    if (e.button == 2) // MIDDLE CLICK: Select this tile
                    {
                        selectedTile = tile;
                    }
                    else if (e.button == 0) // LEFT CLICK: Forward
                    {
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.Height = 0; }
                        else if (tile.Height < 5) { tile.Height++; }
                        else { tile.IsEnabled = false; }
                    }
                    else if (e.button == 1) // RIGHT CLICK: Backward
                    {
                        if (!tile.IsEnabled) { tile.IsEnabled = true; tile.Height = 5; }
                        else if (tile.Height > 0) { tile.Height--; }
                        else { tile.IsEnabled = false; }
                    }

                    EditorUtility.SetDirty(shape);
                }
                
                GUI.backgroundColor = Color.white;
            }
            if (GUILayout.Button("+", GUILayout.Width(20))) { shape.Rows[i].Tiles.Add(new HexGridShape.HexTileData()); }
            EditorGUILayout.EndHorizontal();
        }

        if (GUI.changed) EditorUtility.SetDirty(shape);
        
        if (selectedTile != null)
        {
            // Find the current coordinates of the selected tile
            int rowIdx = -1;
            int colIdx = -1;
            for (int i = 0; i < shape.Rows.Count; i++)
            {
                int foundIdx = shape.Rows[i].Tiles.IndexOf(selectedTile);
                if (foundIdx != -1) { rowIdx = i; colIdx = foundIdx; break; }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Selected Tile Properties (Row: {rowIdx}, Col: {colIdx})", EditorStyles.boldLabel);
    
            EditorGUILayout.HelpBox("Middle-click a grid button to select/refresh a different tile.", MessageType.Info);
    
            selectedTile.IsWalkable = EditorGUILayout.Toggle("Is Walkable", selectedTile.IsWalkable);
            selectedTile.IsClimbable = EditorGUILayout.Toggle("Is Climbable", selectedTile.IsClimbable);
            selectedTile.IsCommandSeat = EditorGUILayout.Toggle("Is Command Seat", selectedTile.IsCommandSeat);
    
            if (GUILayout.Button("Deselect")) { selectedTile = null; }
        }
    }
}