using System;

namespace StylizeAttributes.Core
{
    public abstract class SpecialCaseDrawerAttribute : Attribute, IStylizeAttribute
    {

        public string StyleName { get;  private set; }

        public SpecialCaseDrawerAttribute(Enum enumValue)
        {
            StyleName = enumValue.ToString();
        }

        public SpecialCaseDrawerAttribute()
        {
            StyleName = "Default";
        }
    }
}
