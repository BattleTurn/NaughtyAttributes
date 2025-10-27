using System;

namespace StylizeAttributes.Core
{
    public abstract class SpecialCaseDrawerAttribute : Attribute, IStylized
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
