using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StylizeAttributes.Core
{
    [Serializable]
    public abstract class UIStyleBase : ScriptableObject, ITypeBinder
    {
        [SerializeField] private string styleName;

        public VisualTreeAsset uxml;
        public StyleSheet uss;

        public string StyleName => styleName;
        public abstract Type BindType { get; }

        public void Initialize(string styleName)
        {
            this.styleName = styleName;
        }
    }

}