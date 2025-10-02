using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

public abstract class UIPropertyDrawerBase<T> : IUIPropertyDrawer where T : Enum
{
    public abstract Type TargetAttribute { get; }
    public T EnumValue { get; }
    public VisualTreeAsset UXML
    {
        get
        {
            if (UIStyleConfig.Instance[EnumValue] == null)
            {
                UnityEngine.Debug.LogWarning($"UXML for {EnumValue} is missing in UIStyle");
                return null;
            }
            return UIStyleConfig.Instance[EnumValue].uxml;
        }
    }
    public StyleSheet USS
    {
        get
        {
            if (UIStyleConfig.Instance[EnumValue] == null)
            {
                UnityEngine.Debug.LogWarning($"USS for {EnumValue} is missing in UIStyle");
                return null;
            }
            return UIStyleConfig.Instance[EnumValue].uss;
        }
    }

    public UIPropertyDrawerBase(T enumValue)
    {
        EnumValue = enumValue;
    }

    public abstract void Setup(FieldInfo fieldInfo);

    public abstract VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root);

    protected VisualTreeAsset LoadUXML(VisualElement root)
    {
        if (UXML != null)
        {
                UXML.CloneTree(root);
                return UXML;
        }
        else
            UnityEngine.Debug.LogWarning($"UXML is missing");

        return null;
    }

    protected StyleSheet LoadUSS(VisualElement root)
    {
        if (USS != null)
        {
            root.styleSheets.Add(USS);
            return USS;
        }
        else
            UnityEngine.Debug.LogWarning($"USS is missing");

        return null;
    }
}