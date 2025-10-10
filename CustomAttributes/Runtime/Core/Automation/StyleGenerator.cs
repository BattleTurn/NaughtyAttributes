using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StylizeAttributes.Core
{
    public static class StyleGenerator
    {
        public static void EnsureDefaultStylesExist()
        {
            var config = UIStyleConfig.Instance;

            // Define default styles to ensure
            var defaultStyles = new List<Type>
            {
                typeof(UIButtonStyle),
                typeof(UIReadOnlyStyle)
            };

            foreach (var styleType in defaultStyles)
            {
                if (!config.styles.Any(s => s.GetType() == styleType))
                {
                    CreateDefaultStyle(styleType);
                }
            }

            EditorUtility.SetDirty(config);
        }

        public static void CreateDefaultStyle(Type styleType)
        {
            // Compute Unity-relative and absolute paths
            var folderRelative = $"Assets/{PathNameConst.UI_CONFIG_STYLE_PATH}";
            var assetPath = $"{folderRelative}/{styleType.Name}.asset";
            var folderAbsolute = System.IO.Path.Combine(Application.dataPath, PathNameConst.UI_CONFIG_STYLE_PATH).Replace('\\', '/');

            // If asset already exists, load and register it instead of re-creating
            var existing = AssetDatabase.LoadAssetAtPath<UIStyleBase>(assetPath);
            if (existing != null)
            {
                var existingConfig = UIStyleConfig.Instance;
                if (!existingConfig.styles.Contains(existing))
                {
                    existingConfig.styles.Add(existing);
                    EditorUtility.SetDirty(existingConfig);
                }
                return;
            }

            var style = ScriptableObject.CreateInstance(styleType) as UIStyleBase;
            if (style == null)
            {
                Debug.LogError($"Failed to create instance of {styleType.Name}");
                return;
            }

            style.Initialize("Default");

            // Ensure the directory exists on disk (absolute path)
            Debug.Log($"Ensuring directory exists: {folderAbsolute}");
            if (!System.IO.Directory.Exists(folderAbsolute))
            {
                System.IO.Directory.CreateDirectory(folderAbsolute);
            }

            // Assign USS and UXML files
            var ussPath = $"{PathNameConst.PACKAGE_USS_PATH}/{styleType.Name.Replace("Style", "Field")}.uss";
            var uxmlPath = $"{PathNameConst.PACKAGE_UXML_PATH}/{styleType.Name.Replace("Style", "Field")}.uxml";

            if (System.IO.File.Exists(ussPath))
            {
                style.uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                Debug.Log($"Assigned USS: {ussPath} to {styleType.Name}");
            }
            else
            {
                Debug.LogWarning($"USS file not found: {ussPath}");
            }

            if (System.IO.File.Exists(uxmlPath))
            {
                style.uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                Debug.Log($"Assigned UXML: {uxmlPath} to {styleType.Name}");
            }
            else
            {
                Debug.LogWarning($"UXML file not found: {uxmlPath}");
            }

            // Save the ScriptableObject as an asset (Unity-relative path required)
            AssetDatabase.CreateAsset(style, assetPath);
            AssetDatabase.SaveAssets();

            // Add the created style to the UIStyleConfig
            var config = UIStyleConfig.Instance;
            config.styles.Add(style);
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(style);
        }
    }
}