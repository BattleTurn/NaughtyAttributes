using System;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using StylizeAttributes.Core;

namespace StylizeAttributes.Editor
{
    public class ReadOnlyDrawer : PropertyDrawer<ReadOnlyAttribute>
    {
        public override Type BindAttributeType => typeof(ReadOnlyAttribute);

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            base.CreatePropertyGUI(property, root);

            var propertyField = root.Q<PropertyField>("property");
            Debug.Log("CreatePropertyGUI for ReadOnly: " + property.name);

            if (propertyField == null)
            {
                propertyField = new PropertyField(property);
                root.Add(propertyField);
            }

            propertyField.BindProperty(property);
            propertyField.SetEnabled(false);
            return root;
        }
    }
}