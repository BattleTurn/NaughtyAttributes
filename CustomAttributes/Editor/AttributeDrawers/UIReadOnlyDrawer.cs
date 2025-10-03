using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using CustomAttributes.Runtime;

namespace CustomAttributes.Editor
{
    public class UIReadOnlyDrawer : UIPropertyDrawerBase<UIReadOnlyAttribute>
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            LoadUXML(root);
            LoadUSS(root);

            var propertyField = root.Q<PropertyField>("property");
            if (propertyField != null)
            {
                UnityEngine.Debug.Log("CreatePropertyGUI for ReadOnly: " + property.name);
                propertyField.BindProperty(property);
                propertyField.SetEnabled(false);
            }

            return root;
        }

        public override void Setup(FieldInfo fieldInfo)
        {
        }
    }
}