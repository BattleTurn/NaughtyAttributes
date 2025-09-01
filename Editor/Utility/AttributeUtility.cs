using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class AttributeUtility
    {
        public static void FieldPropertyCallbackHandle(SerializedProperty property, string callbackName, Action<object> actionCallback, Action onUseOtherMethodCallback)
        {
            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            FieldInfo valuesFieldInfo = ReflectionUtility.GetField(target, callbackName);
            if (valuesFieldInfo != null)
            {
                var value = valuesFieldInfo.GetValue(target);
                actionCallback.Invoke(value);
                return;
            }

            PropertyInfo valuesPropertyInfo = ReflectionUtility.GetProperty(target, callbackName);
            if (valuesPropertyInfo != null)
            {
                var value = valuesPropertyInfo.GetValue(target);
                actionCallback.Invoke(value);
                return;
            }

            onUseOtherMethodCallback.Invoke();
        }

        public static void HandleAttributeCallback(object target, SerializedProperty property, MethodInfo callback, ParameterInfo[] callbackParameters, Action actionCallback, string errorMessage)
        {
            if (callbackParameters.Length == 0)
            {
                var result = callback.Invoke(target, null);
                if (result is not float && result is not int)
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        property.name + " is not valid", MessageType.Error, context: property.serializedObject.targetObject);
                }
                else
                {
                    actionCallback.Invoke();
                }
            }
            else if (callbackParameters.Length == 1)
            {
                FieldInfo fieldInfo = ReflectionUtility.GetField(target, property.name);
                Type fieldType = fieldInfo.FieldType;
                Type parameterType = callbackParameters[0].ParameterType;

                if (fieldType == parameterType)
                {
                    var result = callback.Invoke(target, new object[] { fieldInfo.GetValue(target) });
                    if (result is not float && result is not int)
                    {
                        NaughtyEditorGUI.HelpBox_Layout(
                            property.name + " is not valid", MessageType.Error, context: property.serializedObject.targetObject);
                    }
                    else
                    {
                        actionCallback.Invoke();
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
                string warning = errorMessage;
                NaughtyEditorGUI.HelpBox_Layout(warning, MessageType.Warning, context: property.serializedObject.targetObject);
            }
        }
    }
}