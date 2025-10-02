using UnityEngine;

public class TestAttribute : PropertyAttribute
{
    public string message;

    public TestAttribute(string message)
    {
        this.message = message;
    }
}
