using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomAttributes.Runtime
{
    [Serializable]
    public abstract class UIStyle : ScriptableObject
    {
        [SerializeField] private string styleName;

        public VisualTreeAsset uxml;
        public StyleSheet uss;

        public abstract Type Type { get; }
        public string StyleName => styleName;
    }

}