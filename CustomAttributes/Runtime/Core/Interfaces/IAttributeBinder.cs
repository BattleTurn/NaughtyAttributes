using System;

namespace StylizeAttributes.Core
{
    public interface ITypeBinder
    {
        public Type BindType { get; }
    }

    public interface IAttributeBinder<T> where T : Attribute
    {
        public T TargetAttribute { get; }
    }
}