using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomAttributes.Core
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
            var style = ScriptableObject.CreateInstance(styleType) as UIStyleBase;
            if (style == null)
            {
                Debug.LogError($"Failed to create instance of {styleType.Name}");
                return;
            }

            style.Initialize("Default");

            // Ensure the directory exists
            var directory = PathNameConst.UI_CONFIG_STYLE_PATH;
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Assign USS and UXML files
            var uiFolder = "Packages/NaughtyAttributes/CustomAttributes/Editor/AttributeDrawers/UI";
            var ussPath = $"{uiFolder}/Styles/{styleType.Name.Replace("Style", "Field")}.uss";
            var uxmlPath = $"{uiFolder}/UXMLs/{styleType.Name.Replace("Style", "Field")}.uxml";

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

            // Save the ScriptableObject as an asset
            var assetPath = $"{directory}/{styleType.Name}.asset";
            AssetDatabase.CreateAsset(style, assetPath);

            // Add the created style to the UIStyleConfig
            var config = UIStyleConfig.Instance;
            config.styles.Add(style);
        }
    }
}