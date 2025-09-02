using System;
using System.Reflection;
using UnityEditor;

namespace NaughtyAttributes.Editor
{

    public static class AttributeUtility
    {
        // Return true if actionCallback actually modified the property
        public static bool FieldPropertyCallbackHandle(
            SerializedProperty property,
            string callbackName,
            Func<object, bool> actionCallbackOrReturnChanged,
            Func<bool> onUseOtherMethodCallback)
        {
            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            FieldInfo fi = ReflectionUtility.GetField(target, callbackName);
            if (fi != null)
            {
                var value = fi.GetValue(target);
                return actionCallbackOrReturnChanged.Invoke(value);
            }

            PropertyInfo pi = ReflectionUtility.GetProperty(target, callbackName);
            if (pi != null)
            {
                var value = pi.GetValue(target);
                return actionCallbackOrReturnChanged.Invoke(value);
            }

            return onUseOtherMethodCallback.Invoke();
        }

        // Return true if callback path modified the property; NO UI here
        public static bool HandleAttributeCallback(
            object target,
            SerializedProperty property,
            MethodInfo callback,
            ParameterInfo[] callbackParameters,
            Func<bool> invokeAndClamp,
            out string warning)
        {
            warning = null;

            if (callbackParameters.Length == 0)
            {
                // result is numeric
                var result = callback.Invoke(target, null);
                if (result is float or int)
                    return invokeAndClamp();

                warning = $"{property.name} is not valid";
                return false;
            }
            else if (callbackParameters.Length == 1)
            {
                FieldInfo fieldInfo = ReflectionUtility.GetField(target, property.name);
                Type fieldType = fieldInfo?.FieldType;
                Type parameterType = callbackParameters[0].ParameterType;

                if (fieldType == parameterType)
                {
                    var result = callback.Invoke(target, new object[] { fieldInfo.GetValue(target) });
                    if (result is float or int)
                        return invokeAndClamp();

                    warning = $"{property.name} is not valid";
                    return false;
                }
                else
                {
                    warning = "The field type is not the same as the callback's parameter type";
                    return false;
                }
            }
            else
            {
                warning = $"{callback.Name} needs a callback with float or int return type and an optional single parameter of the same type as the field";
                return false;
            }
        }
    }
}