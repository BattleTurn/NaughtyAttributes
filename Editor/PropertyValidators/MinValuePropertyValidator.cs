using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MinValuePropertyValidator : PropertyValidatorBase
    {
        public override void ValidateProperty(SerializedProperty property)
        {
            MinValueAttribute minValueAttribute = PropertyUtility.GetAttribute<MinValueAttribute>(property);

            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            AttributeUtility.FieldPropertyCallbackHandle(property, minValueAttribute.MinValueName, (value) =>
            {
                ValidatorUtility.ClampValue(float.Parse(value.ToString()), LesserThan, property);
            },
            () =>
            {
                MethodInfo minValueCallback = ReflectionUtility.GetMethod(target, minValueAttribute.MinValueName);

                if (minValueCallback != null &&
                    (minValueCallback.ReturnType == typeof(int) || minValueCallback.ReturnType == typeof(float)))
                {
                    UseCallbackHandle(minValueAttribute, property, minValueCallback, target);

                }
                else
                {
                    UseMinValueHandle(minValueAttribute, property);
                }
            });
        }

        private void UseCallbackHandle(MinValueAttribute minValueAttribute, SerializedProperty property, MethodInfo minValueCallback, object target)
        {
            ParameterInfo[] callbackParameters = minValueCallback.GetParameters();

            Action actionCallback = () =>
            {
                var value = minValueCallback.Invoke(target, null);
                ValidatorUtility.ClampValue(float.Parse(value.ToString()), LesserThan, property);
            };

            string warning = $"\"{minValueAttribute.GetType().Name}\" needs a callback with float or int return type and an optional single parameter of the same type as the field";

            AttributeUtility.HandleAttributeCallback(target, property, minValueCallback, callbackParameters, actionCallback, warning);
        }

        private void UseMinValueHandle(MinValueAttribute minValueAttribute, SerializedProperty property)
        {
            ValidatorUtility.ClampValue(minValueAttribute.MinValue, LesserThan, property);
        }

        private bool LesserThan(float a, float b)
        {
            return a < b;
        }

    }
}
