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
            float currentValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
            object maxObj = GetMaxValue(property, progressBarAttribute);

            if (maxObj == null || !IsNumber(maxObj))
            {
                string message = string.Format(
                    "The provided dynamic max value for the progress bar is not correct. Please check if the '{0}' is correct, or the return type is float/int",
                    nameof(progressBarAttribute.MaxValueName));

                DrawDefaultPropertyAndHelpBox(rect, property, message, MessageType.Warning);
                return;
            }

            float maxValue = CastToFloat(maxObj);
            if (maxValue <= 0f) maxValue = 0.0001f; // avoid division by zero, still allows interaction

            // resolve colors
            Color barColor = CastToColor(GetColorValue(property, progressBarAttribute));
            Color labelColor = Color.white;

            float indentLength = NaughtyEditorGUI.GetIndentLength(rect);
            Rect barRect = new Rect(rect.x + indentLength, rect.y, rect.width - indentLength, EditorGUIUtility.singleLineHeight);

            // --- handle user input (click & drag) to set value ---
            float newValue = HandleProgressBarInput(barRect, currentValue, maxValue, property.propertyType == SerializedPropertyType.Integer);

            // write back if changed
            if (!Mathf.Approximately(newValue, currentValue))
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                    property.intValue = Mathf.RoundToInt(newValue);
                else
                    property.floatValue = newValue;

                // Commit now so label text reflects latest value during the same frame
                property.serializedObject.ApplyModifiedProperties();
                currentValue = newValue;
            }

            // draw the bar
            float fill = Mathf.Clamp01(currentValue / maxValue);
            string namePrefix = string.IsNullOrEmpty(progressBarAttribute.Name) ? "" : $"[{progressBarAttribute.Name}] ";
            string valueFormatted = property.propertyType == SerializedPropertyType.Integer
                ? currentValue.ToString()
                : string.Format("{0:0.00}", currentValue);
            string barLabel = $"{namePrefix}{valueFormatted}/{maxValue}";

            DrawBar(barRect, fill, barLabel, barColor, labelColor);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Handles mouse input (click & drag) over the bar to set a value in range [0..max].
        /// Returns the possibly-updated value (int rounding optional).
        /// </summary>
        private float HandleProgressBarInput(Rect barRect, float current, float max, bool roundToInt)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;

            // Cursor hint
            EditorGUIUtility.AddCursorRect(barRect, MouseCursor.SlideArrow);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (barRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        GUIUtility.keyboardControl = 0;
                        e.Use();

                        float v = PixelToValue(e.mousePosition.x, barRect, max, roundToInt);
                        return v;
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        float v = PixelToValue(e.mousePosition.x, barRect, max, roundToInt);
                        GUI.changed = true;
                        e.Use();
                        return v;
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                        // final update on release
                        float v = PixelToValue(e.mousePosition.x, barRect, max, roundToInt);
                        return v;
                    }
                    break;

                case EventType.Repaint:
                    // nothing to do, drawing handled elsewhere
                    break;
            }

            return current;

            // local helper
            float PixelToValue(float mouseX, Rect r, float m, bool round)
            {
                float t = Mathf.InverseLerp(r.xMin, r.xMax, mouseX);
                float v = Mathf.Clamp01(t) * m;
                if (round) v = Mathf.Round(v);
                return v;
            }
        }

        /// <summary>
        /// Draws the progress bar with background, fill, and centered shadow label.
        /// </summary>
        private void DrawBar(Rect rect, float fillPercent, string label, Color barColor, Color labelColor)
        {
            if (Event.current.type != EventType.Repaint)
            {
                // Ensure background is painted even when layout events sequence changes
                EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
                var fillRectEarly = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fillPercent), rect.height);
                EditorGUI.DrawRect(fillRectEarly, barColor);
                return;
            }

            var fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fillPercent), rect.height);

            // background
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
            // fill
            EditorGUI.DrawRect(fillRect, barColor);

            // centered label
            var align = GUI.skin.label.alignment;
            var c = GUI.contentColor;

            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.contentColor = labelColor;

            var labelRect = new Rect(rect.x, rect.y - 2, rect.width, rect.height);
            EditorGUI.DropShadowLabel(labelRect, label);

            GUI.contentColor = c;
            GUI.skin.label.alignment = align;
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

        private bool IsString(object obj)
        {
            return obj is string;
        }

        private bool IsColor(object obj)
        {
            return obj is Color;
        }

        delegate bool ConditionDelegate(object obj);

        private object GetColorHandle(SerializedProperty property, ProgressBarAttribute progressBarAttribute, object target, ConditionDelegate condition, MethodInfo colorMethod, ParameterInfo[] callbackParameters)
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
                    " needs a callback with string or Color return type and an optional single parameter of the same type as the field";

                NaughtyEditorGUI.HelpBox_Layout(warning, MessageType.Warning, context: property.serializedObject.targetObject);
            }

            return null;
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
            return NaughtyColorUtility.TryParse(obj, out var c) ? c : Color.white;
        }
    }
}
