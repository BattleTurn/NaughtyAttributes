using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_PATH + "/" + nameof(UIStyleConfig), fileName = nameof(UIStyleConfig))]
public class UIStyleConfig : ScriptableObject
{
    private static UIStyleConfig _instance;

    public List<UIStyle> styles = new List<UIStyle>();

    private Dictionary<Type, List<UIStyle>> styleMap = new();

    public static UIStyleConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                InitializeInstance();
            }
            return _instance;
        }
    }

#if UNITY_EDITOR
    private static void InitializeInstance()
    {
        // Try to load from Resources by type

        UIStyleConfig[] configs = AssetDatabase.FindAssets("t:UIStyleConfig")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<UIStyleConfig>(path))
            .ToArray();
        Debug.Log($"[UIStyleConfig] Found {configs.Length} UIStyleConfig assets in Resources.");
        if (configs.Length > 1)
            Debug.LogWarning("[UIStyleConfig] Multiple UIStyleConfig assets found in Resources. Using the first one found.");
        else if (configs.Length == 0 || configs == null)
            AutoCreateInstance();
        else
            _instance = configs.FirstOrDefault();
    }

    private static void AutoCreateInstance()
    {
        _instance = CreateInstance<UIStyleConfig>();

        var directory = PathNameConst.UI_CONFIG_PATH;
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(_instance, PathNameConst.UI_CONFIG_PATH + "/" + nameof(UIStyleConfig) + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
#endif

    public List<UIStyle> this[Type type]
    {
        get
        {
            StyleMap();
            return styleMap[type];
        }
    }

    public UIStyle this[Enum styleEnum]
    {
        get
        {
            var type = Type.GetType(styleEnum.GetType().FullName.Replace("Enum", ""));
            Debug.Log($"[UIStyleConfig] styleEnum Type: {styleEnum.GetType().FullName}, Resolved Style Type: {type}");
            if (type == null) return null;
            Debug.Log($"[UIStyleConfig] Getting style of type {type} for enum {styleEnum}");
            StyleMap();
            if (styleMap.TryGetValue(type, out var list))
            {
                string enumName = styleEnum.ToString();
                return list.Where(s => s.StyleName == enumName).FirstOrDefault();
            }
            return null;
        }
    }

    private void OnValidate()
    {
        if (styleMap == null || (styleMap != null && styleMap.Count != 0))
        {
            UIStyleEnumGenerator.GenerateEnums(new List<Type> { typeof(UIButtonStyle), typeof(UIReadOnlyStyle) });
            return;
        }
        UIStyleEnumGenerator.GenerateEnums(this);
    }

    private void StyleMap()
    {
        if (styleMap.Count == 0)
        {
            styleMap = styles.GroupBy(s => s.GetType()).ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
#endif