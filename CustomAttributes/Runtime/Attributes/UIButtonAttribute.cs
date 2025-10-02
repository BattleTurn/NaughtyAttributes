using UnityEngine;

public class UIButtonAttribute : PropertyAttribute
{
    public string MethodName { get; }

    public UIButtonAttribute(string methodName, UIButtonStyleEnum style = UIButtonStyleEnum.Default)
    {
        MethodName = methodName;
    }
}
