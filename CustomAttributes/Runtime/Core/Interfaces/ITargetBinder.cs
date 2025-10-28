using System;

namespace StylizeAttributes.Core
{
    public interface ITargetBinder<T>
    {
        public T TargetObject { get; }
    }
}