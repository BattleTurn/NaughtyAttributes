using System;
using UnityEditor;

using StylizeAttributes.Core;
using UnityEngine.UIElements;

namespace StylizeAttributes.Editor
{
    public abstract class PropertyDrawer<T> : PropertyDrawerBase, IAttributeBinder<T> where T : Attribute, IStylizeAttribute
    {
        protected T targetAttribute;

        public T TargetAttribute { get => targetAttribute; }

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            styleName = targetAttribute.StyleName;

            LoadUXML(root);
            LoadUSS(root);
            return root;
        }

        public override void BindTargetAttribute(IStylizeAttribute attribute)
        {
            if (attribute is T attr)
            {
                targetAttribute = attr;
            }
            else
            {
                throw new ArgumentException($"Invalid attribute type. Expected {typeof(T)}, but got {attribute.GetType()}");
            }
        }
    }
}