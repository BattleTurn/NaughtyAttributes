using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class MaxValuePropertyValidator : PropertyValidatorBase
    {
        public override void ValidateProperty(SerializedProperty property)
        {
            MaxValueAttribute maxValueAttribute = PropertyUtility.GetAttribute<MaxValueAttribute>(property);

            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            AttributeUtility.FieldPropertyCallbackHandle(property, maxValueAttribute.MaxValueName, (value) =>
            {
                ValidatorUtility.ClampValue(float.Parse(value.ToString()), GreaterThan, property);
            },
            () =>
            {
                MethodInfo maxValueCallback = ReflectionUtility.GetMethod(target, maxValueAttribute.MaxValueName);

                if (maxValueCallback != null &&
                    (maxValueCallback.ReturnType == typeof(int) || maxValueCallback.ReturnType == typeof(float)))
                {
                    UseCallbackHandle(maxValueAttribute, property, maxValueCallback, target);

                }
                else
                {
                    UseMaxValueHandle(maxValueAttribute, property);
                }
            });
        }

        private void UseCallbackHandle(MaxValueAttribute maxValueAttribute, SerializedProperty property, MethodInfo maxValueCallback, object target)
        {
            ParameterInfo[] callbackParameters = maxValueCallback.GetParameters();

            Action actionCallback = () =>
            {
                var value = maxValueCallback.Invoke(target, null);
                ValidatorUtility.ClampValue(float.Parse(value.ToString()), GreaterThan, property);
            };

            string warning = $"\"{maxValueAttribute.GetType().Name}\" needs a callback with float or int return type and an optional single parameter of the same type as the field";

            AttributeUtility.HandleAttributeCallback(target, property, maxValueCallback, callbackParameters, actionCallback, warning);
        }


        private void UseMaxValueHandle(MaxValueAttribute maxValueAttribute, SerializedProperty property)
        {
            ValidatorUtility.ClampValue(maxValueAttribute.MaxValue, GreaterThan, property);
        }

        private bool GreaterThan(float a, float b)
        {
            return a > b;
        }
    }
}
