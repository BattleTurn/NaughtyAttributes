using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace CustomAttributes.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class UIAttributeInspector : UnityEditor.Editor
    {
        private static Dictionary<Type, IUIPropertyDrawer> _drawers;

        static UIAttributeInspector()
        {
            _drawers = new Dictionary<Type, IUIPropertyDrawer>();
            var drawerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IUIPropertyDrawer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in drawerTypes)
            {
                var instance = (IUIPropertyDrawer)Activator.CreateInstance(type);
                _drawers[instance.AttributeType] = instance;
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
                        var attr = fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), true).FirstOrDefault() as PropertyAttribute;
                        if (attr != null && _drawers.TryGetValue(attr.GetType(), out var drawer))
                        {
                            drawer.Setup(fieldInfo);

                            // tạo element mới cho từng field
                            var element = new VisualElement();
                            element = drawer.CreatePropertyGUI(iterator.Copy(), element);

                            root.Add(element);
                            continue;
                        }
                    }

                    // Default UI
                    var defaultField = new PropertyField(iterator.Copy());
                    root.Add(defaultField);

                } while (iterator.NextVisible(false));
            }

            return root;
        }
    }
}
