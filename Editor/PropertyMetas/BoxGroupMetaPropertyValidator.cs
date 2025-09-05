using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class BoxGroupMetaPropertyValidator : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            if (property == null) return false;

            var box = PropertyUtility.GetAttribute<BoxGroupAttribute>(property);
            if (box == null) return false;

            var so = property.serializedObject;
            var target = so != null ? so.targetObject : null;
            if (target == null) return false;

            string groupName = box.Name ?? string.Empty;
            string containerPath = BoxGroupPathUtil.GetContainerPath(property);

            // Collect all visible properties that share this group name, preserving inspector order
            var group = new List<SerializedProperty>();
            using (var it = so.GetIterator())
            {
                if (it.NextVisible(true))
                {
                    do
                    {
                        var p = it.Copy();
                        if (p == null) continue;
                        var a = PropertyUtility.GetAttribute<BoxGroupAttribute>(p);
                        if (a != null && (a.Name ?? string.Empty) == groupName)
                        {
                            if (PropertyUtility.IsVisible(p) && BoxGroupPathUtil.GetContainerPath(p) == containerPath)
                                group.Add(p.Copy());
                        }
                    }
                    while (it.NextVisible(false));
                }
            }

            if (group.Count == 0)
                return false;

            // Only the FIRST property in the group draws the box + children; others are skipped
            if (property.propertyPath != group[0].propertyPath)
                return false;

            var helpStyle = EditorStyles.helpBox;
            EditorGUILayout.BeginVertical(helpStyle);
            // Header bar styled similar to ReorderableList header
            if (!string.IsNullOrEmpty(groupName))
            {
                const float headerHeight = 20f;
                Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
                // Compensate helpBox padding to span full inner width and height (flush to box edges)
                headerRect.x -= helpStyle.padding.left;
                headerRect.width += helpStyle.padding.left + helpStyle.padding.right;
                headerRect.y -= helpStyle.padding.top;

                // Draw RL-style header background if available; otherwise fallback to a flat color
                var rlHeader = GUI.skin.FindStyle("RL Header");
                if (rlHeader != null)
                {
                    GUI.Label(headerRect, GUIContent.none, rlHeader);
                }
                else
                {
                    Color bg = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 1f) : new Color(0.80f, 0.80f, 0.80f, 1f);
                    EditorGUI.DrawRect(headerRect, bg);
                }

                // Label: "Name: count" centered vertically, left aligned like RL header title
                int drawableCount = 0;
                for (int i = 0; i < group.Count; i++)
                {
                    var gp = group[i];
                    if (gp.name == "m_Script") continue;
                    drawableCount++;
                }
                var labelRect = new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 16f, headerRect.height);
                GUIStyle headerLabel = EditorStyles.boldLabel;
                var content = new GUIContent(string.IsNullOrEmpty(groupName) ? $": {drawableCount}" : $"{groupName}: {drawableCount}");
                headerLabel.alignment = TextAnchor.MiddleLeft;
                EditorGUI.LabelField(labelRect, content, headerLabel);

                GUILayout.Space(2);
            }

            int savedIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = savedIndent + 1; // indent inner content so arrows sit inside the box
            try
            {
                foreach (var gp in group)
                {
                    if (gp.name == "m_Script")
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.PropertyField(gp);
                        }
                        continue;
                    }
                    // Draw each grouped property via Naughty pipeline, suppressing regrouping (also safe for owner)
                    NaughtyEditorGUI.PropertyField_Layout_IgnoreGroups(gp, includeChildren: true);
                }
            }
            finally
            {
                EditorGUI.indentLevel = savedIndent;
            }

            EditorGUILayout.EndVertical();

            return false;
        }
    }

    internal static class BoxGroupPathUtil
    {
        public static string GetContainerPath(SerializedProperty p)
        {
            if (p == null) return string.Empty;
            var path = p.propertyPath;
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot) : string.Empty;
        }
    }
}