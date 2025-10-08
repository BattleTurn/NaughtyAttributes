using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

using CustomAttributes.Core;

namespace CustomAttributes.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class UIAttributeInspector : UnityEditor.Editor
    {

        private static Dictionary<Type, UIPropertyDrawerBase> _drawers;

        static UIAttributeInspector()
        {
            _drawers = new Dictionary<Type, UIPropertyDrawerBase>();
            var drawerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(UIPropertyDrawerBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in drawerTypes)
            {
                var instance = (UIPropertyDrawerBase)Activator.CreateInstance(type);
                _drawers[instance.BindAttributeType] = instance;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var iterator = serializedObject.GetIterator();

            if (iterator.NextVisible(true))
            {
                do
                {
                    var fieldInfo = target.GetType().GetField(iterator.name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (fieldInfo != null)
                    {
                        var attr = fieldInfo.GetCustomAttributes(typeof(IStylizeAttribute), true).FirstOrDefault() as IStylizeAttribute;
                        if (attr != null && _drawers.TryGetValue(attr.GetType(), out var drawer))
                        {

                            var element = new VisualElement();
                            element = drawer.CreatePropertyGUI(iterator.Copy(), element);

                            root.Add(element);
                            continue;
                        }
                    }

                    var defaultField = new PropertyField(iterator.Copy());
                    root.Add(defaultField);

                } while (iterator.NextVisible(false));
            }

            return root;
        }
    }
}
