using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public abstract class UIStyle : ScriptableObject
{
    [SerializeField] private string styleName;
    public string StyleName => styleName;

    public VisualTreeAsset uxml;
    public StyleSheet uss;
}
