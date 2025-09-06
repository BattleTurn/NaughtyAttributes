using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public class FoldoutMetaPropertyValidator : MetaPropertyValidatorBase
    {
        private static readonly Dictionary<string, SavedBool> s_states = new Dictionary<string, SavedBool>();
        private static readonly HashSet<string> _activeGuards = new HashSet<string>();
        [ThreadStatic] private static int _recDepth;
        private const int MaxDepth = 64;

        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            if (property == null) return false;

            var fold = PropertyUtility.GetAttribute<FoldoutAttribute>(property);
            if (fold == null) return false;

            if (_recDepth > MaxDepth) return false;
            var so = property.serializedObject;
            var target = so != null ? so.targetObject : null;
            if (target == null) return false;

            string groupName = fold.Name ?? string.Empty;
            string containerPath = GetContainerPath(property);
            bool nestedContainer = !string.IsNullOrEmpty(containerPath);

            string guardKey = target.GetInstanceID() + "|FOLD|" + property.propertyPath;
            if (!_activeGuards.Add(guardKey)) return false; // re-entrant
            _recDepth++;
            try
            {
                // Collect SAME-CONTAINER direct siblings like BoxGroup does (fixes nested foldouts)
                List<SerializedProperty> group = new List<SerializedProperty>();
                if (string.IsNullOrEmpty(containerPath))
                {
                    using (var it = so.GetIterator())
                    {
                        if (it.NextVisible(true))
                        {
                            do
                            {
                                if (it.depth != 0) continue;
                                var a = PropertyUtility.GetAttribute<FoldoutAttribute>(it);
                                if (a != null && (a.Name ?? string.Empty) == groupName && PropertyUtility.IsVisible(it))
                                    group.Add(it.Copy());
                            } while (it.NextVisible(false));
                        }
                    }
                }
                else
                {
                    var parent = so.FindProperty(containerPath);
                    if (parent != null)
                    {
                        int parentDepth = parent.depth;
                        var it = parent.Copy();
                        var end = it.GetEndProperty();
                        bool enter = true;
                        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                        {
                            if (it.depth <= parentDepth) break;
                            if (it.depth == parentDepth + 1)
                            {
                                var a = PropertyUtility.GetAttribute<FoldoutAttribute>(it);
                                if (a != null && (a.Name ?? string.Empty) == groupName && PropertyUtility.IsVisible(it))
                                    group.Add(it.Copy());
                            }
                            enter = false;
                        }
                    }
                }

                if (group.Count == 0) return false;
                if (property.propertyPath != group[0].propertyPath) return false; // only first draws

                // Persist expansion
                int id = target.GetInstanceID();
                string stateKey = id + ".NA.Foldout." + groupName + "|" + containerPath + "|" + group[0].propertyPath;
                if (!s_states.TryGetValue(stateKey, out var saved))
                {
                    saved = new SavedBool(stateKey, false);
                    s_states[stateKey] = saved;
                }

                var helpStyle = EditorStyles.helpBox;
                NaughtyEditorGUI.BeginGroupBody(nestedContainer, helpStyle);
                int drawableCount = 0;
                for (int i = 0; i < group.Count; i++) if (group[i].name != "m_Script") drawableCount++;
                bool expanded = saved.Value;
                NaughtyEditorGUI.DrawExtendableGroupHeader(groupName, drawableCount, ref expanded, helpStyle, labelOffsetX:4f, expandBackground:true, useIndent:false, nestedContainer:nestedContainer);
                if (expanded != saved.Value) saved.Value = expanded;
                GUILayout.Space(2);

                if (expanded)
                {
                    int savedIndent = EditorGUI.indentLevel;
                    if (!nestedContainer) EditorGUI.indentLevel = savedIndent + 1; // root only
                    try
                    {
                        foreach (var gp in group)
                        {
                            if (gp.name == "m_Script")
                            {
                                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(gp, includeChildren:false);
                                continue;
                            }
                            bool isComplex = gp.propertyType == SerializedPropertyType.Generic && !gp.isArray;
                            if (isComplex)
                            {
                                DrawComplexAllowNestedGroups(gp);
                                continue;
                            }
                            NaughtyEditorGUI.PropertyField_Layout_IgnoreFoldout(gp, includeChildren:true);
                        }
                    }
                    finally { EditorGUI.indentLevel = savedIndent; }
                }

                NaughtyEditorGUI.EndGroupBody();
                return false;
            }
            finally
            {
                _recDepth--;
                _activeGuards.Remove(guardKey);
            }
        }

        private static string GetContainerPath(SerializedProperty p)
        {
            if (p == null) return string.Empty;
            var path = p.propertyPath;
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot) : string.Empty;
        }

        private void DrawComplexAllowNestedGroups(SerializedProperty complex)
        {
            if (complex == null) return;
            // Draw only foldout line without re-entering foldout validator
            EditorGUILayout.PropertyField(complex, includeChildren: false);
            if (!complex.isExpanded) return;
            int saved = EditorGUI.indentLevel;
            EditorGUI.indentLevel = saved + 1;
            try
            {
                bool drewAny = false;
                object instance = PropertyUtility.GetTargetObjectOfProperty(complex);
                if (instance != null)
                {
                    var type = instance.GetType();
                    foreach (var fi in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        if (fi.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                        if (!fi.IsPublic && !fi.IsDefined(typeof(SerializeField), true)) continue;
                        var child = complex.FindPropertyRelative(fi.Name);
                        if (child == null) continue;
                        if (child.name == "m_Script") continue;
                        NaughtyEditorGUI.PropertyField_Layout(child, includeChildren: true);
                        drewAny = true;
                    }
                }
                if (!drewAny)
                {
                    var copy = complex.Copy();
                    var end = copy.GetEndProperty();
                    bool enter = true; int safety = 0;
                    while (copy.NextVisible(enter) && !SerializedProperty.EqualContents(copy, end))
                    {
                        if (copy.name == "m_Script") { enter = false; continue; }
                        NaughtyEditorGUI.PropertyField_Layout(copy.Copy(), includeChildren: true);
                        if (++safety > 500) break;
                        enter = false;
                    }
                }
            }
            finally
            {
                EditorGUI.indentLevel = saved;
            }
        }
    }
}
