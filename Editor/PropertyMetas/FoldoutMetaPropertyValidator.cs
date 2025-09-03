using System.Collections.Generic;
using UnityEditor;

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

            saved.Value = EditorGUILayout.Foldout(saved.Value, groupName, true);
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

                    // Draw each grouped property via Naughty pipeline (recursively)
                    NaughtyEditorGUI.PropertyField_Layout(gp, includeChildren: true);
                }
            }
            return false;
        }
    }
}
