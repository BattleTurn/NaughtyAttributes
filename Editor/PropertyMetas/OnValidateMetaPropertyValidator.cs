using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class OnValidateMetaPropertyValidator : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            OnValidateAttribute[] onValidateAttributes = PropertyUtility.GetAttributes<OnValidateAttribute>(property);
            if (onValidateAttributes.Length == 0)
            {
                return false;
            }

            object target = PropertyUtility.GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties(); // We must apply modifications so that the new value is updated in the serialized object

            foreach (var onValidateAttribute in onValidateAttributes)
            {
                string warning = string.Format("{0} can invoke only methods with 'void' return type and 0 parameters",
                        onValidateAttribute.GetType().Name);

                MethodInfo callbackMethod = ReflectionUtility.GetMethod(target, onValidateAttribute.CallbackName);
                if (callbackMethod != null && callbackMethod.ReturnType == typeof(void))
                {
                    if (callbackMethod.GetParameters().Length == 0)
                    {
                        callbackMethod.Invoke(target, new object[] { });
                    }
                    else if (callbackMethod.GetParameters().Length == 1)
                    {
                        if (PropertyUtility.Parameter_Convert(property, out object parameter))
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
            return true;
        }
    }
}
