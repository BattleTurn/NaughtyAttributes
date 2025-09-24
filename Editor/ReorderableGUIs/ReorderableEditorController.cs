using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

using Object = UnityEngine.Object;
using System.Linq;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorController
    {
        private static readonly ReorderableEditorGUI _reorderableEditorGUI = new();

        #region Readonly Static Fields
        private readonly static Dictionary<ListKey, ReorderableList> _arrayLists = new();
        private readonly static Dictionary<ListKey, HashSet<int>> _selectedIndices = new();
        private readonly static Dictionary<ListKey, Dictionary<int, Vector2>> _mouseDownPositions = new();
        #endregion

        #region  Static Fields
        // Custom drag state
        private static ListKey? _dragKey = null;
        private static HashSet<int> _dragIndices = null;
        private static bool _isDragging = false;
        private static int _dragInsertIndex = -1;
        #endregion

        #region Public API
        /// <summary>
        /// Creates and renders a ReorderableList with enhanced drag & drop capabilities
        /// </summary>
        public static void CreateReorderableList(Rect rect, SerializedProperty arrayProp)
        {
            var reorderableList = GetOrCreateReorderableList(arrayProp);
            var listRect = _reorderableEditorGUI.CalculateListRect(rect, arrayProp, reorderableList);

            // Handle drag & drop BEFORE rendering to take event priority
            var dropRect = _reorderableEditorGUI.CalculateExpandedDropRect(listRect, arrayProp);
            HandleDragAndDrop(arrayProp, dropRect);

            reorderableList.DoList(listRect);
        }

        /// <summary>
        /// Clears all cached ReorderableList instances
        /// </summary>
        public static void ClearCache()
        {
            _arrayLists.Clear();
            _selectedIndices.Clear();
        }
        #endregion

        #region Internal Input Event Methods
        internal static bool HasAnyKeyboardShortcutPressed(ListKey key, Event currentEvent)
        {
            // Handle keyboard shortcuts for selected elements
            if (currentEvent.type == EventType.KeyDown && _selectedIndices.ContainsKey(key) && _selectedIndices[key].Count > 0)
            {
                if (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace)
                {
                    DeleteSelectedElements(key, _selectedIndices[key]);
                    currentEvent.Use();
                    return false;
                }
                else if (currentEvent.keyCode == KeyCode.D && (currentEvent.control || currentEvent.command))
                {
                    DuplicateSelectedElements(key, _selectedIndices[key]);
                    currentEvent.Use();
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Internal Drag & Drop System
        internal static void HandleCustomDragAndDrop(ListKey key, int index, Rect r, Event currentEvent, SerializedProperty arrayProp)
        {
            // Calculate insertion index during drag if mouse is over this element
            if (_isDragging && _dragKey?.Equals(key) == true && r.Contains(currentEvent.mousePosition))
            {
                // Check if mouse is in top half or bottom half of element
                float elementCenter = r.y + r.height * 0.5f;
                if (currentEvent.mousePosition.y < elementCenter)
                {
                    _dragInsertIndex = index; // Insert before this element
                }
                else
                {
                    _dragInsertIndex = index + 1; // Insert after this element
                }
            }

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && r.Contains(currentEvent.mousePosition))
                    {
                        // Store mouse down position but don't start drag yet
                        if (!_mouseDownPositions.ContainsKey(key))
                            _mouseDownPositions[key] = new Dictionary<int, Vector2>();
                        _mouseDownPositions[key][index] = currentEvent.mousePosition;

                        // Handle selection
                        HandleElementSelection(key, index, currentEvent);

                        // Only Use() event for multi-selection operations (Ctrl/Shift)
                        // Let normal clicks through for PropertyField interaction
                        if (currentEvent.control || currentEvent.command || currentEvent.shift)
                        {
                            currentEvent.Use();
                        }
                    }
                    break;

                case EventType.MouseDrag:
                    // Only start drag if mouse moved enough and we have a mouse down position
                    if (!_isDragging && _mouseDownPositions.ContainsKey(key) && _mouseDownPositions[key].ContainsKey(index))
                    {
                        Vector2 mouseDownPos = _mouseDownPositions[key][index];
                        float dragDistance = Vector2.Distance(mouseDownPos, currentEvent.mousePosition);

                        // Start drag only if mouse moved more than threshold
                        if (dragDistance > 5f) // 5 pixel threshold
                        {
                            HandleDragStart(key, index, currentEvent, arrayProp);
                        }
                    }

                    if (_isDragging && _dragKey?.Equals(key) == true)
                    {
                        HandleDragUpdate(key, currentEvent, arrayProp);
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDragging && _dragKey?.Equals(key) == true)
                    {
                        HandleDragEnd(key, currentEvent, arrayProp);
                        currentEvent.Use();
                    }
                    else if (_mouseDownPositions.ContainsKey(key) && _mouseDownPositions[key].ContainsKey(index))
                    {
                        // This was a click without drag - allow normal property interaction
                        _mouseDownPositions[key].Remove(index);
                    }
                    break;

                case EventType.Repaint:
                    // Draw insertion line during drag
                    if (_isDragging && _dragKey?.Equals(key) == true && _dragInsertIndex >= 0)
                    {
                        _reorderableEditorGUI.DrawInsertionLine(_dragInsertIndex, r, index);
                    }
                    break;
            }
        }
        #endregion

        #region Internal Element Methods
        internal static void DeleteSelectedElements(ListKey key, HashSet<int> selectedIndices)
        {
            var arrayProp = FindArrayPropertyByKey(key);
            if (arrayProp == null) return;

            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Delete Selected Elements");

            // Sort indices in descending order to maintain correct indices when deleting
            var sortedIndices = selectedIndices.OrderByDescending(i => i).ToList();

            foreach (int index in sortedIndices)
            {
                if (index >= 0 && index < arrayProp.arraySize)
                {
                    arrayProp.DeleteArrayElementAtIndex(index);
                }
            }

            // Clear selection after deletion
            _selectedIndices[key].Clear();

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        internal static void DuplicateSelectedElements(ListKey key, HashSet<int> selectedIndices)
        {
            var arrayProp = FindArrayPropertyByKey(key);
            if (arrayProp == null) return;

            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Duplicate Selected Elements");

            // Sort indices to process from end to beginning to maintain indices
            var sortedIndices = selectedIndices.OrderByDescending(i => i).ToList();
            HashSet<int> newSelection = new HashSet<int>();

            foreach (int index in sortedIndices)
            {
                if (index >= 0 && index < arrayProp.arraySize)
                {
                    // Insert new element after current one
                    arrayProp.InsertArrayElementAtIndex(index);

                    // The new element will be at index + 1, add to new selection
                    newSelection.Add(index + 1);
                }
            }

            // Update selection to new duplicated elements
            _selectedIndices[key] = newSelection;

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        internal static void ClearArray(SerializedProperty arrayProp)
        {
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Clear Array");
            arrayProp.ClearArray();
            arrayProp.serializedObject.ApplyModifiedProperties();
        }
        
        
        internal static void RemoveDuplicates(SerializedProperty arrayProp)
        {
            if (!IsObjectReferenceArray(arrayProp)) return;

            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Remove Duplicates");

            HashSet<Object> seenObjects = new HashSet<Object>();
            List<int> indicesToRemove = new List<int>();

            // Collect indices of duplicate items
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var obj = element.objectReferenceValue;

                if (obj == null || seenObjects.Contains(obj))
                {
                    indicesToRemove.Add(i);
                }
                else
                {
                    seenObjects.Add(obj);
                }
            }

            // Remove duplicates in reverse order to maintain indices
            for (int i = indicesToRemove.Count - 1; i >= 0; i--)
            {
                arrayProp.DeleteArrayElementAtIndex(indicesToRemove[i]);
            }

            arrayProp.serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #region ReorderableList Creation & Management    
        private static ReorderableList GetOrCreateReorderableList(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);

            if (!_arrayLists.ContainsKey(key))
            {
                var reorderableList = _reorderableEditorGUI.CreateNewReorderableList(_selectedIndices, arrayProp, key);
                _arrayLists[key] = reorderableList;

                if (!_selectedIndices.ContainsKey(key))
                {
                    _selectedIndices[key] = new HashSet<int>();
                }
            }

            var list = _arrayLists[key];
            list.draggable = arrayProp.isExpanded;
            return list;
        }

        #endregion

        #region Drag & Drop System
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
        #endregion

        #region Object Processing & Conversion
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
                return ConvertTextureToSprite(texture);
            }

            // GameObject component extraction
            if (obj is GameObject go)
            {
                return ExtractComponentFromGameObject(go, elementType);
            }

            // Object fallback
            if (elementType == typeof(Object) && obj is Object)
                return obj;

            return null;
        }

        private static Sprite ConvertTextureToSprite(Texture2D texture)
        {
            // Get all sprites that use this texture
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(texturePath))
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
                
                foreach (var asset in allAssets)
                {
                    if (asset is Sprite sprite)
                    {
                        return sprite;
                    }
                }
            }

            return null;
        }

        private static Object ExtractComponentFromGameObject(GameObject go, Type elementType)
        {
            if (elementType == typeof(Transform)) return go.transform;
            if (elementType == typeof(GameObject)) return go;
            if (elementType == typeof(Object)) return go;
            if (typeof(Component).IsAssignableFrom(elementType))
            {
                return go.GetComponent(elementType);
            }
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
        #endregion

        #region Internal API Type Dectection & Validation
        internal static bool IsObjectReferenceArray(SerializedProperty arrayProp)
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

        internal static void InvalidateListCache(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            _arrayLists.Remove(key);
            _selectedIndices.Remove(key);
            _mouseDownPositions.Remove(key);
            InternalEditorUtility.RepaintAllViews();
        }

        internal static bool IsSmartSelection(ListKey key, int index)
        {
            // Check if this selection was created by smart grouping
            // vs manual Ctrl+click selection
            if (!_selectedIndices.ContainsKey(key))
                return false;
                
            var selection = _selectedIndices[key];
            if (selection.Count <= 1)
                return false;
                
            // Smart selections typically have consecutive indices
            var sortedIndices = selection.OrderBy(i => i).ToArray();
            
            // Check if this index is part of a consecutive group
            for (int i = 0; i < sortedIndices.Length - 1; i++)
            {
                if (sortedIndices[i + 1] - sortedIndices[i] != 1)
                {
                    // Found a gap, check if current index is in consecutive part
                    if (index >= sortedIndices[0] && index <= sortedIndices[i])
                        return true; // In first consecutive group
                    break;
                }
            }
            
            // If all indices are consecutive, it's likely a smart selection
            return sortedIndices[sortedIndices.Length - 1] - sortedIndices[0] == sortedIndices.Length - 1;
        }
        #endregion

        #region Type Detection & Validation
        private static bool IsLikelyObjectReferenceArray(string displayName)
        {
            var name = displayName.ToLower();

            // Common ObjectReference patterns
            string[] objectPatterns = {
                "transform", "gameobject", "component", "behaviour", "renderer",
                "collider", "rigidbody", "material", "texture", "sprite",
                "audio", "prefab", "asset", "scriptable", "scriptableobject"
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

        private static bool HasCompatibleObjects(Object[] objects, Type elementType)
        {
            if (elementType == null)
            {
                // Fallback: accept common Unity objects including ScriptableObject
                foreach (var obj in objects)
                {
                    if (obj is GameObject || obj is Transform || obj is Component || obj is ScriptableObject)
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
        #endregion

        #region Selection Utility Methods
        private static void HandleDragStart(ListKey key, int index, Event currentEvent, SerializedProperty arrayProp)
        {
            // Handle selection first
            HandleElementSelection(key, index, currentEvent);
            
            // Ensure this element is selected for drag
            if (!_selectedIndices.ContainsKey(key))
                _selectedIndices[key] = new HashSet<int>();
                
            var selection = _selectedIndices[key];
            
            // If element not selected, try smart selection for normal click
            if (!selection.Contains(index) && !currentEvent.control && !currentEvent.command)
            {
                var smartSelection = GetOrCreateSmartSelection(key, index, arrayProp.arraySize);
                if (smartSelection.Count > 1)
                {
                    _selectedIndices[key] = smartSelection;
                }
                else
                {
                    selection.Clear();
                    selection.Add(index);
                }
            }
            
            // Start drag with current selection
            _dragKey = key;
            _dragIndices = new HashSet<int>(_selectedIndices[key]);
            _isDragging = true;
            _dragInsertIndex = -1;
        }

        private static void HandleDragUpdate(ListKey key, Event currentEvent, SerializedProperty arrayProp)
        {
            // Store current mouse position for later calculation
            // We'll calculate the actual insertion index during repaint when we have all element rects
            _dragInsertIndex = -1;
            
            // Request repaint to update insertion line
            GUI.changed = true;
        }

        private static void HandleDragEnd(ListKey key, Event currentEvent, SerializedProperty arrayProp)
        {
            // If no specific insertion index was calculated, try to determine from mouse position
            if (_dragInsertIndex < 0)
            {
                // Find closest element based on mouse Y position
                float mouseY = currentEvent.mousePosition.y;
                int closestIndex = Mathf.RoundToInt(mouseY / EditorGUIUtility.singleLineHeight);
                _dragInsertIndex = Mathf.Clamp(closestIndex, 0, arrayProp.arraySize);
            }
            
            if (_dragInsertIndex >= 0 && _dragIndices != null && _dragIndices.Count > 0)
            {
                // Only perform reorder if insertion index is different from current positions
                var sortedDragIndices = _dragIndices.OrderBy(i => i).ToList();
                bool needsReorder = _dragInsertIndex < sortedDragIndices[0] || 
                                   _dragInsertIndex > sortedDragIndices[sortedDragIndices.Count - 1] + 1;
                
                if (needsReorder)
                {
                    PerformCustomReorder(arrayProp, _dragIndices, _dragInsertIndex);
                }
            }
            
            // Reset drag state
            _dragKey = null;
            _dragIndices = null;
            _isDragging = false;
            _dragInsertIndex = -1;
        }

        private static void PerformCustomReorder(SerializedProperty arrayProp, HashSet<int> dragIndices, int insertIndex)
        {
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Reorder Multiple Elements");
            
            // Convert to sorted list for consistent ordering
            var sortedIndices = dragIndices.OrderBy(i => i).ToList();
            
            // Store data of dragged elements
            var draggedData = new List<object>();
            foreach (int idx in sortedIndices)
            {
                if (idx >= 0 && idx < arrayProp.arraySize)
                {
                    var element = arrayProp.GetArrayElementAtIndex(idx);
                    draggedData.Add(GetElementData(element));
                }
            }
            
            // Remove dragged elements (in reverse order to maintain indices)
            var reversedIndices = sortedIndices.OrderByDescending(i => i).ToList();
            foreach (int idx in reversedIndices)
            {
                if (idx >= 0 && idx < arrayProp.arraySize)
                {
                    arrayProp.DeleteArrayElementAtIndex(idx);
                }
            }
            
            // Adjust insert index if needed
            int finalInsertIndex = insertIndex;
            foreach (int removedIndex in sortedIndices)
            {
                if (removedIndex < insertIndex)
                    finalInsertIndex--;
            }
            
            // Insert elements at new position
            for (int i = 0; i < draggedData.Count; i++)
            {
                int insertAt = Mathf.Max(0, finalInsertIndex + i);
                if (insertAt <= arrayProp.arraySize)
                {
                    arrayProp.InsertArrayElementAtIndex(insertAt);
                    var newElement = arrayProp.GetArrayElementAtIndex(insertAt);
                    SetElementData(newElement, draggedData[i]);
                }
            }
            
            // Update selection to new positions
            var newSelection = new HashSet<int>();
            for (int i = 0; i < draggedData.Count; i++)
            {
                newSelection.Add(finalInsertIndex + i);
            }
            
            var key = new ListKey(arrayProp.serializedObject.targetObject.GetInstanceID(), arrayProp.propertyPath);
            _selectedIndices[key] = newSelection;
            
            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        private static void HandleElementSelection(ListKey key, int index, Event currentEvent)
        {
            if (!_selectedIndices.ContainsKey(key))
            {
                _selectedIndices[key] = new HashSet<int>();
            }

            var selectedSet = _selectedIndices[key];

            if (currentEvent.button == 0) // Left click
            {
                if (currentEvent.control || currentEvent.command) // Ctrl/Cmd + Click
                {
                    // Toggle selection
                    if (selectedSet.Contains(index))
                        selectedSet.Remove(index);
                    else
                        selectedSet.Add(index);
                }
                else if (currentEvent.shift) // Shift + Click
                {
                    // Range selection
                    if (selectedSet.Count > 0)
                    {
                        int lastSelected = selectedSet.Max();
                        int start = Mathf.Min(lastSelected, index);
                        int end = Mathf.Max(lastSelected, index);
                        
                        for (int i = start; i <= end; i++)
                        {
                            selectedSet.Add(i);
                        }
                    }
                    else
                    {
                        selectedSet.Add(index);
                    }
                }
                else // Normal click
                {
                    // Simple single selection (smart selection will be handled in mouse up)
                    selectedSet.Clear();
                    selectedSet.Add(index);
                }
            }
            else if (currentEvent.button == 1) // Right click
            {
                // If right-clicking on unselected item, select it first
                if (!selectedSet.Contains(index))
                {
                    selectedSet.Clear();
                    selectedSet.Add(index);
                }
                
                // Show context menu for selected elements
                _reorderableEditorGUI.ShowElementContextMenu(key, selectedSet);
            }
        }

        private static HashSet<int> GetOrCreateSmartSelection(ListKey key, int draggedIndex, int arraySize)
        {
            // Check if we already have a manual selection that includes the dragged element
            if (_selectedIndices.ContainsKey(key) && _selectedIndices[key].Contains(draggedIndex))
            {
                return _selectedIndices[key];
            }

            // Create smart selection: find adjacent elements of the same type
            var smartSelection = new HashSet<int>();
            var arrayProp = FindArrayPropertyByKey(key);
            if (arrayProp == null || draggedIndex < 0 || draggedIndex >= arraySize)
            {
                smartSelection.Add(draggedIndex);
                return smartSelection;
            }

            // Get the type/value of the dragged element
            var draggedElement = arrayProp.GetArrayElementAtIndex(draggedIndex);
            var draggedType = GetElementTypeInfo(draggedElement);

            // Start with the dragged element
            smartSelection.Add(draggedIndex);

            // Look backwards for similar elements
            for (int i = draggedIndex - 1; i >= 0; i--)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (IsSimilarElement(draggedElement, element, draggedType))
                {
                    smartSelection.Add(i);
                }
                else
                {
                    break; // Stop at first different element
                }
            }

            // Look forwards for similar elements
            for (int i = draggedIndex + 1; i < arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (IsSimilarElement(draggedElement, element, draggedType))
                {
                    smartSelection.Add(i);
                }
                else
                {
                    break; // Stop at first different element
                }
            }

            // Update the selection cache
            _selectedIndices[key] = smartSelection;
            
            return smartSelection;
        }

        private static string GetElementTypeInfo(SerializedProperty element)
        {
            // Return a type signature for comparison
            switch (element.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    var obj = element.objectReferenceValue;
                    if (obj != null)
                        return obj.GetType().Name;
                    return "null_object";
                
                case SerializedPropertyType.Enum:
                    return $"enum_{element.enumNames?.Length ?? 0}";
                
                default:
                    return element.propertyType.ToString();
            }
        }

        private static bool IsSimilarElement(SerializedProperty reference, SerializedProperty other, string referenceType)
        {
            // Check if elements are of similar type and potentially groupable
            var otherType = GetElementTypeInfo(other);
            
            // Same basic type
            if (referenceType != otherType)
                return false;

            // For object references, check for same type or both null
            if (reference.propertyType == SerializedPropertyType.ObjectReference)
            {
                var refObj = reference.objectReferenceValue;
                var otherObj = other.objectReferenceValue;
                
                // Both null - similar
                if (refObj == null && otherObj == null)
                    return true;
                
                // One null, one not - different
                if (refObj == null || otherObj == null)
                    return false;
                
                // Same type - similar
                return refObj.GetType() == otherObj.GetType();
            }

            // For primitives, same property type means similar
            return true;
        }

        private static object GetElementData(SerializedProperty element)
        {
            // Store element data based on property type
            switch (element.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return element.intValue;
                case SerializedPropertyType.Float:
                    return element.floatValue;
                case SerializedPropertyType.String:
                    return element.stringValue;
                case SerializedPropertyType.Boolean:
                    return element.boolValue;
                case SerializedPropertyType.ObjectReference:
                    return element.objectReferenceValue;
                case SerializedPropertyType.Vector2:
                    return element.vector2Value;
                case SerializedPropertyType.Vector3:
                    return element.vector3Value;
                case SerializedPropertyType.Vector4:
                    return element.vector4Value;
                case SerializedPropertyType.Vector2Int:
                    return element.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return element.vector3IntValue;
                case SerializedPropertyType.Enum:
                    return element.enumValueIndex;
                case SerializedPropertyType.Color:
                    return element.colorValue;
                case SerializedPropertyType.Rect:
                    return element.rectValue;
                case SerializedPropertyType.Bounds:
                    return element.boundsValue;
                default:
                    // For complex types, we'll copy the entire property
                    return null; // Will handle differently
            }
        }

        private static void SetElementData(SerializedProperty element, object data)
        {
            if (data == null) return;

            switch (element.propertyType)
            {
                case SerializedPropertyType.Integer:
                    element.intValue = (int)data;
                    break;
                case SerializedPropertyType.Float:
                    element.floatValue = (float)data;
                    break;
                case SerializedPropertyType.String:
                    element.stringValue = (string)data;
                    break;
                case SerializedPropertyType.Boolean:
                    element.boolValue = (bool)data;
                    break;
                case SerializedPropertyType.ObjectReference:
                    element.objectReferenceValue = (Object)data;
                    break;
                case SerializedPropertyType.Vector2:
                    element.vector2Value = (Vector2)data;
                    break;
                case SerializedPropertyType.Vector3:
                    element.vector3Value = (Vector3)data;
                    break;
                case SerializedPropertyType.Vector4:
                    element.vector4Value = (Vector4)data;
                    break;
                case SerializedPropertyType.Vector2Int:
                    element.vector2IntValue = (Vector2Int)data;
                    break;
                case SerializedPropertyType.Vector3Int:
                    element.vector3IntValue = (Vector3Int)data;
                    break;
                case SerializedPropertyType.Enum:
                    element.enumValueIndex = (int)data;
                    break;
                case SerializedPropertyType.Color:
                    element.colorValue = (Color)data;
                    break;
                case SerializedPropertyType.Rect:
                    element.rectValue = (Rect)data;
                    break;
                case SerializedPropertyType.Bounds:
                    element.boundsValue = (Bounds)data;
                    break;
            }
        }
        #endregion

        #region Array Utility Methods
        public static bool HasNullReferences(SerializedProperty arrayProp)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null)
                    return true;
            }
            return false;
        }

        public static void RemoveNullReferences(SerializedProperty arrayProp)
        {
            if (!IsObjectReferenceArray(arrayProp)) return;

            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Remove Null References");

            List<int> indicesToRemove = new List<int>();

            // Collect indices of null references
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null)
                {
                    indicesToRemove.Add(i);
                }
            }

            // Remove nulls in reverse order to maintain indices
            for (int i = indicesToRemove.Count - 1; i >= 0; i--)
            {
                arrayProp.DeleteArrayElementAtIndex(indicesToRemove[i]);
            }

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        public static void ReverseArray(SerializedProperty arrayProp)
        {
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Reverse Array");

            int size = arrayProp.arraySize;

            // For ObjectReference arrays
            if (IsObjectReferenceArray(arrayProp))
            {
                List<Object> objects = new List<Object>();

                // Collect all objects
                for (int i = 0; i < size; i++)
                {
                    var element = arrayProp.GetArrayElementAtIndex(i);
                    objects.Add(element.objectReferenceValue);
                }

                // Reverse and reassign
                objects.Reverse();
                for (int i = 0; i < size; i++)
                {
                    var element = arrayProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = objects[i];
                }
            }
            else
            {
                // For other types, use Unity's built-in move capability
                for (int i = 0; i < size / 2; i++)
                {
                    arrayProp.MoveArrayElement(i, size - 1 - i);
                    arrayProp.MoveArrayElement(size - 2 - i, i);
                }
            }

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        public static bool CanSortArray(SerializedProperty arrayProp)
        {
            // Check if array has elements
            if (arrayProp.arraySize == 0) return false;

            // Check first element to determine sortability
            var firstElement = arrayProp.GetArrayElementAtIndex(0);

            // ObjectReference arrays - sortable by name
            if (firstElement.propertyType == SerializedPropertyType.ObjectReference)
                return true;

            // Numeric and comparable value types
            return IsNumericOrComparableType(firstElement.propertyType);
        }


        public static void SortArray(SerializedProperty arrayProp)
        {
            if (arrayProp.arraySize <= 1) return;

            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Sort Array");

            var firstElement = arrayProp.GetArrayElementAtIndex(0);

            // ObjectReference arrays - sort by name
            if (firstElement.propertyType == SerializedPropertyType.ObjectReference)
            {
                SortObjectReferenceArray(arrayProp);
            }
            // Value type arrays - sort by value
            else
            {
                SortValueTypeArray(arrayProp, firstElement.propertyType);
            }

            arrayProp.serializedObject.ApplyModifiedProperties();
        }
        
        
        public static string GetSortMenuText(SerializedProperty arrayProp)
        {
            if (arrayProp.arraySize == 0) return "Sort";
            
            var firstElement = arrayProp.GetArrayElementAtIndex(0);
            
            // ObjectReference arrays - sort by name
            if (firstElement.propertyType == SerializedPropertyType.ObjectReference)
                return "Sort by Name";
                
            // String arrays - sort alphabetically  
            if (firstElement.propertyType == SerializedPropertyType.String)
                return "Sort Alphabetically";
                
            // Numeric types - sort by value
            switch (firstElement.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                    return "Sort by Value";
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                    return "Sort by Magnitude";
                default:
                    return "Sort";
            }
        }
        
        private static SerializedProperty FindArrayPropertyByKey(ListKey key)
        {
            // Try to find the array property using the cached ReorderableList
            if (_arrayLists.ContainsKey(key))
            {
                var reorderableList = _arrayLists[key];
                return reorderableList.serializedProperty;
            }
            return null;
        }

        private static bool IsNumericOrComparableType(SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                    return true;
                default:
                    return false;
            }
        }

        private static void SortObjectReferenceArray(SerializedProperty arrayProp)
        {
            // Collect all objects with their names
            List<(Object obj, string name)> objectsWithNames = new List<(Object, string)>();

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var obj = element.objectReferenceValue;
                string name = obj != null ? obj.name : "";
                objectsWithNames.Add((obj, name));
            }

            // Sort by name
            objectsWithNames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            // Apply sorted order back to array
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                element.objectReferenceValue = objectsWithNames[i].obj;
            }
        }

        private static void SortValueTypeArray(SerializedProperty arrayProp, SerializedPropertyType propertyType)
        {
            // Collect all values from the array
            List<object> values = new List<object>();

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                values.Add(GetPropertyValue(element, propertyType));
            }

            // Sort the values
            values.Sort((a, b) => 
            {
                var comparableA = GetComparableFromValue(a, propertyType);
                var comparableB = GetComparableFromValue(b, propertyType);
                
                if (comparableA == null && comparableB == null) return 0;
                if (comparableA == null) return -1;
                if (comparableB == null) return 1;
                return comparableA.CompareTo(comparableB);
            });

            // Apply the sorted values back to the array
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                SetPropertyValue(element, values[i], propertyType);
            }
        }

        private static object GetPropertyValue(SerializedProperty element, SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                    return element.intValue;
                case SerializedPropertyType.Float:
                    return element.floatValue;
                case SerializedPropertyType.String:
                    return element.stringValue;
                case SerializedPropertyType.Boolean:
                    return element.boolValue;
                case SerializedPropertyType.Enum:
                    return element.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return element.vector2Value;
                case SerializedPropertyType.Vector3:
                    return element.vector3Value;
                case SerializedPropertyType.Vector4:
                    return element.vector4Value;
                case SerializedPropertyType.Vector2Int:
                    return element.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return element.vector3IntValue;
                default:
                    return null;
            }
        }

        private static void SetPropertyValue(SerializedProperty element, object value, SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                    element.intValue = (int)value;
                    break;
                case SerializedPropertyType.Float:
                    element.floatValue = (float)value;
                    break;
                case SerializedPropertyType.String:
                    element.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Boolean:
                    element.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Enum:
                    element.enumValueIndex = (int)value;
                    break;
                case SerializedPropertyType.Vector2:
                    element.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    element.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.Vector4:
                    element.vector4Value = (Vector4)value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    element.vector2IntValue = (Vector2Int)value;
                    break;
                case SerializedPropertyType.Vector3Int:
                    element.vector3IntValue = (Vector3Int)value;
                    break;
            }
        }

        private static IComparable GetComparableFromValue(object value, SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                    return (int)value;
                case SerializedPropertyType.Float:
                    return (float)value;
                case SerializedPropertyType.String:
                    return (string)value ?? "";
                case SerializedPropertyType.Boolean:
                    return (bool)value;
                case SerializedPropertyType.Enum:
                    return (int)value;
                case SerializedPropertyType.Vector2:
                    return ((Vector2)value).magnitude;
                case SerializedPropertyType.Vector3:
                    return ((Vector3)value).magnitude;
                case SerializedPropertyType.Vector4:
                    return ((Vector4)value).magnitude;
                case SerializedPropertyType.Vector2Int:
                    return ((Vector2Int)value).magnitude;
                case SerializedPropertyType.Vector3Int:
                    return ((Vector3Int)value).magnitude;
                default:
                    return 0f;
            }
        }
        #endregion
    }
}