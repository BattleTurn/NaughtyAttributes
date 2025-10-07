using System;
using UnityEngine;

namespace CustomAttributes.Core
{
    public class UIButtonAttribute : PropertyAttribute, IStylizeAttribute
    {
        public string MethodName { get; }

        private string _styleName = "Default";

        public string StyleName => _styleName;

        public UIButtonAttribute(string methodName, Enum enumValue)
        {
            MethodName = methodName;
            _styleName = enumValue.ToString();
        }

        public UIButtonAttribute(string methodName) : base()
        {
            MethodName = methodName;
        }
    }
}