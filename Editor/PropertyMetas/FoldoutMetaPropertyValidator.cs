using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class FoldoutMetaPropertyValidator : MetaPropertyValidatorBase
    {
        private static readonly Dictionary<string, SavedBool> s_states = new Dictionary<string, SavedBool>();

        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            if (property == null) return false;

            var fold = PropertyUtility.GetAttribute<FoldoutAttribute>(property);
            if (fold == null) return false; ;

            var so = property.serializedObject;
            var target = so != null ? so.targetObject : null;
            if (target == null) return false;

            string groupName = fold.Name ?? string.Empty;

            // Collect all visible properties that share this group name, preserving inspector order
            var group = new List<SerializedProperty>();
            using (var it = so.GetIterator())
            {
                if (it.NextVisible(true))
                {
                    do
                    {
                        var p = so.FindProperty(it.name);
                        if (p == null) continue;
                        var a = PropertyUtility.GetAttribute<FoldoutAttribute>(p);
                        if (a != null && a.Name == groupName)
                        {
                            if (PropertyUtility.IsVisible(p))
                                group.Add(p.Copy());
                        }
                    }
                    while (it.NextVisible(false));
                }
            }

            if (group.Count == 0)
                return false; // nothing to draw

            // Only the FIRST property in the group draws the foldout + children; others are skipped
            if (property.name != group[0].name)
                return false; // handled by skipping duplicate draw

            int id = target.GetInstanceID();
            string stateKey = id + ".NA.Foldout." + groupName;
            if (!s_states.TryGetValue(stateKey, out var saved))
            {
                saved = new SavedBool(stateKey, false);
                s_states[stateKey] = saved;
            }

            // Surround the group with a thin helpBox like BoxGroup
            var helpStyle = EditorStyles.helpBox;
            EditorGUILayout.BeginVertical(helpStyle);

            // RL-style header with foldout functionality, header background flush with inner box
            const float headerHeight = 20f;
            Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
            headerRect.x -= helpStyle.padding.left;
            headerRect.width += helpStyle.padding.left + helpStyle.padding.right;
            headerRect.y -= helpStyle.padding.top;

            // Draw header background using RL Header style if available
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

            // Foldout arrow — inset further inside the header
            float arrowW = 0f;
            Rect arrowRect = new Rect(headerRect.x + 16f, headerRect.y + (headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f,
                arrowW, EditorGUIUtility.singleLineHeight);
            bool newValue = EditorGUI.Foldout(arrowRect, saved.Value, GUIContent.none, true);
            if (newValue != saved.Value) saved.Value = newValue;

            // Make entire header clickable to toggle
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                saved.Value = !saved.Value;
                GUI.changed = true;
                Event.current.Use();
            }

            // Label "Name: count"
            int drawableCount = 0;
            for (int i = 0; i < group.Count; i++)
            {
                var gp = group[i];
                if (gp.name == "m_Script" || gp.name == property.name) continue;
                drawableCount++;
            }
            var labelRect = new Rect(arrowRect.xMax, headerRect.y, headerRect.width - (arrowRect.width + 16f), headerRect.height);
            GUIStyle headerLabel = EditorStyles.boldLabel;
            var content = new GUIContent(string.IsNullOrEmpty(groupName) ? $": {drawableCount}" : $"{groupName}: {drawableCount}");
            headerLabel.alignment = TextAnchor.MiddleLeft;
            EditorGUI.LabelField(labelRect, content, headerLabel);

            GUILayout.Space(2);

            if (saved.Value)
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

                    if (gp.name == property.name) // skip drawing the owner itself to prevent recursion
                        continue;

            // Draw each grouped property via Naughty pipeline (recursively) ignoring foldout regrouping
                    NaughtyEditorGUI.PropertyField_Layout_IgnoreFoldout(gp, includeChildren: true);
                }
            }
        EditorGUILayout.EndVertical();
            return false;
        }
    }
}
