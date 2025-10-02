using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

public class UIButtonDrawer : UIPropertyDrawerBase<UIButtonStyleEnum>
{
    public override Type TargetAttribute => typeof(UIButtonAttribute);

    private UIButtonAttribute attribute;

    public UIButtonDrawer(UIButtonStyleEnum enumValue) : base(enumValue)
    {
    }

    public UIButtonDrawer() : this(UIButtonStyleEnum.Default)
    {
    }

    public override void Setup(FieldInfo fieldInfo)
    {
        attribute = fieldInfo.GetCustomAttribute<UIButtonAttribute>();
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
    {
        LoadUXML(root);
        LoadUSS(root);

        var button = root.Q<Button>("button");

        Debug.Log("CreatePropertyGUI for Button: " + button.name);
        if (button != null)
        {
            // Text hiển thị từ MethodName
            button.text = attribute != null ? attribute.MethodName : "Unnamed Button";

            button.clicked += () =>
            {
                Debug.Log($"Button '{button.text}' clicked!");
                if (attribute != null)
                {
                    var method = property.serializedObject.targetObject
                        .GetType()
                        .GetMethod(attribute.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (method != null)
                        method.Invoke(property.serializedObject.targetObject, null);
                    else
                        Debug.LogWarning($"Method '{attribute.MethodName}' not found on {property.serializedObject.targetObject}");
                }
            };
        }

        return root;
    }
}
