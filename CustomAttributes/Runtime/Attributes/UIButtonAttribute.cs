using System;
using UnityEngine;

namespace CustomAttributes.Runtime
{
    public class UIButtonAttribute : UIBaseAttribute
    {
        public string MethodName { get; }

        public UIButtonAttribute(string methodName, Enum enumValue) : base(enumValue)
        {
            MethodName = methodName;
        }

        public UIButtonAttribute(string methodName) : base()
        {
            MethodName = methodName;
        }
    }
}