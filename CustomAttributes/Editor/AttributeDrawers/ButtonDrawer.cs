using System;
using System.Reflection;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;

using StylizeAttributes.Core;

namespace StylizeAttributes.Editor
{
    public class ButtonDrawer : PropertyDrawer<ButtonAttribute>
    {
        public override Type BindType => typeof(ButtonAttribute);

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            base.CreatePropertyGUI(property, root);

            var button = root.Q<Button>("button");

            Debug.Log("CreatePropertyGUI for Button: " + button.name);
            if (button != null)
            {
                button.text = targetAttribute != null ? targetAttribute.MethodName : "Unnamed Button";

                button.clicked += () =>
                {
                    Debug.Log($"Button '{button.text}' clicked!");
                    if (targetAttribute != null)
                    {
                        var method = property.serializedObject.targetObject
                            .GetType()
                            .GetMethod(targetAttribute.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (method != null)
                            method.Invoke(property.serializedObject.targetObject, null);
                        else
                            Debug.LogWarning($"Method '{targetAttribute.MethodName}' not found on {property.serializedObject.targetObject}");
                    }
                };
            }

            return root;
        }
    }
}