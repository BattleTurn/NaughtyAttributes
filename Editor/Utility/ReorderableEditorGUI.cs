
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorGUI
    {
        private static readonly Dictionary<ListKey, ReorderableList> _arrayLists = new Dictionary<ListKey, ReorderableList>();

        private const float INDENT_WIDTH = 15.0f;

        private struct ListKey
        {
            public int id;
            public string path;
            public ListKey(int id, string path) { this.id = id; this.path = path; }
        }

        public static void CreateReorderableList(Rect rect, SerializedProperty arrayProp)
        {
            ReorderableList reorderableList = null;
            var so = arrayProp.serializedObject;

            Rect headerRect = new Rect();

            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            if (!_arrayLists.ContainsKey(key))
            {
                reorderableList = new ReorderableList(so, arrayProp, true, true, true, true)
                {
                    drawHeaderCallback = (Rect r) => {
                        // Adjust header rect for indent
                        int indentLevel = EditorGUI.indentLevel;
                        float indent = indentLevel * INDENT_WIDTH;
                        headerRect = new Rect(r.x + indent, r.y, r.width - indent, r.height);
                        DrawHeader(arrayProp, headerRect);
                    },

                    drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) =>
                    {
                        if (!arrayProp.isExpanded) return;
                        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
                        int indentLevel = EditorGUI.indentLevel;
                        float indent = indentLevel * INDENT_WIDTH;
                        r.y += 1.0f;
                        r.x += 10.0f + indent;
                        r.width -= 10.0f + indent;

                        EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), element, true);
                    },

                    elementHeightCallback = (int index) =>
                    {
                        if (!arrayProp.isExpanded) return 0f;
                        return EditorGUI.GetPropertyHeight(arrayProp.GetArrayElementAtIndex(index)) + 4.0f;
                    },

                    onAddCallback = (ReorderableList l) => ReorderableList.defaultBehaviours.DoAddButton(l),
                    onRemoveCallback = (ReorderableList l) => ReorderableList.defaultBehaviours.DoRemoveButton(l),
                    drawNoneElementCallback = (Rect rr) =>
                    {
                        if (!arrayProp.isExpanded) return;
                        int indentLevel = EditorGUI.indentLevel;
                        float indent = indentLevel * INDENT_WIDTH;
                        Rect indentedRect = new Rect(rr.x + indent, rr.y, rr.width - indent, rr.height);
                        ReorderableList.defaultBehaviours.DrawNoneElement(indentedRect, reorderableList.draggable);
                    },
                    drawFooterCallback = (Rect fr) =>
                    {
                        if (!arrayProp.isExpanded) return;
                        int indentLevel = EditorGUI.indentLevel;
                        float indent = indentLevel * INDENT_WIDTH;
                        Rect indentedRect = new Rect(fr.x + indent, fr.y, fr.width - indent, fr.height);
                        ReorderableList.defaultBehaviours.DrawFooter(indentedRect, reorderableList);
                    },
                };

                _arrayLists[key] = reorderableList;
            }

            reorderableList = _arrayLists[key];
            reorderableList.draggable = arrayProp.isExpanded;

            // If caller passes default, reserve exact height and indent to align with surrounding properties
            if (rect == default)
            {
                float listHeight;
                if (!arrayProp.isExpanded)
                {
                    reorderableList.elementHeight = 0f;
                    // Always use header height + small gap for collapsed lists
                    listHeight = EditorGUIUtility.singleLineHeight + 6f;
                }
                else
                {
                    listHeight = reorderableList.GetHeight();
                }
                Rect rr = EditorGUILayout.GetControlRect(false, listHeight);
                rr = EditorGUI.IndentedRect(rr);
                reorderableList.DoList(rr);
            }
            else
            {
                // Non-layout path when caller already has a rect
                Rect rr = rect;
                rr.height = arrayProp.isExpanded
                    ? reorderableList.GetHeight()
                    : EditorGUIUtility.singleLineHeight + 4f;
                reorderableList.DoList(rr);
            }
        }

        public static void ClearCache()
        {
            _arrayLists.Clear();
        }

        private static void DrawHeader(SerializedProperty arrayProp, Rect r)
        {
            string label = $"{arrayProp.displayName}: {arrayProp.arraySize}";

            // Create a consistent rect with 4px left padding regardless of nesting level
            // Use the original rect 'r' instead of IndentedRect to avoid cumulative indentation
            Rect headerRect = new Rect(r.x, r.y, r.width, r.height);
            headerRect.x += INDENT_WIDTH;

            GUI.Label(headerRect, GUIContent.none);
            bool lastExpanded = arrayProp.isExpanded;
            if (arrayProp.propertyPath.Contains('.'))
            {
                string[] pathParts = arrayProp.propertyPath.Split('.');
                float indent = EditorGUI.indentLevel * INDENT_WIDTH + INDENT_WIDTH * (pathParts.Length - 1);
                headerRect.x -= indent;
                headerRect.width += indent;
                // Nested array: use a toggle-style foldout to avoid excessive indentation
            }

            arrayProp.isExpanded = EditorGUI.Foldout(headerRect, arrayProp.isExpanded, label, true);
            
            if (lastExpanded != arrayProp.isExpanded)
            {
                var so = arrayProp.serializedObject;
                var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
                _arrayLists.Remove(key);
                InternalEditorUtility.RepaintAllViews();
            }
        }
    }
}