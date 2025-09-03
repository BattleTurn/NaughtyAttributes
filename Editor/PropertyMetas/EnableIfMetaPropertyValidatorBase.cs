using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class EnableIfMetaPropertyValidatorBase : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            ReadOnlyAttribute readOnlyAttribute = PropertyUtility.GetAttribute<ReadOnlyAttribute>(property);
            if (readOnlyAttribute != null)
            {
                return false;
            }

            EnableIfAttributeBase enableIfAttribute = PropertyUtility.GetAttribute<EnableIfAttributeBase>(property);
            if (enableIfAttribute == null)
            {
                return true;
            }

            object target = PropertyUtility.GetTargetObjectWithProperty(property);

            // deal with enum conditions
            if (enableIfAttribute.EnumValue != null)
            {
                Enum value = PropertyUtility.GetEnumValue(target, enableIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? enableIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(enableIfAttribute.EnumValue);

                    return matched != enableIfAttribute.Inverted;
                }

                string message = enableIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }

            // deal with normal conditions
            List<bool> conditionValues = PropertyUtility.GetConditionValues(target, enableIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool enabled = PropertyUtility.GetConditionsFlag(conditionValues, enableIfAttribute.ConditionOperator, enableIfAttribute.Inverted);
                return enabled;
            }
            else
            {
                string message = enableIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }
        }
    }
}
