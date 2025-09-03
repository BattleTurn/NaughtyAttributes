using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class OnValueChangedMetaPropertyValidator : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            OnValueChangedAttribute[] onValueChangedAttributes = PropertyUtility.GetAttributes<OnValueChangedAttribute>(property);
            if (onValueChangedAttributes.Length == 0)
            {
                return false;
            }

            object target = PropertyUtility.GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties(); // We must apply modifications so that the new value is updated in the serialized object

            bool isChanged = true;
            foreach (var onValueChangedAttribute in onValueChangedAttributes)
            {
                MethodInfo callbackMethod = ReflectionUtility.GetMethod(target, onValueChangedAttribute.CallbackName);
                if (callbackMethod != null &&
                    callbackMethod.ReturnType == typeof(void) &&
                    callbackMethod.GetParameters().Length == 0)
                {
                    //TODO: Get the propertyValue changed, if changed set isChanged = true
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
            return isChanged;
        }
    }
}
