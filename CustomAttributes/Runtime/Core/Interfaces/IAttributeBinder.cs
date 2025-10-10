using System;

namespace StylizeAttributes.Core
{
    public interface IAttributeTypeBinder
    {
        public Type BindAttributeType { get; }
    }

    public interface IAttributeBinder<T>
    {
        public T TargetAttribute { get; }
    }
}