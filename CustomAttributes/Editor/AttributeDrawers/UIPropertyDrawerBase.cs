using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using CustomAttributes.Runtime;

namespace CustomAttributes.Editor
{
    public abstract class UIPropertyDrawerBase<T> : IUIPropertyDrawer where T : Attribute
    {
        public Type AttributeType => typeof(T);
        public T TargetAttribute { get; protected set; }
        public string EnumValue { get; }

        public VisualTreeAsset UXML
        {
            get
            {
                if (UIStyleConfig.Instance[AttributeType, EnumValue.ToString()] == null)
                {
                    UnityEngine.Debug.LogWarning($"UXML for {EnumValue} is missing in UIStyle");
                    return null;
                }
                return UIStyleConfig.Instance[AttributeType, EnumValue.ToString()].uxml;
            }
        }
        public StyleSheet USS
        {
            get
            {
                if (UIStyleConfig.Instance[AttributeType, EnumValue.ToString()] == null)
                {
                    UnityEngine.Debug.LogWarning($"USS for {EnumValue} is missing in UIStyle");
                    return null;
                }
                return UIStyleConfig.Instance[AttributeType, EnumValue.ToString()].uss;
            }
        }

        public UIPropertyDrawerBase(string enumValue)
        {
            EnumValue = enumValue;
        }

        public UIPropertyDrawerBase() : this("Default")
        {
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
}