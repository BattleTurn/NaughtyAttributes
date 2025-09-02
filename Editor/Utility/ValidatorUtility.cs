using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class ValidatorUtility
    {
        public delegate bool CompareValue(float a, float b);

        /// <summary>
        /// Clamp against 'value' using 'condition'. Returns true if property was modified.
        /// </summary>
        public static bool ClampValue<T>(float value, CompareValue condition, SerializedProperty property)
        {
            bool changed = false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    {
                        int v = property.intValue;
                        if (condition(v, value)) { property.intValue = (int)value; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Float:
                    {
                        float v = property.floatValue;
                        if (condition(v, value)) { property.floatValue = value; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Vector2:
                    {
                        Vector2 before = property.vector2Value;
                        Vector2 after = Vector2.Min(before, new Vector2(value, value)); // for Max; đảo chiều cho Min trong caller bằng 'condition'
                        if (after != before) { property.vector2Value = after; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Vector3:
                    {
                        Vector3 before = property.vector3Value;
                        Vector3 after = Vector3.Min(before, new Vector3(value, value, value));
                        if (after != before) { property.vector3Value = after; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Vector4:
                    {
                        Vector4 before = property.vector4Value;
                        Vector4 after = Vector4.Min(before, new Vector4(value, value, value, value));
                        if (after != before) { property.vector4Value = after; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Vector2Int:
                    {
                        Vector2Int before = property.vector2IntValue;
                        Vector2Int after = Vector2Int.Min(before, new Vector2Int((int)value, (int)value));
                        if (after != before) { property.vector2IntValue = after; changed = true; }
                        break;
                    }
                case SerializedPropertyType.Vector3Int:
                    {
                        Vector3Int before = property.vector3IntValue;
                        Vector3Int after = Vector3Int.Min(before, new Vector3Int((int)value, (int)value, (int)value));
                        if (after != before) { property.vector3IntValue = after; changed = true; }
                        break;
                    }
                default:
                    Debug.LogWarning($"{nameof(T)} can be used only on int, float, Vector or VectorInt fields", property.serializedObject.targetObject);
                    break;
            }

            return changed;
        }
    }
}