using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

namespace NaughtyAttributes.Editor
{
    [CustomPropertyDrawer(typeof(ProgressBarAttribute))]
    public class ProgressBarPropertyDrawer : PropertyDrawerBase
    {
        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            ProgressBarAttribute progressBarAttribute = PropertyUtility.GetAttribute<ProgressBarAttribute>(property);
            var maxValue = GetMaxValue(property, progressBarAttribute);

            return IsNumber(property) && IsNumber(maxValue)
                ? GetPropertyHeight(property)
                : GetPropertyHeight(property) + GetHelpBoxHeight();
        }

        protected override void OnGUI_Internal(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            if (!IsNumber(property))
            {
                string message = string.Format("Field {0} is not a number", property.name);
                DrawDefaultPropertyAndHelpBox(rect, property, message, MessageType.Warning);
                return;
            }

            ProgressBarAttribute progressBarAttribute = PropertyUtility.GetAttribute<ProgressBarAttribute>(property);
            var value = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
            var valueFormatted = property.propertyType == SerializedPropertyType.Integer ? value.ToString() : string.Format("{0:0.00}", value);
            var maxValue = GetMaxValue(property, progressBarAttribute);

            if (maxValue != null && IsNumber(maxValue))
            {
                var fillPercentage = value / CastToFloat(maxValue);
                var barLabel = (!string.IsNullOrEmpty(progressBarAttribute.Name) ? "[" + progressBarAttribute.Name + "] " : "") + valueFormatted + "/" + maxValue;
                var barColor = GetColorValue(property, progressBarAttribute);
                var labelColor = Color.white;

                var indentLength = NaughtyEditorGUI.GetIndentLength(rect);
                Rect barRect = new Rect()
                {
                    x = rect.x + indentLength,
                    y = rect.y,
                    width = rect.width - indentLength,
                    height = EditorGUIUtility.singleLineHeight
                };

                DrawBar(barRect, Mathf.Clamp01(fillPercentage), barLabel, CastToColor(barColor), labelColor);
            }
            else
            {
                string message = string.Format(
                    "The provided dynamic max value for the progress bar is not correct. Please check if the '{0}' is correct, or the return type is float/int",
                    nameof(progressBarAttribute.MaxValueName));

                DrawDefaultPropertyAndHelpBox(rect, property, message, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private object GetMaxValue(SerializedProperty property, ProgressBarAttribute progressBarAttribute)
        {
            if (string.IsNullOrEmpty(progressBarAttribute.MaxValueName))
            {
                return progressBarAttribute.MaxValue;
            }
            else
            {
                object target = PropertyUtility.GetTargetObjectWithProperty(property);

                FieldInfo valuesFieldInfo = ReflectionUtility.GetField(target, progressBarAttribute.MaxValueName);
                if (valuesFieldInfo != null)
                {
                    return valuesFieldInfo.GetValue(target);
                }

                PropertyInfo valuesPropertyInfo = ReflectionUtility.GetProperty(target, progressBarAttribute.MaxValueName);
                if (valuesPropertyInfo != null)
                {
                    return valuesPropertyInfo.GetValue(target);
                }

                MethodInfo methodValuesInfo = ReflectionUtility.GetMethod(target, progressBarAttribute.MaxValueName);
                if (methodValuesInfo != null &&
                    (methodValuesInfo.ReturnType == typeof(float) || methodValuesInfo.ReturnType == typeof(int)) &&
                    methodValuesInfo.GetParameters().Length == 0)
                {
                    return methodValuesInfo.Invoke(target, null);
                }

                return null;
            }
        }

        private object GetColorValue(SerializedProperty property, ProgressBarAttribute progressBarAttribute)
        {
            if (string.IsNullOrEmpty(progressBarAttribute.ColorValueName))
            {
                return progressBarAttribute.Color;
            }
            else
            {
                object target = PropertyUtility.GetTargetObjectWithProperty(property);

                MethodInfo colorMethod = ReflectionUtility.GetMethod(target, progressBarAttribute.ColorValueName);

                if (colorMethod != null &&
                    colorMethod.ReturnType == typeof(Color))
                {
                    ParameterInfo[] callbackParameters = colorMethod.GetParameters();
                    return GetColorHandle(property, progressBarAttribute, target, IsColor, colorMethod, callbackParameters);
                }
                else if (colorMethod != null &&
                         colorMethod.ReturnType == typeof(string))
                {
                    ParameterInfo[] callbackParameters = colorMethod.GetParameters();
                    return GetColorHandle(property, progressBarAttribute, target, IsString, colorMethod, callbackParameters);
                }
                else
                {
                    // Try to fallback to a field or property with the same name (support color fields/properties or string names)
                    FieldInfo field = ReflectionUtility.GetField(target, progressBarAttribute.ColorValueName);
                    if (field != null)
                    {
                        var value = field.GetValue(target);
                        if (value is Color || value is string)
                        {
                            return value;
                        }
                    }

                    PropertyInfo prop = ReflectionUtility.GetProperty(target, progressBarAttribute.ColorValueName);
                    if (prop != null)
                    {
                        var value = prop.GetValue(target);
                        if (value is Color || value is string)
                        {
                            return value;
                        }
                    }
                }

                // Fallback to the static color on the attribute if nothing valid was found
                return progressBarAttribute.Color;
            }
        }

        private static bool IsString(object obj)
        {
            return obj is string;
        }

        private static bool IsColor(object obj)
        {
            return obj is Color;
        }

        delegate bool ConditionDelegate(object obj);

        private static object GetColorHandle(SerializedProperty property, ProgressBarAttribute progressBarAttribute, object target, ConditionDelegate condition, MethodInfo colorMethod, ParameterInfo[] callbackParameters)
        {
            if (callbackParameters.Length == 0)
            {
                var result = colorMethod.Invoke(target, null);
                if (condition(result))
                {
                    return result;
                }
                else
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        progressBarAttribute.ColorValueName + " is not valid", MessageType.Error, context: property.serializedObject.targetObject);
                }
            }
            else if (callbackParameters.Length == 1)
            {
                FieldInfo fieldInfo = ReflectionUtility.GetField(target, property.name);
                Type fieldType = fieldInfo.FieldType;
                Type parameterType = callbackParameters[0].ParameterType;

                if (fieldType == parameterType)
                {
                    var result = colorMethod.Invoke(target, new object[] { fieldInfo.GetValue(target) });
                    if (condition(result))
                    {
                        return result;
                    }
                    else
                    {
                        NaughtyEditorGUI.HelpBox_Layout(
                            progressBarAttribute.ColorValueName + " is not valid", MessageType.Error, context: property.serializedObject.targetObject);
                    }
                }
                else
                {
                    string warning = "The field type is not the same as the callback's parameter type";
                    NaughtyEditorGUI.HelpBox_Layout(warning, MessageType.Warning, context: property.serializedObject.targetObject);
                }
            }
            else
            {
                string warning =
                    progressBarAttribute.GetType().Name +
                    " needs a callback with string return type and an optional single parameter of the same type as the field";

                NaughtyEditorGUI.HelpBox_Layout(warning, MessageType.Warning, context: property.serializedObject.targetObject);
            }

            return null;
        }


        private void DrawBar(Rect rect, float fillPercent, string label, Color barColor, Color labelColor)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var fillRect = new Rect(rect.x, rect.y, rect.width * fillPercent, rect.height);

            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
            EditorGUI.DrawRect(fillRect, barColor);

            // set alignment and cache the default
            var align = GUI.skin.label.alignment;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;

            // set the color and cache the default
            var c = GUI.contentColor;
            GUI.contentColor = labelColor;

            // calculate the position
            var labelRect = new Rect(rect.x, rect.y - 2, rect.width, rect.height);

            // draw~
            EditorGUI.DropShadowLabel(labelRect, label);

            // reset color and alignment
            GUI.contentColor = c;
            GUI.skin.label.alignment = align;
        }

        private bool IsNumber(SerializedProperty property)
        {
            bool isNumber = property.propertyType == SerializedPropertyType.Float || property.propertyType == SerializedPropertyType.Integer;
            return isNumber;
        }

        private bool IsNumber(object obj)
        {
            return (obj is float) || (obj is int);
        }

        private float CastToFloat(object obj)
        {
            if (obj is int)
            {
                return (int)obj;
            }
            else
            {
                return (float)obj;
            }
        }

        private Color CastToColor(object obj)
        {
            if (obj is Color color)
            {
                return color;
            }
            else if (obj is string colorString)
            {
                return NaughtyColorUtility.GetColor(colorString, Color.white);
            }

            return Color.white;
        }
    }
}
