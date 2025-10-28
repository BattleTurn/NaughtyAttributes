using System;

namespace StylizeAttributes.Core
{
    public abstract class UIStyle<T> : UIStyleBase, ITypeBinder
    {
        public override Type BindType => typeof(T);
    }
}