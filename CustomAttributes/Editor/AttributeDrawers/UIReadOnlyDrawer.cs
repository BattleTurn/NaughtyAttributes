using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class UIReadOnlyDrawer : UIPropertyDrawerBase<UIReadOnlyStyleEnum>
{
    public override Type TargetAttribute => typeof(UIReadOnlyAttribute);

    public UIReadOnlyDrawer(UIReadOnlyStyleEnum valueEnum) : base(valueEnum)
    {
    }

    public UIReadOnlyDrawer() : this(UIReadOnlyStyleEnum.Default)
    {
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
    {
        LoadUXML(root);
        LoadUSS(root);

        var propertyField = root.Q<PropertyField>("property");
        if (propertyField != null)
        {
            UnityEngine.Debug.Log("CreatePropertyGUI for ReadOnly: " + property.name);
            propertyField.BindProperty(property);
            propertyField.SetEnabled(false);
        }

        return root;
    }

    public override void Setup(FieldInfo fieldInfo)
    {
    }
}
