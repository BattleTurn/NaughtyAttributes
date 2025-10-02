using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

[CustomPropertyDrawer(typeof(TestAttribute))]
public class TestAttributeDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var container = new VisualElement();

        Debug.Log("Creating property CreatePropertyGUI for TestAttribute: " + property.displayName);

        var label = new Label("This is a test attribute drawer");
        container.Add(label);

        var field = new PropertyField(property.FindPropertyRelative("Message"), "Message");
        container.Add(field);

        return container;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Debug.Log("Creating property OnGUI for TestAttribute: " + property.displayName);
        CreatePropertyGUI(property);
    }
}