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
            string containerPath = GetContainerPath(property);

            // Collect contiguous blocks of properties with same name+container
            List<List<SerializedProperty>> blocks = new List<List<SerializedProperty>>();
            List<SerializedProperty> current = null;
            using (var it = so.GetIterator())
            {
                if (it.NextVisible(true))
                {
                    do
                    {
                        var p = it.Copy();
                        if (p == null) continue;
                        var a = PropertyUtility.GetAttribute<FoldoutAttribute>(p);
                        bool match = a != null && a.Name == groupName && PropertyUtility.IsVisible(p) && GetContainerPath(p) == containerPath;
                        if (match)
                        {
                            if (current == null) current = new List<SerializedProperty>();
                            current.Add(p.Copy());
                        }
                        else
                        {
                            if (current != null)
                            {
                                blocks.Add(current);
                                current = null;
                            }
                        }
                    }
                    while (it.NextVisible(false));
                }
            }
            if (current != null) blocks.Add(current);

            // Find the block that contains this property
            List<SerializedProperty> group = null;
            foreach (var b in blocks)
            {
                for (int i = 0; i < b.Count; i++)
                {
                    if (b[i].propertyPath == property.propertyPath)
                    {
                        group = b;
                        break;
                    }
                }
                if (group != null) break;
            }

            if (group == null || group.Count == 0)
                return false;

            // Draw only for the first property in this contiguous block
            if (property.propertyPath != group[0].propertyPath)
                return false;

            int id = target.GetInstanceID();
            string stateKey = id + ".NA.Foldout." + groupName + "|" + containerPath + "|" + group[0].propertyPath;
            if (!s_states.TryGetValue(stateKey, out var saved))
            {
                saved = new SavedBool(stateKey, false);
                s_states[stateKey] = saved;
            }

            // Surround the group with a thin helpBox like BoxGroup
            var helpStyle = EditorStyles.helpBox;
            EditorGUILayout.BeginVertical(helpStyle);

            // RL-style header with foldout functionality; keep visuals flush but respect inner padding for content
            const float headerHeight = 20f;
            Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
            // Stretch background to the box edges
            Rect headerBgRect = headerRect;
            headerBgRect.x -= helpStyle.padding.left;
            headerBgRect.width += helpStyle.padding.left + helpStyle.padding.right;
            headerBgRect.y -= helpStyle.padding.top;

            // Draw header background using RL Header style if available
            var rlHeader = GUI.skin.FindStyle("RL Header");
            if (rlHeader != null)
            {
                GUI.Label(headerBgRect, GUIContent.none, rlHeader);
            }
            else
            {
                Color bg = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 0.95f) : new Color(0.80f, 0.80f, 0.80f, 1f);
                EditorGUI.DrawRect(headerBgRect, bg);
            }

            // Foldout arrow — keep inside the inner padding of the box group
            float arrowW = 14f;
            float innerLeft = headerRect.x; // within helpBox inner area
            Rect arrowRect = new Rect(innerLeft + 12f, headerRect.y + (headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f,
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
                if (gp.name == "m_Script") continue;
                drawableCount++;
            }
            var labelRect = new Rect(arrowRect.xMax + 6f, headerRect.y, headerRect.width - (arrowRect.xMax - headerRect.x) - 12f, headerRect.height);
            GUIStyle headerLabel = EditorStyles.boldLabel;
            var content = new GUIContent(string.IsNullOrEmpty(groupName) ? $": {drawableCount}" : $"{groupName}: {drawableCount}");
            headerLabel.alignment = TextAnchor.MiddleLeft;
            EditorGUI.LabelField(labelRect, content, headerLabel);

            GUILayout.Space(2);

            if (saved.Value)
            {
                int savedIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = savedIndent + 1; // indent inner content so nested arrows live inside the box
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

                        // Draw each grouped property (including owner) via Naughty pipeline, suppressing foldout regrouping
                        NaughtyEditorGUI.PropertyField_Layout_IgnoreFoldout(gp, includeChildren: true);
                    }
                }
                finally
                {
                    EditorGUI.indentLevel = savedIndent;
                }
            }
        EditorGUILayout.EndVertical();
            return false;
        }

        private static string GetContainerPath(SerializedProperty p)
        {
            if (p == null) return string.Empty;
            var path = p.propertyPath;
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot) : string.Empty;
        }
    }
}
