﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    public static class PropertyUtility
    {
        public static void SetElementData(this SerializedProperty element, object data)
        {
            if (data == null) return;

            switch (element.propertyType)
            {
                case SerializedPropertyType.Integer:
                    element.intValue = (int)data;
                    break;
                case SerializedPropertyType.Float:
                    element.floatValue = (float)data;
                    break;
                case SerializedPropertyType.String:
                    element.stringValue = (string)data;
                    break;
                case SerializedPropertyType.Boolean:
                    element.boolValue = (bool)data;
                    break;
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    element.objectReferenceValue = (Object)data;
                    break;
                case SerializedPropertyType.Vector2:
                    element.vector2Value = (Vector2)data;
                    break;
                case SerializedPropertyType.Vector3:
                    element.vector3Value = (Vector3)data;
                    break;
                case SerializedPropertyType.Vector4:
                    element.vector4Value = (Vector4)data;
                    break;
                case SerializedPropertyType.Vector2Int:
                    element.vector2IntValue = (Vector2Int)data;
                    break;
                case SerializedPropertyType.Vector3Int:
                    element.vector3IntValue = (Vector3Int)data;
                    break;
                case SerializedPropertyType.Enum:
                    element.enumValueIndex = (int)data;
                    break;
                case SerializedPropertyType.Color:
                    element.colorValue = (Color)data;
                    break;
                case SerializedPropertyType.Rect:
                    element.rectValue = (Rect)data;
                    break;
                case SerializedPropertyType.Bounds:
                    element.boundsValue = (Bounds)data;
                    break;
                case SerializedPropertyType.Quaternion:
                    element.quaternionValue = (Quaternion)data;
                    break;
                case SerializedPropertyType.RectInt:
                    element.rectIntValue = (RectInt)data;
                    break;
                case SerializedPropertyType.BoundsInt:
                    element.boundsIntValue = (BoundsInt)data;
                    break;
                case SerializedPropertyType.ManagedReference:
                    element.managedReferenceValue = data;
                    break;
                case SerializedPropertyType.Hash128:
                    element.hash128Value = (Hash128)data;
                    break;
            }
        }

        public static object GetElementData(this SerializedProperty element)
        {
            // Store element data based on property type
            switch (element.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return element.intValue;
                case SerializedPropertyType.Float:
                    return element.floatValue;
                case SerializedPropertyType.String:
                    return element.stringValue;
                case SerializedPropertyType.Boolean:
                    return element.boolValue;
                case SerializedPropertyType.ObjectReference:
                    return element.objectReferenceValue;
                case SerializedPropertyType.Vector2:
                    return element.vector2Value;
                case SerializedPropertyType.Vector3:
                    return element.vector3Value;
                case SerializedPropertyType.Vector4:
                    return element.vector4Value;
                case SerializedPropertyType.Vector2Int:
                    return element.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return element.vector3IntValue;
                case SerializedPropertyType.Enum:
                    return element.enumValueIndex;
                case SerializedPropertyType.Color:
                    return element.colorValue;
                case SerializedPropertyType.Rect:
                    return element.rectValue;
                case SerializedPropertyType.Bounds:
                    return element.boundsValue;
                default:
                    // For complex types, we'll copy the entire property
                    return null; // Will handle differently
            }
        }

        public static T GetAttribute<T>(SerializedProperty property) where T : class
        {
            T[] attributes = GetAttributes<T>(property);
            return (attributes.Length > 0) ? attributes[0] : null;
        }

        public static T[] GetAttributes<T>(SerializedProperty property) where T : class
        {
            FieldInfo fieldInfo = ReflectionUtility.GetField(GetTargetObjectWithProperty(property), property.name);
            if (fieldInfo == null)
            {
                return new T[] { };
            }

            return (T[])fieldInfo.GetCustomAttributes(typeof(T), true);
        }

        public static GUIContent GetLabel(SerializedProperty property)
        {
            LabelAttribute labelAttribute = GetAttribute<LabelAttribute>(property);
            string labelText = (labelAttribute == null)
                ? property.displayName
                : labelAttribute.Label;

            GUIContent label = new GUIContent(labelText);
            return label;
        }

        public static void CallOnValueChangedCallbacks(SerializedProperty property)
        {
            OnValueChangedAttribute[] onValueChangedAttributes = GetAttributes<OnValueChangedAttribute>(property);
            if (onValueChangedAttributes.Length == 0)
            {
                return;
            }

            object target = GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties(); // We must apply modifications so that the new value is updated in the serialized object

            foreach (var onValueChangedAttribute in onValueChangedAttributes)
            {
                MethodInfo callbackMethod = ReflectionUtility.GetMethod(target, onValueChangedAttribute.CallbackName);
                if (callbackMethod != null &&
                    callbackMethod.ReturnType == typeof(void) &&
                    callbackMethod.GetParameters().Length == 0)
                {
                    callbackMethod.Invoke(target, new object[] { });
                }
                else
                {
                    string warning = string.Format(
                        "{0} can invoke only methods with 'void' return type and 0 parameters",
                        onValueChangedAttribute.GetType().Name);

                    Debug.LogWarning(warning, property.serializedObject.targetObject);
                }
            }
        }

        public static void CallOnValidateCallbacks(SerializedProperty property)
        {
            OnValidateAttribute[] onValueChangedAttributes = GetAttributes<OnValidateAttribute>(property);
            if (onValueChangedAttributes.Length == 0)
            {
                return;
            }

            object target = GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties(); // We must apply modifications so that the new value is updated in the serialized object

            foreach (var onValueChangedAttribute in onValueChangedAttributes)
            {
                string warning = string.Format("{0} can invoke only methods with 'void' return type and 0 parameters",
                        onValueChangedAttribute.GetType().Name);

                MethodInfo callbackMethod = ReflectionUtility.GetMethod(target, onValueChangedAttribute.CallbackName);
                if (callbackMethod != null && callbackMethod.ReturnType == typeof(void))
                {
                    if (callbackMethod.GetParameters().Length == 0)
                    {
                        callbackMethod.Invoke(target, new object[] { });
                    }
                    else if (callbackMethod.GetParameters().Length == 1)
                    {
                        if (Parameter_Convert(property, out object parameter))
                            callbackMethod.Invoke(target, new object[] { parameter });
                    }
                    else
                    {
                        Debug.LogWarning(warning, property.serializedObject.targetObject);
                    }
                }
                else
                {
                    Debug.LogWarning(warning, property.serializedObject.targetObject);
                }
            }
        }

        public static bool Parameter_Convert(SerializedProperty property, out object value)
        {
            bool isConvert = true;

            try
            {
                var targetObject = property.serializedObject.targetObject;
                var targetObjectClassType = targetObject.GetType();
                var field = targetObjectClassType.GetField(property.propertyPath);
                if (field != null)
                {
                    value = field.GetValue(targetObject);
                }
                else
                {
                    value = null;
                    isConvert = false;
                }
            }
            catch
            {
                value = null;
                isConvert = false;
            }

            return isConvert;
        }

        public static bool IsEnabled(SerializedProperty property)
        {
            ReadOnlyAttribute readOnlyAttribute = GetAttribute<ReadOnlyAttribute>(property);
            if (readOnlyAttribute != null)
            {
                return false;
            }

            EnableIfAttributeBase enableIfAttribute = GetAttribute<EnableIfAttributeBase>(property);
            if (enableIfAttribute == null)
            {
                return true;
            }

            object target = GetTargetObjectWithProperty(property);

            // deal with enum conditions
            if (enableIfAttribute.EnumValue != null)
            {
                Enum value = GetEnumValue(target, enableIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? enableIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(enableIfAttribute.EnumValue);

                    return matched != enableIfAttribute.Inverted;
                }

                string message = enableIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }

            // deal with normal conditions
            List<bool> conditionValues = GetConditionValues(target, enableIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool enabled = GetConditionsFlag(conditionValues, enableIfAttribute.ConditionOperator, enableIfAttribute.Inverted);
                return enabled;
            }
            else
            {
                string message = enableIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }
        }

        public static bool IsVisible(SerializedProperty property)
        {
            ShowIfAttributeBase showIfAttribute = GetAttribute<ShowIfAttributeBase>(property);
            if (showIfAttribute == null)
            {
                return true;
            }

            object target = GetTargetObjectWithProperty(property);

            // deal with enum conditions
            if (showIfAttribute.EnumValue != null)
            {
                Enum value = GetEnumValue(target, showIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? showIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(showIfAttribute.EnumValue);

                    return matched != showIfAttribute.Inverted;
                }

                string message = showIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }

            // deal with normal conditions
            List<bool> conditionValues = GetConditionValues(target, showIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool enabled = GetConditionsFlag(conditionValues, showIfAttribute.ConditionOperator, showIfAttribute.Inverted);
                return enabled;
            }
            else
            {
                string message = showIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }
        }

        /// <summary>
        ///		Gets an enum value from reflection.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="enumName">Name of a field, property, or method that returns an enum.</param>
        /// <returns>Null if can't find an enum value.</returns>
        internal static Enum GetEnumValue(object target, string enumName)
        {
            FieldInfo enumField = ReflectionUtility.GetField(target, enumName);
            if (enumField != null && enumField.FieldType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumField.GetValue(target);
            }

            PropertyInfo enumProperty = ReflectionUtility.GetProperty(target, enumName);
            if (enumProperty != null && enumProperty.PropertyType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumProperty.GetValue(target);
            }

            MethodInfo enumMethod = ReflectionUtility.GetMethod(target, enumName);
            if (enumMethod != null && enumMethod.ReturnType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumMethod.Invoke(target, null);
            }

            return null;
        }

        internal static List<bool> GetConditionValues(object target, string[] conditions)
        {
            List<bool> conditionValues = new List<bool>();
            foreach (var condition in conditions)
            {
                FieldInfo conditionField = ReflectionUtility.GetField(target, condition);
                if (conditionField != null &&
                    conditionField.FieldType == typeof(bool))
                {
                    conditionValues.Add((bool)conditionField.GetValue(target));
                }

                PropertyInfo conditionProperty = ReflectionUtility.GetProperty(target, condition);
                if (conditionProperty != null &&
                    conditionProperty.PropertyType == typeof(bool))
                {
                    conditionValues.Add((bool)conditionProperty.GetValue(target));
                }

                MethodInfo conditionMethod = ReflectionUtility.GetMethod(target, condition);
                if (conditionMethod != null &&
                    conditionMethod.ReturnType == typeof(bool) &&
                    conditionMethod.GetParameters().Length == 0)
                {
                    conditionValues.Add((bool)conditionMethod.Invoke(target, null));
                }
            }

            return conditionValues;
        }

        internal static bool GetConditionsFlag(List<bool> conditionValues, EConditionOperator conditionOperator, bool invert)
        {
            bool flag;
            if (conditionOperator == EConditionOperator.And)
            {
                flag = true;
                foreach (var value in conditionValues)
                {
                    flag = flag && value;
                }
            }
            else
            {
                flag = false;
                foreach (var value in conditionValues)
                {
                    flag = flag || value;
                }
            }

            if (invert)
            {
                flag = !flag;
            }

            return flag;
        }

        public static Type GetPropertyType(SerializedProperty property)
        {
            object obj = GetTargetObjectOfProperty(property);
            Type objType = obj.GetType();

            return objType;
        }

        /// <summary>
        /// Gets the object the property represents.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static object GetTargetObjectOfProperty(SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            return obj;
        }

        /// <summary>
        /// Gets the object that the property is a member of
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static object GetTargetObjectWithProperty(SerializedProperty property)
        {
            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                string element = elements[i];
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();

            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(source);
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    return property.GetValue(source, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            IEnumerable enumerable = GetValue_Imp(source, name) as IEnumerable;
            if (enumerable == null)
            {
                return null;
            }

            IEnumerator enumerator = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enumerator.MoveNext())
                {
                    return null;
                }
            }

            return enumerator.Current;
        }
    }
}
