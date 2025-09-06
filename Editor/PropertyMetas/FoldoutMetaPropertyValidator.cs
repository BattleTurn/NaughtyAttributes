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
            if (fold == null) return false; ;

            if (_recDepth > MaxDepth) return false;

            var so = property.serializedObject;
            var target = so != null ? so.targetObject : null;
            if (target == null) return false;

            string groupName = fold.Name ?? string.Empty;
            string containerPath = GetContainerPath(property);
            string guardKey = target.GetInstanceID() + "|FOLD|" + property.propertyPath;
            if (!_activeGuards.Add(guardKey)) return false;
            _recDepth++;
            try
            {

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

                var helpStyle = EditorStyles.helpBox;
                bool nestedContainer = !string.IsNullOrEmpty(containerPath);
                NaughtyEditorGUI.BeginGroupBody(nestedContainer, helpStyle);
                int drawableCount = 0;
                for (int i = 0; i < group.Count; i++) if (group[i].name != "m_Script") drawableCount++;
                bool expanded = saved.Value;
                NaughtyEditorGUI.DrawExtendableGroupHeader(groupName, drawableCount, ref expanded, helpStyle, labelOffsetX:4f, expandBackground:true, useIndent:false, nestedContainer:nestedContainer);
                if (expanded != saved.Value) saved.Value = expanded;
                GUILayout.Space(2);

                if (saved.Value)
                {
                    int savedIndent = EditorGUI.indentLevel;
                    // Root foldout: indent inner fields; nested foldout: keep same indent to align with siblings
                    if (!nestedContainer)
                        EditorGUI.indentLevel = savedIndent + 1;
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
                            bool isComplex = gp.propertyType == SerializedPropertyType.Generic && !gp.isArray;
                            if (isComplex)
                            {
                                DrawComplexAllowNestedGroups(gp);
                                continue;
                            }
                            NaughtyEditorGUI.PropertyField_Layout_IgnoreFoldout(gp, includeChildren: true);
                        }
                    }
                    finally
                    {
                        EditorGUI.indentLevel = savedIndent;
                    }
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
