using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using CustomAttributes.Core;

namespace CustomAttributes.Editor
{
    public class UIReadOnlyDrawer : UIPropertyDrawer<UIReadOnlyAttribute>
    {
        public override Type BindAttributeType => typeof(UIReadOnlyAttribute);

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            base.CreatePropertyGUI(property, root);

            var propertyField = root.Q<PropertyField>("property");
            if (propertyField != null)
            {
                UnityEngine.Debug.Log("CreatePropertyGUI for ReadOnly: " + property.name);
                propertyField.BindProperty(property);
                propertyField.SetEnabled(false);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(property, true);
                EditorGUI.EndDisabledGroup();
            }

            return root;
        }
    }
}