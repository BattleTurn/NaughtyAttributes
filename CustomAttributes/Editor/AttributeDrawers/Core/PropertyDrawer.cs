using System;

using UnityEditor;

using UnityEngine.UIElements;

using StylizeAttributes.Core;

namespace StylizeAttributes.Editor
{
    public abstract class PropertyDrawer<T> : PropertyDrawerBase, IAttributeBinder<T> where T : Attribute, IStylized
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

        public override void BindTargetAttribute(IStylized attribute)
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