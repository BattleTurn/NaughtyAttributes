using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public static class SerializedPropertyExtensions
    {
        public static Type GetElementType(this SerializedProperty property)
        {
            if (!property.isArray) return null;

            try
            {
                // Method 1: Try reflection approach (Unity 2020+)
                var getFieldInfoMethod = typeof(SerializedProperty).GetMethod("GetFieldInfoAndStaticType", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (getFieldInfoMethod != null)
                {
                    object[] args = new object[] { null, null };
                    getFieldInfoMethod.Invoke(property, args);

                    var fieldType = args[1] as Type;
                    if (fieldType != null)
                    {
                        if (fieldType.IsArray)
                            return fieldType.GetElementType();

                        if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length > 0)
                            return fieldType.GetGenericArguments()[0];
                    }
                }

                // Method 2: Fallback - analyze existing elements
                if (property.arraySize > 0)
                {
                    var firstElement = property.GetArrayElementAtIndex(0);
                    if (firstElement.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        // Try to get type from existing object reference
                        if (firstElement.objectReferenceValue != null)
                            return firstElement.objectReferenceValue.GetType();
                            
                        // Check if it has a specific object type restriction
                        var objRefType = GetObjectReferenceType(firstElement);
                        if (objRefType != null)
                            return objRefType;
                    }
                }

                // Method 3: Final fallback - return UnityEngine.Object for object references
                if (property.arraySize == 0 || (property.arraySize > 0 && 
                    property.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ObjectReference))
                {
                    return typeof(UnityEngine.Object);
                }
            }
            catch (Exception)
            {
                // Silent fallback for any reflection errors
            }

            return null;
        }

        private static Type GetObjectReferenceType(SerializedProperty objectRefProperty)
        {
            try
            {
                // Try to get the specific object reference type through reflection
                var typeField = typeof(SerializedProperty).GetField("m_ObjectType", BindingFlags.NonPublic | BindingFlags.Instance);
                if (typeField != null)
                {
                    var typeString = typeField.GetValue(objectRefProperty) as string;
                    if (!string.IsNullOrEmpty(typeString))
                    {
                        // Parse type string like "PPtr<$GameObject>" or "PPtr<$Transform>"
                        if (typeString.StartsWith("PPtr<$") && typeString.EndsWith(">"))
                        {
                            var typeName = typeString.Substring(6, typeString.Length - 7);
                            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                                      Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
                            return type;
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }
            return null;
        }
    }
}