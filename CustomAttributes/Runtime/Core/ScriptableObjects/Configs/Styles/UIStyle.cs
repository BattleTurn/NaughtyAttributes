using System;

namespace StylizeAttributes.Core
{
    public abstract class UIStyle<T> : UIStyleBase, ITypeBinder where T : Attribute
    {
        public override Type BindType => typeof(T);
    }
}