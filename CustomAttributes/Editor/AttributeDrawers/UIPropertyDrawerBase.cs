using System;

using UnityEditor;
using UnityEngine.UIElements;

using CustomAttributes.Core;
namespace CustomAttributes.Editor
{
    public abstract class UIPropertyDrawerBase : IAttributeTypeBinder, IUIPropertyDrawer
    {
        protected string styleName = "Default";

        public string StyleName => styleName;
        public VisualTreeAsset UXML
        {
            get
            {
                Type attributeType = BindAttributeType;

                UnityEngine.Debug.Log($"Retrieving UXML for EnumValue: {styleName} and AttributeType: {attributeType}");
                if (UIStyleConfig.Instance[attributeType, styleName] == null)
                {
                    UnityEngine.Debug.LogWarning($"UXML for {styleName} is missing in UIStyle");
                    return null;
                }
                return UIStyleConfig.Instance[attributeType, styleName].uxml;
            }
        }
        public StyleSheet USS
        {
            get
            {
                Type attributeType = BindAttributeType;

                UnityEngine.Debug.Log($"Retrieving USS for EnumValue: {styleName} and AttributeType: {attributeType}");
                if (UIStyleConfig.Instance[attributeType, styleName] == null)
                {
                    UnityEngine.Debug.LogWarning($"USS for {styleName} is missing in UIStyle");
                    return null;
                }
                return UIStyleConfig.Instance[attributeType, styleName].uss;
            }
        }

        public abstract Type BindAttributeType { get; }

        public UIPropertyDrawerBase(string styleName)
        {
            this.styleName = styleName;
        }

        public UIPropertyDrawerBase() : this("Default")
        {
        }

        public abstract VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root);

        protected VisualTreeAsset LoadUXML(VisualElement root)
        {
            UnityEngine.Debug.Log($"Loading UXML for {BindAttributeType}, UXML: {UXML}");
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