using System;

namespace CustomAttributes.Runtime
{
    /// <summary>
    /// Makes a field read-only in the inspector.
    /// </summary>
    /// <remarks>
    /// This attribute is only for visual purposes and does not affect the field's behavior in any way.
    /// </remarks>
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class UIReadOnlyAttribute : UIBaseAttribute
    {
        public string Name { get; protected set; }

        public UIReadOnlyAttribute(Enum enumValue) : base(enumValue)
        {
            Name = enumValue.ToString();
        }

        public UIReadOnlyAttribute() : base()
        {
        }
    }
}