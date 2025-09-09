
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorGUI
    {
        private static readonly Dictionary<ListKey, ReorderableList> _arrayLists = new Dictionary<ListKey, ReorderableList>();

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

            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            if (!_arrayLists.ContainsKey(key))
            {
                reorderableList = new ReorderableList(so, arrayProp, true, true, true, true)
                {
                    drawHeaderCallback = (Rect r) => { DrawHeader(arrayProp, r); },

                    drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) =>
                    {
                        if (!arrayProp.isExpanded) return;
                        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
                        r.y += 1.0f;
                        r.x += 10.0f;
                        r.width -= 10.0f;

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
                        ReorderableList.defaultBehaviours.DrawNoneElement(rr, reorderableList.draggable);
                    },
                    drawFooterCallback = (Rect fr) =>
                    {
                        if (!arrayProp.isExpanded) return;
                        ReorderableList.defaultBehaviours.DrawFooter(fr, reorderableList);
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
            // Use the rect provided by ReorderableList (already aligned with current indent)
            // Slightly smaller inset for nested properties to pull header a bit outward
            bool isNested = arrayProp.depth > 0;
            float arrowInset = isNested ? -(4f * arrayProp.depth) : 11f;
            float arrowSize = EditorGUIUtility.singleLineHeight;
            float arrowY = r.y + (r.height - arrowSize) * 0.5f;
            Rect foldRect = new Rect(r.x + arrowInset, arrowY, 14f, r.height);

            bool lastExpanded = arrayProp.isExpanded;
            arrayProp.isExpanded = EditorGUI.Foldout(foldRect, arrayProp.isExpanded, GUIContent.none, true);

            if (lastExpanded != arrayProp.isExpanded)
            {
                // Clear cache for this list so height/layout is recalculated immediately
                var so = arrayProp.serializedObject;
                var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
                _arrayLists.Remove(key);
                InternalEditorUtility.RepaintAllViews();
            }

            // Label sits just right of the arrow
            const float labelGap = 6f;
            float labelX = foldRect.xMax + labelGap;
            Rect labelRect = new Rect(labelX, r.y, r.width - (labelX - r.x), r.height);
            EditorGUI.LabelField(labelRect, $"{arrayProp.displayName}: {arrayProp.arraySize}");

        }
    }
}