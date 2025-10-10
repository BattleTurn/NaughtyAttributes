using System;

namespace StylizeAttributes.Core
{
    public abstract class UIStyle<T> : UIStyleBase, IAttributeTypeBinder where T : Attribute
    {
        public override Type BindAttributeType => typeof(T);
    }
}