using System;
using UnityEngine;

namespace CustomAttributes.Runtime
{
    public abstract class UIBaseAttribute : PropertyAttribute
    {
        public string Style { get; }

        public UIBaseAttribute(Enum enumValue)
        {
            Style = enumValue.ToString();
        }

        public UIBaseAttribute() : this("Default")
        {
        }

        private UIBaseAttribute(string enumName)
        {
            Style = enumName;
        }
    }
}