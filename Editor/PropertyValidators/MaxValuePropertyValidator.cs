using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MaxValuePropertyValidator : PropertyValidatorBase
    {
        public override bool ValidateProperty(SerializedProperty property)
        {
            MaxValueAttribute a = PropertyUtility.GetAttribute<MaxValueAttribute>(property);
            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            return AttributeUtility.FieldPropertyCallbackHandle(
                property, a.MaxValueName,
                value => ValidatorUtility.ClampValue<MaxValueAttribute>(Convert.ToSingle(value), GreaterThan, property),
                () =>
                {
                    MethodInfo cb = ReflectionUtility.GetMethod(target, a.MaxValueName);

                    if (cb != null && (cb.ReturnType == typeof(int) || cb.ReturnType == typeof(float)))
                    {
                        string warn;
                        bool changed = AttributeUtility.HandleAttributeCallback(
                            target, property, cb, cb.GetParameters(),
                            invokeAndClamp: () =>
                            {
                                var v = cb.Invoke(target, null);
                                return ValidatorUtility.ClampValue<MaxValueAttribute>(Convert.ToSingle(v), GreaterThan, property);
                            },
                            out warn);

                        if (!string.IsNullOrEmpty(warn))
                            NaughtyEditorGUI.HelpBox_Layout(warn, MessageType.Warning, context: property.serializedObject.targetObject);

                        return changed;
                    }
                    else
                    {
                        return UseMaxValueHandle(a, property);
                    }
                });
        }

        private bool UseMaxValueHandle(MaxValueAttribute a, SerializedProperty p)
        {
            // Direct literal max
            return ValidatorUtility.ClampValue<MaxValueAttribute>(a.MaxValue, GreaterThan, p);
        }

        private bool GreaterThan(float a, float b) => a > b;

    }
}
