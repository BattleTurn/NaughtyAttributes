using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class ShowIfMetaPropertyValidatorBase : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            ShowIfAttributeBase showIfAttribute = PropertyUtility.GetAttribute<ShowIfAttributeBase>(property);
            if (showIfAttribute == null)
            {
                return true; // visible by default
            }

            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            // enum condition path
            if (showIfAttribute.EnumValue != null)
            {
                Enum value = PropertyUtility.GetEnumValue(target, showIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? showIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(showIfAttribute.EnumValue);
                    return matched != showIfAttribute.Inverted;
                }

                string message = showIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);
                return false;
            }

            // boolean conditions path
            List<bool> conditionValues = PropertyUtility.GetConditionValues(target, showIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool visible = PropertyUtility.GetConditionsFlag(conditionValues, showIfAttribute.ConditionOperator, showIfAttribute.Inverted);
                return visible;
            }
            else
            {
                string message = showIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);
                return false;
            }
        }
    }
}