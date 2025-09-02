using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MinValuePropertyValidator : PropertyValidatorBase
    {
        public override bool ValidateProperty(SerializedProperty property)
        {
            MinValueAttribute a = PropertyUtility.GetAttribute<MinValueAttribute>(property);
            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            return AttributeUtility.FieldPropertyCallbackHandle(
                property, a.MinValueName,
                value => ValidatorUtility.ClampValue<MinValueAttribute>(Convert.ToSingle(value), LesserThan, property),
                () =>
                {
                    MethodInfo cb = ReflectionUtility.GetMethod(target, a.MinValueName);

                    if (cb != null && (cb.ReturnType == typeof(int) || cb.ReturnType == typeof(float)))
                    {
                        string warn;
                        bool changed = AttributeUtility.HandleAttributeCallback(
                            target, property, cb, cb.GetParameters(),
                            invokeAndClamp: () =>
                            {
                                var v = cb.Invoke(target, null);
                                return ValidatorUtility.ClampValue<MinValueAttribute>(Convert.ToSingle(v), LesserThan, property);
                            },
                            out warn);

                        if (!string.IsNullOrEmpty(warn))
                            NaughtyEditorGUI.HelpBox_Layout(warn, MessageType.Warning, context: property.serializedObject.targetObject);

                        return changed;
                    }
                    else
                    {
                        return UseMinValueHandle(a, property);
                    }
                });
        }

        private bool UseMinValueHandle(MinValueAttribute a, SerializedProperty p)
        {
            // Direct literal min
            return ValidatorUtility.ClampValue<MinValueAttribute>(a.MinValue, LesserThan, p);
        }

        private bool LesserThan(float a, float b) => a < b;


    }
}
