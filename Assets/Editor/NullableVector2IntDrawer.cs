using UnityEditor;
using UnityEngine;

// Tell Unity this drawer is for your specific coordinate class/struct
[CustomPropertyDrawer(typeof(NullableVector2Int))] 
public class OptionalVector2Drawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty isSet = property.FindPropertyRelative("isSet");
        SerializedProperty coords = property.FindPropertyRelative("coords");

        // Draw the toggle (the checkbox)
        Rect toggleRect = new Rect(position.x, position.y, 20, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(toggleRect, isSet, GUIContent.none);

        // Only draw the Vector2Int fields if the checkbox is checked
        if (isSet.boolValue)
        {
            Rect fieldRect = new Rect(position.x + 25, position.y, position.width - 25, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(fieldRect, coords, label);
        }
        else
        {
            // If unchecked, just draw a label saying "Null"
            Rect labelRect = new Rect(position.x + 25, position.y, position.width - 25, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label, new GUIContent("Null"));
        }
    }
}