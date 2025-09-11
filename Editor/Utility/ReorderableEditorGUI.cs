
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
                HandleDragAndDrop(arrayProp, rr);
            }
            else
            {
                // Non-layout path when caller already has a rect
                Rect rr = rect;
                rr.height = arrayProp.isExpanded
                    ? reorderableList.GetHeight()
                    : EditorGUIUtility.singleLineHeight + 4f;
                reorderableList.DoList(rr);
                HandleDragAndDrop(arrayProp, rr);
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

            // FIRST: Quick check if this is an ObjectReference array
            bool isObjectReferenceArray = false;
            if (arrayProp.arraySize > 0)
            {
                var firstElement = arrayProp.GetArrayElementAtIndex(0);
                isObjectReferenceArray = (firstElement.propertyType == SerializedPropertyType.ObjectReference);
            }
            else
            {
                // For empty arrays, use heuristic based on property path and field name
                var displayName = arrayProp.displayName.ToLower();
                var propertyPath = arrayProp.propertyPath.ToLower();

                // Common patterns for ObjectReference arrays
                bool likelyObjectReference =
                    displayName.Contains("transform") ||
                    displayName.Contains("gameobject") ||
                    displayName.Contains("component") ||
                    displayName.Contains("behaviour") ||
                    displayName.Contains("renderer") ||
                    displayName.Contains("collider") ||
                    displayName.Contains("rigidbody") ||
                    displayName.Contains("material") ||
                    displayName.Contains("texture") ||
                    displayName.Contains("sprite") ||
                    displayName.Contains("audio") ||
                    displayName.Contains("prefab") ||
                    displayName.Contains("asset");

                // Common patterns for non-ObjectReference arrays  
                bool likelyValueType =
                    displayName.Contains("int") ||
                    displayName.Contains("float") ||
                    displayName.Contains("vector") ||
                    displayName.Contains("string") ||
                    displayName.Contains("bool") ||
                    displayName.Contains("struct") ||
                    displayName.Contains("enum");

                if (likelyValueType)
                {
                    isObjectReferenceArray = false;
                }
                else if (likelyObjectReference)
                {
                    isObjectReferenceArray = true;
                }
                else
                {
                    // Unknown - be conservative and assume not ObjectReference
                    isObjectReferenceArray = false;
                }
            }

            if (!isObjectReferenceArray)
            {
                // Not an ObjectReference array - reject drag but don't consume event
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
                return;
            }

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    var dragged = DragAndDrop.objectReferences;
                    if (dragged == null || dragged.Length == 0) return;

                    // Get element type to check compatibility
                    Type expectedElementType = arrayProp.GetElementType();

                    // Check if any dragged object is compatible
                    bool hasCompatibleObject = false;
                    if (expectedElementType != null)
                    {
                        foreach (var obj in dragged)
                        {
                            if (obj == null) continue;

                            Object targetObject = obj;
                            bool isCompatible = false;

                            // Direct type check first
                            if (expectedElementType.IsAssignableFrom(obj.GetType()))
                            {
                                isCompatible = true;
                            }
                            // GameObject component extraction
                            else if (obj is GameObject go)
                            {
                                if (expectedElementType == typeof(Transform))
                                {
                                    isCompatible = true; // Transform always exists
                                }
                                else if (typeof(Component).IsAssignableFrom(expectedElementType))
                                {
                                    var component = go.GetComponent(expectedElementType);
                                    if (component != null)
                                    {
                                        isCompatible = true;
                                    }
                                }
                                else if (expectedElementType == typeof(GameObject))
                                {
                                    isCompatible = true;
                                }
                                else if (expectedElementType == typeof(UnityEngine.Object))
                                {
                                    isCompatible = true; // Fallback case
                                }
                            }
                            // Special fallback for Object type only if it's actually a Unity Object
                            else if (expectedElementType == typeof(UnityEngine.Object) && obj is UnityEngine.Object)
                            {
                                isCompatible = true;
                            }

                            if (isCompatible)
                            {
                                hasCompatibleObject = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // No exact type info but confirmed ObjectReference - be conservative
                        foreach (var obj in dragged)
                        {
                            if (obj is GameObject || obj is Transform || obj is Component)
                            {
                                hasCompatibleObject = true;
                                break;
                            }
                        }
                    }

                    // Set appropriate visual mode
                    DragAndDrop.visualMode = hasCompatibleObject ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                    if (evt.type == EventType.DragPerform && hasCompatibleObject)
                    {
                        DragAndDrop.AcceptDrag();

                        // Record undo
                        Undo.RecordObject(arrayProp.serializedObject.targetObject, "Drag & Drop to List");

                        HashSet<Object> existing = new HashSet<Object>();
                        for (int i = 0; i < arrayProp.arraySize; i++)
                        {
                            var elem = arrayProp.GetArrayElementAtIndex(i);
                            if (elem.propertyType == SerializedPropertyType.ObjectReference && elem.objectReferenceValue != null)
                                existing.Add(elem.objectReferenceValue);
                        }

                        int addedCount = 0;

                        // Get element type from existing element or field info
                        Type elementType = arrayProp.GetElementType();

                        foreach (var obj in dragged)
                        {
                            if (obj == null) continue;

                            Object targetObject = obj;
                            bool shouldAdd = false;

                            if (elementType != null)
                            {
                                // Direct type check first
                                if (elementType.IsAssignableFrom(obj.GetType()))
                                {
                                    targetObject = obj;
                                    shouldAdd = true;
                                }
                                // GameObject component extraction
                                else if (obj is GameObject go)
                                {
                                    if (elementType == typeof(Transform))
                                    {
                                        targetObject = go.transform;
                                        shouldAdd = true;
                                    }
                                    else if (typeof(Component).IsAssignableFrom(elementType))
                                    {
                                        var component = go.GetComponent(elementType);
                                        if (component != null)
                                        {
                                            targetObject = component;
                                            shouldAdd = true;
                                        }
                                    }
                                    else if (elementType == typeof(GameObject))
                                    {
                                        targetObject = go;
                                        shouldAdd = true;
                                    }
                                    else if (elementType == typeof(UnityEngine.Object))
                                    {
                                        targetObject = go;
                                        shouldAdd = true;
                                    }
                                }
                                // Object fallback
                                else if (elementType == typeof(UnityEngine.Object) && obj is UnityEngine.Object)
                                {
                                    targetObject = obj;
                                    shouldAdd = true;
                                }
                            }
                            else
                            {
                                // No elementType info - only allow direct UnityEngine.Object assignments
                                if (obj is UnityEngine.Object)
                                {
                                    targetObject = obj;
                                    shouldAdd = true;
                                }
                            }

                            if (!shouldAdd || existing.Contains(targetObject)) continue;

                            arrayProp.arraySize++;
                            var newElement = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
                            if (newElement.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                newElement.objectReferenceValue = targetObject;
                                existing.Add(targetObject);
                                addedCount++;
                            }
                        }

                        arrayProp.serializedObject.ApplyModifiedProperties();
                    }

                    // Only use event for DragPerform
                    if (evt.type == EventType.DragPerform)
                    {
                        evt.Use();
                    }
                    break;
            }
        }
    }
}