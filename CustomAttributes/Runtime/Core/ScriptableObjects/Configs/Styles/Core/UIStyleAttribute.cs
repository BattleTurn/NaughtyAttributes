using System;

namespace StylizeAttributes.Core
{
    public abstract class UIStyleAttribute<T> : UIStyleBase, ITypeBinder where T : Attribute
    {
        public override Type BindType => typeof(T);
    }
}