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
    public partial class StylizedAttributeInspector : UnityEditor.Editor
    {
        protected readonly Dictionary<Type, PropertyDrawerBase> _stylizedDrawers = new();
        protected readonly Dictionary<Type, AutoDrawerBase> _autoDrawers = new();

        private IEnumerable<MethodInfo> _methods;
        private List<SerializedProperty> _serializedProperties = new List<SerializedProperty>();

        protected virtual void OnEnable()
        {
            _methods = ReflectionUtility.GetAllMethods(
                target, m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0);
            CollectDrawers();
            CollectAutoDrawers();
        }

        private void CollectDrawers()
        {
            var drawerTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => a.GetTypes())
                            .Where(t => typeof(PropertyDrawerBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in drawerTypes)
            {
                var instance = (PropertyDrawerBase)Activator.CreateInstance(type);
                Debug.Log($"Registering drawer for attribute: {instance.BindType.Name}");
                _stylizedDrawers[instance.BindType] = instance;
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
                    // 1️⃣ Handle IStylizeAttribute
                    var attr = fieldInfo.GetCustomAttributes(typeof(IStylized), true).FirstOrDefault() as IStylized;
                    if (attr != null && _stylizedDrawers.TryGetValue(attr.GetType(), out var drawer))
                    {

                        var element = new VisualElement();
                        drawer.BindTargetAttribute(attr);
                        element = drawer.CreatePropertyGUI(prop, element);

                        root.Add(element);
                        continue;
                    }

                    // 2️⃣ Let AutoDrawer handle automatically
                    if (TryHandleAutoDrawer(fieldInfo, prop, root))
                    {
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
                var attr = method.GetCustomAttributes(typeof(IStylized), true).FirstOrDefault() as IStylized;
                if (attr != null && _stylizedDrawers.TryGetValue(attr.GetType(), out var drawer))
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
