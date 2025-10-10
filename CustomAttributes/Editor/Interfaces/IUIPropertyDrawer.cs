using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace StylizeAttributes.Editor
{
    public interface IUIPropertyDrawer
    {
        VisualTreeAsset UXML { get; }
        StyleSheet USS { get; }
        public string StyleName { get; }

        VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root);
    }
}