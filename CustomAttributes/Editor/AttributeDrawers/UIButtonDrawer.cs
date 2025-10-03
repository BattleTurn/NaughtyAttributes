using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

using CustomAttributes.Runtime;

namespace CustomAttributes.Editor
{
    public class UIButtonDrawer : UIPropertyDrawerBase<UIButtonAttribute>
    {
        public override void Setup(FieldInfo fieldInfo)
        {
            TargetAttribute = fieldInfo.GetCustomAttribute<UIButtonAttribute>();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            LoadUXML(root);
            LoadUSS(root);

            var button = root.Q<Button>("button");

            Debug.Log("CreatePropertyGUI for Button: " + button.name);
            if (button != null)
            {
                button.text = TargetAttribute != null ? TargetAttribute.MethodName : "Unnamed Button";

                button.clicked += () =>
                {
                    Debug.Log($"Button '{button.text}' clicked!");
                    if (TargetAttribute != null)
                    {
                        var method = property.serializedObject.targetObject
                            .GetType()
                            .GetMethod(TargetAttribute.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (method != null)
                            method.Invoke(property.serializedObject.targetObject, null);
                        else
                            Debug.LogWarning($"Method '{TargetAttribute.MethodName}' not found on {property.serializedObject.targetObject}");
                    }
                };
            }

            return root;
        }
    }
}