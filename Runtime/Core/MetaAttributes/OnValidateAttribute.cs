using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class OnValidateAttribute : MetaAttribute
    {
        public string CallbackName { get; private set; }

        public OnValidateAttribute(string callbackName)
        {
            CallbackName = callbackName;
        }
    }
}
