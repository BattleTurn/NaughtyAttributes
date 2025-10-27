using System;

namespace StylizeAttributes.Core
{
    /// <summary>
    /// Makes a field read-only in the inspector.
    /// </summary>
    /// <remarks>
    /// This attribute is only for visual purposes and does not affect the field's behavior in any way.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class ReadOnlyAttribute : Attribute, IStylized
    {
        private string _style = "Default";
        
        public string Name { get; protected set; }

        public string StyleName => _style;

        public ReadOnlyAttribute(Enum enumValue)
        {
            Name = enumValue.ToString();
        }

        public ReadOnlyAttribute() : base()
        {
        }
    }
}