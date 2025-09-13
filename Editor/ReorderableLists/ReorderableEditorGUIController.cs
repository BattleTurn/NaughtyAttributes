using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorGUIController
    {
        private static readonly Dictionary<ListKey, HashSet<int>> _selectedIndices = new();

        private static readonly Dictionary<ListKey, Dictionary<int, Vector2>> _mouseDownPositions = new();

        // Custom drag state
        private static ListKey? _dragKey = null;
        private static HashSet<int> _dragIndices = null;
        private static bool _isDragging = false;
        private static int _dragInsertIndex = -1;

        internal static IDictionary<ListKey, HashSet<int>> SelectedIndices => _selectedIndices;
        internal static IDictionary<ListKey, Dictionary<int, Vector2>> MouseDownPositions => _mouseDownPositions;

        internal static bool CheckAnyKeyboardShortcutPressed(Dictionary<ListKey, ReorderableList> arrayLists, ListKey key, Event currentEvent)
        {
            if (currentEvent.type == EventType.KeyDown && _selectedIndices.ContainsKey(key) && _selectedIndices[key].Count > 0)
            {
                if (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace)
                {
                    arrayLists.DeleteSelectedElements(key, _selectedIndices[key]);
                    currentEvent.Use();
                    return true;
                }
                else if (currentEvent.keyCode == KeyCode.D && (currentEvent.control || currentEvent.command))
                {
                    arrayLists.DuplicateSelectedElements(key, _selectedIndices[key]);
                    currentEvent.Use();
                    return true;
                }
            }

            return false;
        }


        internal static Rect OnContextMenuRightClick(SerializedProperty arrayProp, Rect headerRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.ContextClick && headerRect.Contains(currentEvent.mousePosition))
            {
                ReorderableEditorGUI.ShowArrayContextMenu(arrayProp);
                currentEvent.Use();
            }

            return headerRect;
        }

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
                        DrawInsertionLine(r, index, arrayProp);
                    }
                    break;
            }
        }

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

        private static void HandleElementSelection(ListKey key, int index, Event currentEvent)
        {
            // Clear selections from other properties when starting new selection
            ClearOtherPropertySelections(key, currentEvent);
            
            var selectedSet = EnsureSelectionExists(key);

            if (currentEvent.button == 0) // Left click
            {
                HandleLeftClickSelection(selectedSet, index, currentEvent);
            }
            else if (currentEvent.button == 1) // Right click
            {
                HandleRightClickSelection(key, selectedSet, index);
            }
        }

        private static void ClearOtherPropertySelections(ListKey currentKey, Event currentEvent)
        {
            // Only clear other selections when starting a new selection operation
            // Don't clear when doing Ctrl+Click or Shift+Click on the same property
            bool isModifierClick = currentEvent.control || currentEvent.command || currentEvent.shift;
            
            if (!isModifierClick)
            {
                // Normal click: clear all other property selections
                var keysToRemove = _selectedIndices.Keys.Where(k => !k.Equals(currentKey)).ToList();
                foreach (var key in keysToRemove)
                {
                    _selectedIndices[key].Clear();
                }
            }
            else
            {
                // Modifier click: clear other properties but keep current if it has selection
                var keysToRemove = _selectedIndices.Keys.Where(k => !k.Equals(currentKey)).ToList();
                foreach (var key in keysToRemove)
                {
                    _selectedIndices[key].Clear();
                }
            }
        }

        private static HashSet<int> EnsureSelectionExists(ListKey key)
        {
            if (!_selectedIndices.ContainsKey(key))
            {
                _selectedIndices[key] = new HashSet<int>();
            }
            return _selectedIndices[key];
        }

        private static void HandleLeftClickSelection(HashSet<int> selectedSet, int index, Event currentEvent)
        {
            if (currentEvent.control || currentEvent.command) // Ctrl/Cmd + Click
            {
                HandleToggleSelection(selectedSet, index);
            }
            else if (currentEvent.shift) // Shift + Click
            {
                HandleRangeSelection(selectedSet, index);
            }
            else // Normal click
            {
                HandleSingleSelection(selectedSet, index);
            }
        }

        private static void HandleToggleSelection(HashSet<int> selectedSet, int index)
        {
            // Toggle selection
            if (selectedSet.Contains(index))
                selectedSet.Remove(index);
            else
                selectedSet.Add(index);
        }

        private static void HandleRangeSelection(HashSet<int> selectedSet, int index)
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

        private static void HandleSingleSelection(HashSet<int> selectedSet, int index)
        {
            // Simple single selection (smart selection will be handled in mouse up)
            selectedSet.Clear();
            selectedSet.Add(index);
        }

        private static void HandleRightClickSelection(ListKey key, HashSet<int> selectedSet, int index)
        {
            // If right-clicking on unselected item, select it first
            if (!selectedSet.Contains(index))
            {
                selectedSet.Clear();
                selectedSet.Add(index);
            }
            
            // Show context menu for selected elements
            ReorderableEditorGUI.ShowElementContextMenu(key, selectedSet);
        }

        private static void PerformCustomReorder(SerializedProperty arrayProp, HashSet<int> dragIndices, int insertIndex)
        {
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Reorder Multiple Elements");
            
            // Get sorted indices and backup data
            var sortedIndices = dragIndices.OrderBy(i => i).ToList();
            var draggedData = BackupDraggedElements(arrayProp, sortedIndices);
            
            // Remove elements and calculate final insert position
            RemoveDraggedElements(arrayProp, sortedIndices);
            int finalInsertIndex = CalculateFinalInsertIndex(insertIndex, sortedIndices);
            
            // Insert elements at new position and update selection
            InsertElementsAtNewPosition(arrayProp, draggedData, finalInsertIndex);
            UpdateSelectionAfterReorder(arrayProp, draggedData.Count, finalInsertIndex);
            
            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        private static List<object> BackupDraggedElements(SerializedProperty arrayProp, List<int> sortedIndices)
        {
            var draggedData = new List<object>();
            foreach (int idx in sortedIndices)
            {
                if (idx >= 0 && idx < arrayProp.arraySize)
                {
                    var element = arrayProp.GetArrayElementAtIndex(idx);
                    draggedData.Add(element.GetElementData());
                }
            }
            return draggedData;
        }

        private static void RemoveDraggedElements(SerializedProperty arrayProp, List<int> sortedIndices)
        {
            // Remove in reverse order to maintain indices
            var reversedIndices = sortedIndices.OrderByDescending(i => i).ToList();
            foreach (int idx in reversedIndices)
            {
                if (idx >= 0 && idx < arrayProp.arraySize)
                {
                    arrayProp.DeleteArrayElementAtIndex(idx);
                }
            }
        }

        private static int CalculateFinalInsertIndex(int insertIndex, List<int> sortedIndices)
        {
            int finalInsertIndex = insertIndex;
            foreach (int removedIndex in sortedIndices)
            {
                if (removedIndex < insertIndex)
                    finalInsertIndex--;
            }
            return finalInsertIndex;
        }

        private static void InsertElementsAtNewPosition(SerializedProperty arrayProp, List<object> draggedData, int finalInsertIndex)
        {
            for (int i = 0; i < draggedData.Count; i++)
            {
                int insertAt = Mathf.Max(0, finalInsertIndex + i);
                if (insertAt <= arrayProp.arraySize)
                {
                    arrayProp.InsertArrayElementAtIndex(insertAt);
                    var newElement = arrayProp.GetArrayElementAtIndex(insertAt);
                    newElement.SetElementData(draggedData[i]);
                }
            }
        }

        private static void UpdateSelectionAfterReorder(SerializedProperty arrayProp, int elementCount, int finalInsertIndex)
        {
            var newSelection = new HashSet<int>();
            for (int i = 0; i < elementCount; i++)
            {
                newSelection.Add(finalInsertIndex + i);
            }
            
            var key = new ListKey(arrayProp.serializedObject.targetObject.GetInstanceID(), arrayProp.propertyPath);
            _selectedIndices[key] = newSelection;
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

        private static SerializedProperty FindArrayPropertyByKey(ListKey key)
        {
            // Try to find the array property using the cached ReorderableList
            if (ReorderableEditorGUI.arrayLists.ContainsKey(key))
            {
                var reorderableList = ReorderableEditorGUI.arrayLists[key];
                return reorderableList.serializedProperty;
            }
            return null;
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

        private static void DrawInsertionLine(Rect elementRect, int index, SerializedProperty arrayProp)
        {
            if (_dragInsertIndex == index)
            {
                float y = elementRect.y - 1;
                EditorGUI.DrawRect(new Rect(elementRect.x, y, elementRect.width, 2), Color.cyan);
            }
            else if (_dragInsertIndex == index + 1)
            {
                float y = elementRect.y + elementRect.height - 1;
                EditorGUI.DrawRect(new Rect(elementRect.x, y, elementRect.width, 2), Color.cyan);
            }
        }
    }
}