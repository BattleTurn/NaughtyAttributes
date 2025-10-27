using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace StylizeAttributes.Editor
{
    public class EnumDrawer : AutoDrawer<Enum>
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
                return new PropertyField(property);

            var names = new List<string>(property.enumDisplayNames);
            var popup = new PopupField<string>(names, property.enumValueIndex);
            popup.RegisterValueChangedCallback(evt =>
            {
                property.enumValueIndex = popup.index;
                property.serializedObject.ApplyModifiedProperties();
            });

            root.Add(popup);
            return root;
        }
    }
}
