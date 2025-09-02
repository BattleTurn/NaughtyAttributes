
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
                    drawHeaderCallback = (Rect r) =>
                    {
                        DrawHeader(arrayProp, r);
                    },

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
                };

                reorderableList.drawNoneElementCallback = (Rect rr) =>
                {
                    if (!arrayProp.isExpanded) return;
                    ReorderableList.defaultBehaviours.DrawNoneElement(rr, reorderableList.draggable);
                };

                reorderableList.drawFooterCallback = (Rect fr) =>
                {
                    if (!arrayProp.isExpanded) return;
                    ReorderableList.defaultBehaviours.DrawFooter(fr, reorderableList);
                };

                _arrayLists[key] = reorderableList;
            }

            reorderableList = _arrayLists[key];
            reorderableList.draggable = arrayProp.isExpanded;

            float listHeight = reorderableList.GetHeight();
            rect = EditorGUILayout.GetControlRect(false, listHeight);
            rect = EditorGUI.IndentedRect(rect);

            if (rect == default)
            {
                reorderableList.DoLayoutList();
            }
            else
            {
                reorderableList.DoList(rect);
            }
        }

        public static void ClearCache()
        {
            _arrayLists.Clear();
        }

        private static void DrawHeader(SerializedProperty arrayProp, Rect r)
        {
            Rect rect = EditorGUILayout.GetControlRect(true, r.height);
            // Toggle foldout on the serialized array itself
            Rect foldRect = new Rect(rect.x + 16, r.y, r.width, r.height);

            bool lastExpanded = arrayProp.isExpanded;
            arrayProp.isExpanded = EditorGUI.Foldout(foldRect, arrayProp.isExpanded, GUIContent.none, true);

            if (lastExpanded != arrayProp.isExpanded)
            {
                InternalEditorUtility.RepaintAllViews();
            }

            Rect labelRect = new Rect(foldRect.x, r.y, r.width, r.height);
            EditorGUI.LabelField(labelRect, $"{arrayProp.displayName}: {arrayProp.arraySize}");

        }
    }
}