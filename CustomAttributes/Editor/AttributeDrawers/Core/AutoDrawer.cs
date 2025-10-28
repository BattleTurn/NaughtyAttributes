using System;

using UnityEditor;

using UnityEngine.UIElements;

using StylizeAttributes.Core;

namespace StylizeAttributes.Editor
{
    public abstract class AutoDrawer<T> : AutoDrawerBase, ITargetBinder<T> 
    {
        protected T targetObject;

        public override Type BindType => typeof(T);

        public T TargetObject { get => targetObject; }

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            LoadUXML(root);
            LoadUSS(root);
            return root;
        }

        public override void BindTargetAttribute(IStylized attribute)
        {
            if (attribute is T attr)
            {
                targetObject = attr;
            }
            else
            {
                throw new ArgumentException($"Invalid attribute type. Expected {typeof(T)}, but got {attribute.GetType()}");
            }
        }
    }
}