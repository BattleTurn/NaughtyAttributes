using System;
using System.Reflection;
using UnityEditor;

using CustomAttributes.Core;
using UnityEngine.UIElements;

namespace CustomAttributes.Editor
{
    public abstract class UIPropertyDrawer<T> : UIPropertyDrawerBase, IAttributeBinder<T> where T : Attribute, IStylizeAttribute
    {
        protected T targetAttribute;

        public T TargetAttribute { get => targetAttribute; }

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            // Retrieve and log all attributes of the SerializedProperty
            if (targetAttribute == null)
                targetAttribute = GetAttribute(property);
            styleName = targetAttribute.StyleName;

            LoadUXML(root);
            LoadUSS(root);
            return root;
        }

        protected T GetAttribute(SerializedProperty property)
        {
            var fieldInfo = property.serializedObject.targetObject.GetType()
                            .GetField(property.propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo != null)
            {
                var attributes = fieldInfo.GetCustomAttributes(true);
                foreach (var attribute in attributes)
                {
                    if (attribute is T)
                    {
                        UnityEngine.Debug.Log($"Attribute found: {attribute.GetType().Name}");
                        return attribute as T;
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"FieldInfo not found for property path: {property.propertyPath}");
            }
            return null;
        }
    }
}