using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

namespace CustomAttributes.Runtime
{
    public static class UIStyleEnumGenerator
    {
        private const string OutputFolder = "Assets/Scripts/Generated/UIStyles/";

        public static void GenerateEnums(UIStyleConfig config)
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            var grouped = config.styles.GroupBy(s => s.GetType().Name);

            foreach (var group in grouped)
            {
                string typeName = group.Key.Replace("Style", ""); // e.g. UIButtonStyle -> Button
                string enumName = typeName + "StyleEnum";

                string code = "public enum " + enumName + "\n{\n";
                foreach (var style in group)
                {
                    code += $"    {style.StyleName},\n";
                }
                code += "}\n";

                File.WriteAllText(OutputFolder + enumName + ".cs", code);
                Debug.Log($"[UIStyleEnumGenerator] Generated enum {enumName} with {group.Count()} entries");
            }

            AssetDatabase.Refresh();
        }

        public static void GenerateEnums(List<Type> config)
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            var grouped = config.GroupBy(s => s.GetType().Name);

            foreach (var group in grouped)
            {
                string typeName = group.Key.Replace("Style", ""); // e.g. UIButtonStyle -> Button
                string enumName = typeName + "StyleEnum";

                string code = "public enum " + enumName + "\n{\n";
                foreach (var style in group)
                {
                    code += $"    {"Default"},\n";
                }
                code += "}\n";

                File.WriteAllText(OutputFolder + enumName + ".cs", code);
                Debug.Log($"[UIStyleEnumGenerator] Generated enum {enumName} with {group.Count()} entries");
            }

            AssetDatabase.Refresh();
        }
    }
}