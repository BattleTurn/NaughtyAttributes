using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace NaughtyAttributes.Editor
{
    [CustomPropertyDrawer(typeof(ColorAttribute))]
    public class ColorPropertyDrawer : PropertyDrawerBase
    {
        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            return GetPropertyHeight(property);
        }

        protected override void OnGUI_Internal(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            EditorGUI.PropertyField(rect, property);
            ColorAttribute colorAttribute = (ColorAttribute)attribute;

            UnityEngine.Object presetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(colorAttribute.Path);
            SerializedObject so = new SerializedObject(presetObject);
            SerializedProperty presets = so.FindProperty("m_Presets");
            int count = presets.arraySize;

            EditorGUILayout.BeginHorizontal(); // Start horizontal layout
            for (int i = 0; i < count; i++)
            {
                Color color = presets.GetArrayElementAtIndex(i).FindPropertyRelative("m_Color").colorValue;

                // Display color button
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.background = NaughtyEditorGUI.MakeTex(20, 20, color);

                if (GUILayout.Button("", buttonStyle))
                {
                    property.colorValue = color;
                }
            }

            EditorGUILayout.EndHorizontal(); // End horizontal layout
            EditorGUILayout.Space();

            EditorGUI.EndProperty();
        }
    }
}
