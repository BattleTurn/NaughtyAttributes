using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class RequiredPropertyValidator : PropertyValidatorBase
    {
        private static readonly Dictionary<(int id, string path), bool> _lastValidity = new();

        public override bool ValidateProperty(SerializedProperty property)
        {
            RequiredAttribute requiredAttribute = PropertyUtility.GetAttribute<RequiredAttribute>(property);
            
            // Only reference types are supported
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                string warning = requiredAttribute.GetType().Name + " works only on reference types";
                NaughtyEditorGUI.HelpBox_Layout(warning, MessageType.Warning, context: property.serializedObject.targetObject);
                // no value modification
                return false;
            }

            bool isValid = property.objectReferenceValue != null;

            // Pick message
            string errorMessage = !string.IsNullOrEmpty(requiredAttribute.Message)
                ? requiredAttribute.Message
                : property.name + " is required";

            if (!isValid)
            {
                NaughtyEditorGUI.HelpBox_Layout(errorMessage, MessageType.Error, context: property.serializedObject.targetObject);
            }

            // --- Repaint trigger when validity flips (no value modification) ---
            var target = property.serializedObject.targetObject;
            if (target != null)
            {
                var key = (target.GetInstanceID(), property.propertyPath);
                if (!_lastValidity.TryGetValue(key, out bool last) || last != isValid)
                {
                    _lastValidity[key] = isValid;
                    // Tell IMGUI “something changed” so the Inspector refreshes immediately
                    GUI.changed = true;
                }
            }

            // Validator did NOT change any values
            return false;
        }
    }
}
