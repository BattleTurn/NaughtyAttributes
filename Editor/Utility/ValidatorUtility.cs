using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class ValidatorUtility
    {
        public delegate bool CompareValue(float a, float b);
        
        public static void ClampValue(float value, CompareValue condition, SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (condition.Invoke(property.intValue, value))
                {
                    property.intValue = (int)value;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                if (condition.Invoke(property.floatValue, value))
                {
                    property.floatValue = value;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector2)
            {
                property.vector2Value = Vector2.Min(property.vector2Value, new Vector2(value, value));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = Vector3.Min(property.vector3Value, new Vector3(value, value, value));
            }
            else if (property.propertyType == SerializedPropertyType.Vector4)
            {
                property.vector4Value = Vector4.Min(property.vector4Value, new Vector4(value, value, value, value));
            }
            else if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                property.vector2IntValue = Vector2Int.Min(property.vector2IntValue, new Vector2Int((int)value, (int)value));
            }
            else if (property.propertyType == SerializedPropertyType.Vector3Int)
            {
                property.vector3IntValue = Vector3Int.Min(property.vector3IntValue, new Vector3Int((int)value, (int)value, (int)value));
            }
            else
            {
                string warning = nameof(MaxValueAttribute) + " can be used only on int, float, Vector or VectorInt fields";
                Debug.LogWarning(warning, property.serializedObject.targetObject);
            }
        }
    }
}