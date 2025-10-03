using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace CustomAttributes.Editor
{
    public interface IUIPropertyDrawer
    {
        Type AttributeType { get; }
        VisualTreeAsset UXML { get; }
        StyleSheet USS { get; }

        void Setup(FieldInfo fieldInfo);

        VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root);
    }
}