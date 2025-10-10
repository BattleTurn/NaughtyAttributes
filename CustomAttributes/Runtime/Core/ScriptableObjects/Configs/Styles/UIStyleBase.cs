using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StylizeAttributes.Core
{
    [Serializable]
    public abstract class UIStyleBase : ScriptableObject, IAttributeTypeBinder
    {
        [SerializeField] private string styleName;

        public VisualTreeAsset uxml;
        public StyleSheet uss;

        public string StyleName => styleName;
        public abstract Type BindAttributeType { get; }

        public void Initialize(string styleName)
        {
            this.styleName = styleName;
        }
    }

}