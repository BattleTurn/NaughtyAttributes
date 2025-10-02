using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

public interface IUIPropertyDrawer
{
    Type TargetAttribute { get; }
    VisualTreeAsset UXML { get; }
    StyleSheet USS { get; }

    void Setup(FieldInfo fieldInfo);

    VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root);
}
