using System;
using System.Reflection;
using UnityEditor;

using UnityEngine.UIElements;

namespace StylizeAttributes.Editor
{
    public partial class StylizedAttributeInspector
    {
        private void CollectAutoDrawers()
        {
            var drawerTypes = TypeCache.GetTypesDerivedFrom<AutoDrawerBase>();
            foreach (var type in drawerTypes)
            {
                if (type.IsAbstract) continue;
                if (Activator.CreateInstance(type) is AutoDrawerBase drawer)
                    _autoDrawers[drawer.BindType] = drawer;
            }
        }

        private bool TryHandleAutoDrawer(FieldInfo field, SerializedProperty property, VisualElement root)
        {
            if (field == null) return false;

            // Try to match the AutoDrawer<T> for this field type
            foreach (var kvp in _autoDrawers)
            {
                var bindType = kvp.Key;

                // Match both exact type and assignable (e.g., enum inherits Enum)
                if (bindType.IsAssignableFrom(field.FieldType))
                {
                    var drawer = kvp.Value;
                    var element = new VisualElement();
                    element = drawer.CreatePropertyGUI(property.Copy(), element);
                    root.Add(element);
                    return true;
                }
            }

            return false;
        }
    }
}