using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class BoxGroupMetaPropertyValidator : MetaPropertyValidatorBase
    {
        private static readonly HashSet<string> _activeGuards = new HashSet<string>();
        [ThreadStatic] private static int _recursionDepth;
        private const int MaxDepth = 64;
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            if (property == null) return false;

            var box = PropertyUtility.GetAttribute<BoxGroupAttribute>(property);
            if (box == null) return false;

            if (_recursionDepth > MaxDepth) return false;

            var so = property.serializedObject;
            var target = so != null ? so.targetObject : null;
            if (target == null) return false;

            string guardKey = target.GetInstanceID() + "|BOX|" + property.propertyPath;
            if (!_activeGuards.Add(guardKey)) return false; // re-entrant, skip
            _recursionDepth++;
            try
            {

                string groupName = box.Name ?? string.Empty;
                string containerPath = GetContainerPath(property);

                // Collect visible properties in SAME container (direct siblings). Root container uses depth==0 scan.
                var group = new List<SerializedProperty>();
                if (string.IsNullOrEmpty(containerPath))
                {
                    using (var it = so.GetIterator())
                    {
                        if (it.NextVisible(true))
                        {
                            do
                            {
                                if (it.depth != 0) continue; // root-level only
                                var a = PropertyUtility.GetAttribute<BoxGroupAttribute>(it);
                                if (a != null && (a.Name ?? string.Empty) == groupName && PropertyUtility.IsVisible(it))
                                    group.Add(it.Copy());
                            } while (it.NextVisible(false));
                        }
                    }
                }
                else
                {
                    // Nested: scope to parent property and only take its direct children
                    var parentProp = so.FindProperty(containerPath);
                    if (parentProp != null)
                    {
                        int parentDepth = parentProp.depth;
                        var it = parentProp.Copy();
                        var end = it.GetEndProperty();
                        bool enter = true;
                        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                        {
                            if (it.depth <= parentDepth) break; // done with this subtree
                            if (it.depth == parentDepth + 1)
                            {
                                var a = PropertyUtility.GetAttribute<BoxGroupAttribute>(it);
                                if (a != null && (a.Name ?? string.Empty) == groupName && PropertyUtility.IsVisible(it))
                                    group.Add(it.Copy());
                            }
                            enter = false; // siblings only
                        }
                    }
                }

                if (group.Count == 0)
                    return false;

                // Only the FIRST property in the group draws the box + children; others are skipped
                if (property.propertyPath != group[0].propertyPath)
                    return false;

                // Removed contiguous auto-extension: each attributed field (or set of attributed siblings) forms its own group.

                var helpStyle = EditorStyles.helpBox;
                EditorGUILayout.BeginVertical(helpStyle);
                // Header bar styled similar to ReorderableList header
                if (!string.IsNullOrEmpty(groupName))
                {
                    NaughtyEditorGUI.DrawGroupHeader(groupName, group, helpStyle, EditorStyles.boldLabel, new Vector2(4, 0));
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
                                EditorGUILayout.PropertyField(gp, includeChildren: false);
                            }
                            continue;
                        }

                        bool isComplex = gp.propertyType == SerializedPropertyType.Generic && !gp.isArray;
                            if (isComplex)
                            {
                                // Custom foldout line (avoid Unity auto child draw to prevent duplicates)
                                gp.isExpanded = EditorGUILayout.Foldout(gp.isExpanded, PropertyUtility.GetLabel(gp), true);
                                if (!gp.isExpanded) continue;

                            int savedChildIndent = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = savedChildIndent + 1;
                            try
                            {
                                var it = gp.Copy();
                                var end = it.GetEndProperty();
                                bool enterChildren = true;
                                while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
                                {
                                    if (it.name == "m_Script") { enterChildren = false; continue; }
                                    NaughtyEditorGUI.PropertyField_Layout(it.Copy(), includeChildren: true);
                                    enterChildren = false; // siblings only
                                }
                            }
                            finally
                            {
                                EditorGUI.indentLevel = savedChildIndent;
                            }
                            continue;
                        }

                        // Simple field inside this box group: ignore groups to avoid re-trigger at same level
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
            finally
            {
                _recursionDepth--;
                _activeGuards.Remove(guardKey);
            }
        }
        // (Unused legacy traversal removed; complex properties now drawn directly via Naughty pipeline)

        private static string GetContainerPath(SerializedProperty p)
        {
            if (p == null) return string.Empty;
            var path = p.propertyPath;
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot) : string.Empty;
        }
    }
}