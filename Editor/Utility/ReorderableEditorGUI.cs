
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorGUI
    {
        private static readonly Dictionary<ListKey, ReorderableList> _arrayLists = new();
        private static readonly Dictionary<ListKey, HashSet<int>> _selectedIndices = new();

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
                    drawHeaderCallback = (Rect r) =>
                    {
                        // Adjust header rect for indent
                        int indentLevel = EditorGUI.indentLevel;
                        float indent = indentLevel * INDENT_WIDTH;
                        headerRect = new Rect(r.x + indent, r.y, r.width - indent, r.height);
                        DrawHeader(arrayProp, headerRect);
                    },

                    drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) =>
                    {
                        if (!arrayProp.isExpanded) return;

                        // Safe dictionary access to prevent KeyNotFoundException
                        if (_selectedIndices.ContainsKey(key) && _selectedIndices[key].Contains(index))
                            EditorGUI.DrawRect(r, new Color(0.3f, 0.5f, 1f, 0.3f));

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

                if (!_selectedIndices.ContainsKey(key))
                {
                    _selectedIndices[key] = new HashSet<int>();
                }
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
                
                // Handle drag BEFORE ReorderableList.DoList to take priority
                Rect expandedDropRect = new Rect(rr.x, rr.y, rr.width, rr.height);
                if (arrayProp.isExpanded)
                {
                    // For expanded lists, extend significantly up to cover header
                    expandedDropRect.y -= EditorGUIUtility.singleLineHeight;
                    expandedDropRect.height += EditorGUIUtility.singleLineHeight + 4f;
                }
                else
                {
                    // For collapsed lists, still extend a bit to cover header
                    expandedDropRect.y -= 4f;
                    expandedDropRect.height += 8f;
                }
                HandleDragAndDrop(arrayProp, expandedDropRect);
                
                reorderableList.DoList(rr);
            }
            else
            {
                // Non-layout path when caller already has a rect
                Rect rr = rect;
                rr.height = arrayProp.isExpanded
                    ? reorderableList.GetHeight()
                    : EditorGUIUtility.singleLineHeight + 4f;
                
                // Handle drag BEFORE ReorderableList.DoList to take priority
                Rect expandedDropRect = new Rect(rr.x, rr.y, rr.width, rr.height);
                if (arrayProp.isExpanded)
                {
                    // For expanded lists, extend significantly up to cover header
                    expandedDropRect.y -= EditorGUIUtility.singleLineHeight;
                    expandedDropRect.height += EditorGUIUtility.singleLineHeight + 4f;
                }
                else
                {
                    // For collapsed lists, still extend a bit to cover header
                    expandedDropRect.y -= 4f;
                    expandedDropRect.height += 8f;
                }
                HandleDragAndDrop(arrayProp, expandedDropRect);
                
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

        private static void HandleDragAndDrop(SerializedProperty arrayProp, Rect dropRect)
        {
            Event evt = Event.current;
            if (evt == null || evt.type == EventType.Used) return;
            
            if (!dropRect.Contains(evt.mousePosition)) return;

            // Early validation - only handle ObjectReference arrays
            if (!IsObjectReferenceArray(arrayProp))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
                return;
            }

            var dragged = DragAndDrop.objectReferences;
            if (dragged == null || dragged.Length == 0) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    HandleDragUpdated(arrayProp, dragged);
                    break;

                case EventType.DragPerform:
                    HandleDragPerform(arrayProp, dragged);
                    evt.Use();
                    break;
            }
        }

        private static bool IsObjectReferenceArray(SerializedProperty arrayProp)
        {
            // Check existing elements first
            if (arrayProp.arraySize > 0)
            {
                var firstElement = arrayProp.GetArrayElementAtIndex(0);
                return firstElement.propertyType == SerializedPropertyType.ObjectReference;
            }

            // For empty arrays, use name-based heuristics
            return IsLikelyObjectReferenceArray(arrayProp.displayName);
        }

        private static bool IsLikelyObjectReferenceArray(string displayName)
        {
            var name = displayName.ToLower();

            // Common ObjectReference patterns
            string[] objectPatterns = {
                "transform", "gameobject", "component", "behaviour", "renderer",
                "collider", "rigidbody", "material", "texture", "sprite",
                "audio", "prefab", "asset"
            };

            // Common value type patterns
            string[] valuePatterns = {
                "int", "float", "vector", "string", "bool", "struct", "enum"
            };

            foreach (var pattern in valuePatterns)
            {
                if (name.Contains(pattern)) return false;
            }

            foreach (var pattern in objectPatterns)
            {
                if (name.Contains(pattern)) return true;
            }

            // Conservative default - assume value type
            return false;
        }

        private static void HandleDragUpdated(SerializedProperty arrayProp, Object[] dragged)
        {
            var elementType = arrayProp.GetElementType();
            bool hasCompatibleObject = HasCompatibleObjects(dragged, elementType);
            DragAndDrop.visualMode = hasCompatibleObject ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        }

        private static void HandleDragPerform(SerializedProperty arrayProp, Object[] dragged)
        {
            var elementType = arrayProp.GetElementType();
            if (!HasCompatibleObjects(dragged, elementType)) return;

            DragAndDrop.AcceptDrag();
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Drag & Drop to List");

            AddCompatibleObjects(arrayProp, dragged, elementType);

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        private static bool HasCompatibleObjects(Object[] objects, Type elementType)
        {
            if (elementType == null)
            {
                // Fallback: accept common Unity objects
                foreach (var obj in objects)
                {
                    if (obj is GameObject || obj is Transform || obj is Component)
                        return true;
                }
                return false;
            }

            foreach (var obj in objects)
            {
                if (IsCompatibleObject(obj, elementType))
                    return true;
            }
            return false;
        }

        private static bool IsCompatibleObject(Object obj, Type elementType)
        {
            if (obj == null) return false;

            // Direct type compatibility
            if (elementType.IsAssignableFrom(obj.GetType()))
                return true;

            // Smart conversions
            if (elementType == typeof(Sprite) && obj is Texture2D)
                return true;

            // GameObject component extraction
            if (obj is GameObject go)
            {
                return CanExtractFromGameObject(go, elementType);
            }

            // Fallback for Object type
            return elementType == typeof(Object) && obj is Object;
        }

        private static bool CanExtractFromGameObject(GameObject go, Type elementType)
        {
            if (elementType == typeof(Transform)) return true;
            if (elementType == typeof(GameObject)) return true;
            if (elementType == typeof(Object)) return true;
            if (typeof(Component).IsAssignableFrom(elementType))
            {
                return go.GetComponent(elementType) != null;
            }
            return false;
        }

        private static void AddCompatibleObjects(SerializedProperty arrayProp, Object[] objects, Type elementType)
        {
            foreach (var obj in objects)
            {
                var targetObject = ExtractTargetObject(obj, elementType);
                if (targetObject != null)
                {
                    AddObjectToArray(arrayProp, targetObject);
                }
            }
        }

        private static Object ExtractTargetObject(Object obj, Type elementType)
        {
            if (obj == null) return null;

            // Direct assignment
            if (elementType == null || elementType.IsAssignableFrom(obj.GetType()))
                return obj;

            // SMART CONVERSIONS
            // Texture2D → Sprite conversion
            if (elementType == typeof(Sprite) && obj is Texture2D texture)
            {
                // Get all sprites that use this texture
                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
                    Sprite foundSprite = null;

                    foreach (var asset in allAssets)
                    {
                        if (asset is Sprite sprite)
                        {
                            foundSprite = sprite;
                            break;
                        }
                    }

                    if (foundSprite != null)
                        return foundSprite;
                }

                return null;
            }

            // GameObject component extraction
            if (obj is GameObject go)
            {
                if (elementType == typeof(Transform)) return go.transform;
                if (elementType == typeof(GameObject)) return go;
                if (elementType == typeof(Object)) return go;
                if (typeof(Component).IsAssignableFrom(elementType))
                {
                    return go.GetComponent(elementType);
                }
            }

            // Object fallback
            if (elementType == typeof(Object) && obj is Object)
                return obj;

            return null;
        }

        private static void AddObjectToArray(SerializedProperty arrayProp, Object targetObject)
        {
            arrayProp.arraySize++;
            var newElement = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);

            if (newElement.propertyType == SerializedPropertyType.ObjectReference)
            {
                newElement.objectReferenceValue = targetObject;
            }
        }
    }
}