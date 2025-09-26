using UnityEditor;
using UnityEngine;

public sealed class ElementColorData
{
    [SerializeField]
    private Color _evenLightColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField]
    private Color _evenDarkColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField]
    private Color _oddLightColor = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField]
    private Color _oddDarkColor = new Color(0.20f, 0.20f, 0.20f, 1f);

    [Header("Element Outline")]
    [SerializeField]
    private Color _evenOutlineColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField]
    private Color _evenOutlineDarkColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField]
    private Color _oddOutlineLightColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    [SerializeField]
    private Color _oddOutlineDarkColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    public Color GetElementColor(int index)
    {
        if (index % 2 == 0)
            return EditorGUIUtility.isProSkin ? _evenDarkColor : _evenLightColor;
        return EditorGUIUtility.isProSkin ? _oddDarkColor : _oddLightColor;
    }

    public Color GetOutlineColor(int index)
    {
        if (index % 2 == 0)
            return EditorGUIUtility.isProSkin ? _evenOutlineDarkColor : _evenOutlineColor;
        return EditorGUIUtility.isProSkin ? _oddOutlineDarkColor : _oddOutlineLightColor;
    }
}