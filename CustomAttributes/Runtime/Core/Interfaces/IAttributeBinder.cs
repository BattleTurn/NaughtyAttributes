using System;

namespace CustomAttributes.Core
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