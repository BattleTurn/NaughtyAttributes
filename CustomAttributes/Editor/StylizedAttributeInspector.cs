using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

using StylizeAttributes.Core;

namespace StylizeAttributes.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class StylizedAttributeInspector : UnityEditor.Editor
    {
        private Dictionary<Type, PropertyDrawerBase> _drawers = new();

        private IEnumerable<MethodInfo> _methods;
        private List<SerializedProperty> _serializedProperties = new List<SerializedProperty>();

        protected virtual void OnEnable()
        {
            _methods = ReflectionUtility.GetAllMethods(
                target, m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0);
            GetDrawers();
        }

        private void GetDrawers()
        {
            var drawerTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => a.GetTypes())
                            .Where(t => typeof(PropertyDrawerBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in drawerTypes)
            {
                var instance = (PropertyDrawerBase)Activator.CreateInstance(type);
                Debug.Log($"Registering drawer for attribute: {instance.BindAttributeType.Name}");
                _drawers[instance.BindAttributeType] = instance;
            }
        }


        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            SerializedProperty iterator = GetSerializedProperties(_serializedProperties);
            DrawSerializedFields(root);
            DrawButtons(iterator, root);
            return root;
        }

        protected SerializedProperty GetSerializedProperties(List<SerializedProperty> serializedProperties)
        {
            serializedProperties.Clear();
            var iterator = serializedObject.GetIterator();

            if (iterator.NextVisible(true))
            {
                do
                {
                    serializedProperties.Add(serializedObject.FindProperty(iterator.name));
                }
                while (iterator.NextVisible(false));
            }
            return iterator;
        }

        private void DrawSerializedFields(VisualElement root)
        {
            foreach (var prop in _serializedProperties)
            {
                if (prop == null) continue;
                FieldInfo fieldInfo = prop.GetField();

                if (fieldInfo != null)
                {
                    var attr = fieldInfo.GetCustomAttributes(typeof(IStylizeAttribute), true).FirstOrDefault() as IStylizeAttribute;
                    if (attr != null && _drawers.TryGetValue(attr.GetType(), out var drawer))
                    {

                        var element = new VisualElement();
                        drawer.BindTargetAttribute(attr);
                        element = drawer.CreatePropertyGUI(prop, element);

                        root.Add(element);
                        continue;
                    }
                }

                var defaultField = new PropertyField(prop);
                root.Add(defaultField);
            }
        }


        private void DrawButtons(SerializedProperty iterator, VisualElement root)
        {
            if (_methods == null)
            {
                return;
            }

            foreach (var method in _methods)
            {
                var attr = method.GetCustomAttributes(typeof(IStylizeAttribute), true).FirstOrDefault() as IStylizeAttribute;
                if (attr != null && _drawers.TryGetValue(attr.GetType(), out var drawer))
                {

                    var element = new VisualElement();
                    drawer.BindTargetAttribute(attr);
                    element = drawer.CreatePropertyGUI(iterator.Copy(), element);

                    root.Add(element);
                    continue;
                }
            }
        }

    }
}
