
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    /// <summary>
    /// Enhanced ReorderableList implementation with intelligent drag & drop support
    /// Features: Smart type detection, auto-conversion, multi-object drops, component extraction
    /// </summary>
    public static class ReorderableEditorGUI
    {
        #region Fields & Constants
        
        internal static readonly Dictionary<ListKey, ReorderableList> arrayLists = new();

        private static readonly Dictionary<ListKey, Dictionary<int, Color>> _elementBackgrounds = new();
        
        private const float INDENT_WIDTH = 15.0f;

        #endregion

        #region Public API
        internal static void ShowElementContextMenu(ListKey key, HashSet<int> selectedIndices)
        {
            GenericMenu menu = new GenericMenu();

            if (selectedIndices.Count == 1)
            {
                menu.AddItem(new GUIContent($"Delete Element"), false, () => arrayLists.DeleteSelectedElements(key, selectedIndices));
                menu.AddItem(new GUIContent($"Duplicate Element"), false, () => arrayLists.DuplicateSelectedElements(key, selectedIndices));
            }
            else if (selectedIndices.Count > 1)
            {
                menu.AddItem(new GUIContent($"Delete {selectedIndices.Count} Elements"), false, () => arrayLists.DeleteSelectedElements(key, selectedIndices));
                menu.AddItem(new GUIContent($"Duplicate {selectedIndices.Count} Elements"), false, () => arrayLists.DuplicateSelectedElements(key, selectedIndices));
            }

            menu.ShowAsContext();
        }
        
        /// <summary>
        /// Creates and renders a ReorderableList with enhanced drag & drop capabilities
        /// </summary>
        public static void CreateReorderableList(Rect rect, SerializedProperty arrayProp)
        {
            var reorderableList = GetOrCreateReorderableList(arrayProp);
            var listRect = CalculateListRect(rect, arrayProp, reorderableList);

            // Handle drag & drop BEFORE rendering to take event priority
            var dropRect = CalculateExpandedDropRect(listRect, arrayProp);
            HandleDragAndDrop(arrayProp, dropRect);

            reorderableList.DoList(listRect);
        }

        /// <summary>
        /// Clears all cached ReorderableList instances
        /// </summary>
        public static void ClearCache()
        {
            arrayLists.Clear();
            ReorderableEditorGUIController.SelectedIndices.Clear();
            _elementBackgrounds.Clear();
        }
        
        public static void ShowArrayContextMenu(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            bool hasSelection = ReorderableEditorGUIController.SelectedIndices.ContainsKey(key) && ReorderableEditorGUIController.SelectedIndices[key].Count > 0;
            
            GenericMenu menu = new GenericMenu();

            // Selection-based operations
            if (hasSelection)
            {
                int selectedCount = ReorderableEditorGUIController.SelectedIndices[key].Count;
                menu.AddItem(new GUIContent($"Delete Selected ({selectedCount})"), false, () => arrayLists.DeleteSelectedElements(key, ReorderableEditorGUIController.SelectedIndices[key]));
                menu.AddItem(new GUIContent($"Duplicate Selected ({selectedCount})"), false, () => arrayLists.DuplicateSelectedElements(key, ReorderableEditorGUIController.SelectedIndices[key]));
                menu.AddSeparator("");
            }

            // Clear Array
            menu.AddItem(new GUIContent("Clear Array"), false, () => ClearArray(arrayProp));

            menu.AddSeparator("");

            // Remove Duplicates (only for ObjectReference arrays)
            if (IsObjectReferenceArray(arrayProp) && arrayProp.arraySize > 1)
            {
                menu.AddItem(new GUIContent("Remove Duplicates"), false, () => RemoveDuplicates(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove Duplicates"));
            }

            // Remove Null References
            if (IsObjectReferenceArray(arrayProp) && HasNullReferences(arrayProp))
            {
                menu.AddItem(new GUIContent("Remove Null References"), false, () => RemoveNullReferences(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove Null References"));
            }

            menu.AddSeparator("");

            // Sort Array (only for compatible types)
            if (CanSortArray(arrayProp) && arrayProp.arraySize > 1)
            {
                var sortMenuText = GetSortMenuText(arrayProp);
                menu.AddItem(new GUIContent(sortMenuText), false, () => SortArray(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Sort"));
            }

            // Reverse Array
            if (arrayProp.arraySize > 1)
            {
                menu.AddItem(new GUIContent("Reverse"), false, () => ReverseArray(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Reverse"));
            }

            menu.ShowAsContext();
        }
        #endregion

        #region ReorderableList Creation & Management

        private static ReorderableList GetOrCreateReorderableList(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);

            if (!arrayLists.ContainsKey(key))
            {
                var reorderableList = CreateNewReorderableList(arrayProp, key);
                arrayLists[key] = reorderableList;

                if (!ReorderableEditorGUIController.SelectedIndices.ContainsKey(key))
                {
                    ReorderableEditorGUIController.SelectedIndices[key] = new HashSet<int>();
                }
            }

            var list = arrayLists[key];
            list.draggable = arrayProp.isExpanded;
            return list;
        }

        private static ReorderableList CreateNewReorderableList(SerializedProperty arrayProp, ListKey key)
        {
            var so = arrayProp.serializedObject;
            Rect headerRect = new Rect();

            var reorderableList = new ReorderableList(so, arrayProp, false, true, true, true); // draggable = false
            
            // Additional steps to ensure no reorder handles
            reorderableList.showDefaultBackground = true;
            
            reorderableList.drawHeaderCallback = (Rect r) =>
            {
                int indentLevel = EditorGUI.indentLevel;
                float indent = indentLevel * INDENT_WIDTH;
                headerRect = new Rect(r.x + indent, r.y, r.width - indent, r.height);
                DrawHeader(arrayProp, headerRect);
            };

            reorderableList.drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) =>
            {
                // Completely override Unity's default element drawing
                // This ensures no reorder handles are drawn
                if (index >= 0 && index < arrayProp.arraySize)
                {
                    DrawElement(arrayProp, key, r, index);
                }
            };

            // Override background drawing to disable Unity's default selection highlight
            reorderableList.drawElementBackgroundCallback = (Rect r, int index, bool isActive, bool isFocused) =>
            {
                // Do nothing - we handle our own background in DrawElement
            };

            reorderableList.elementHeightCallback = (int index) =>
            {
                return GetElementHeight(arrayProp, index);
            };

            // Force disable dragging to prevent default reorder icons
            reorderableList.draggable = false;
            
            // Disable default reorder callbacks to prevent conflicts
            reorderableList.onReorderCallback = null;

            reorderableList.onAddCallback = (ReorderableList l) => ReorderableList.defaultBehaviours.DoAddButton(l);
            reorderableList.onRemoveCallback = (ReorderableList l) => ReorderableList.defaultBehaviours.DoRemoveButton(l);
            
            reorderableList.drawNoneElementCallback = (Rect rr) =>
            {
                DrawNoneElement(arrayProp, rr, reorderableList);
            };
            
            reorderableList.drawFooterCallback = (Rect fr) =>
            {
                DrawFooter(arrayProp, fr, reorderableList);
            };
            
            return reorderableList;
        }
        
        #endregion

        #region Layout & Rect Calculations
        
        private static Rect CalculateListRect(Rect rect, SerializedProperty arrayProp, ReorderableList reorderableList)
        {
            if (rect == default)
            {
                float listHeight = CalculateListHeight(arrayProp, reorderableList);
                Rect rr = EditorGUILayout.GetControlRect(false, listHeight);
                return EditorGUI.IndentedRect(rr);
            }
            else
            {
                Rect rr = rect;
                rr.height = arrayProp.isExpanded
                    ? reorderableList.GetHeight()
                    : EditorGUIUtility.singleLineHeight + 4f;
                return rr;
            }
        }

        private static float CalculateListHeight(SerializedProperty arrayProp, ReorderableList reorderableList)
        {
            if (!arrayProp.isExpanded)
            {
                reorderableList.elementHeight = 0f;
                return EditorGUIUtility.singleLineHeight + 6f;
            }
            else
            {
                return reorderableList.GetHeight();
            }
        }

        private static Rect CalculateExpandedDropRect(Rect listRect, SerializedProperty arrayProp)
        {
            Rect expandedDropRect = new Rect(listRect.x, listRect.y, listRect.width, listRect.height);
            
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
            
            return expandedDropRect;
        }
        
        #endregion

        #region Drawing Methods
        
        private static void DrawHeader(SerializedProperty arrayProp, Rect r)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);

            // Build header content
            var headerContent = BuildHeaderContent(arrayProp, key);
            
            // Calculate header rect
            Rect headerRect = CalculateHeaderRect(r, arrayProp);

            // Handle context menu
            if (HandleHeaderContextMenu(headerRect, arrayProp)) return;

            // Draw foldout and handle expansion changes
            DrawFoldoutAndHandleExpansion(headerRect, arrayProp, headerContent);
        }

        private static GUIContent BuildHeaderContent(SerializedProperty arrayProp, ListKey key)
        {
            // Build header label with selection info
            string label = $"{arrayProp.displayName}: {arrayProp.arraySize}";
            string tooltip = BuildHeaderTooltip();

            // Add selection info if any elements are selected
            if (ReorderableEditorGUIController.SelectedIndices.ContainsKey(key) && 
                ReorderableEditorGUIController.SelectedIndices[key].Count > 0)
            {
                int selectedCount = ReorderableEditorGUIController.SelectedIndices[key].Count;
                label += $" ({selectedCount} selected)";
                tooltip = $"Selected: {selectedCount} items\n" + tooltip;
            }

            return new GUIContent(label, tooltip);
        }

        private static string BuildHeaderTooltip()
        {
            return "💡 Drag elements to auto-select similar adjacent items\n" +
                   "• Green highlight = Smart selection\n" +
                   "• Blue highlight = Manual selection\n" +
                   "• Ctrl+Click = Manual multi-select\n" +
                   "• Right-click = Context menu";
        }

        private static Rect CalculateHeaderRect(Rect r, SerializedProperty arrayProp)
        {
            Rect headerRect = new Rect(r.x, r.y, r.width, r.height);
            headerRect.x += INDENT_WIDTH;

            // Adjust for nested properties
            if (arrayProp.propertyPath.Contains('.'))
            {
                string[] pathParts = arrayProp.propertyPath.Split('.');
                float indent = EditorGUI.indentLevel * INDENT_WIDTH + INDENT_WIDTH * (pathParts.Length - 1);
                headerRect.x -= indent;
                headerRect.width += indent;
            }

            return headerRect;
        }

        private static bool HandleHeaderContextMenu(Rect headerRect, SerializedProperty arrayProp)
        {
            if (headerRect.BeenRightClickOn())
            {
                ShowArrayContextMenu(arrayProp);
                return true; // Context menu was shown, stop processing
            }
            return false;
        }

        private static void DrawFoldoutAndHandleExpansion(Rect headerRect, SerializedProperty arrayProp, GUIContent headerContent)
        {
            // Draw invisible label for proper spacing
            GUI.Label(headerRect, GUIContent.none);
            
            // Store previous expansion state
            bool lastExpanded = arrayProp.isExpanded;

            // Draw the foldout
            arrayProp.isExpanded = EditorGUI.Foldout(headerRect, arrayProp.isExpanded, headerContent, true);

            // Handle expansion state changes
            if (lastExpanded != arrayProp.isExpanded)
            {
                InvalidateListCache(arrayProp);
            }
        }

        private static void DrawElement(SerializedProperty arrayProp, ListKey key, Rect r, int index)
        {
            if (!arrayProp.isExpanded) return;

            Event currentEvent = Event.current;

            // Calculate rects first
            Rect fullBackgroundRect = new Rect(r.x - 19, r.y - 2, r.width + 24, r.height);
            Rect elementRect = new Rect(r.x - 19, r.y, r.width + 22, r.height);

            // Draw visual elements
            DrawElementBackground(fullBackgroundRect, key, index, currentEvent);
            
            // Handle delete button
            if (DrawDeleteButton(r, arrayProp, index)) return;

            // Adjust drawing area
            r.width -= 25;

            // Handle interactions
            HandleElementInteractions(key, index, r, currentEvent, arrayProp);

            // Draw reorder icon and property field
            DrawReorderIcon(elementRect, currentEvent);
            DrawPropertyField(elementRect, arrayProp, index);
        }

        private static void DrawElementBackground(Rect fullBackgroundRect, ListKey key, int index, Event currentEvent)
        {
            if (currentEvent.type != EventType.Repaint) return;

            // Draw alternating background
            Color backgroundColor = GetAlternatingBackgroundColor(index);
            EditorGUI.DrawRect(fullBackgroundRect, backgroundColor);

            // Draw selection frame
            DrawSelectionFrame(fullBackgroundRect, key, index);
        }

        private static Color GetAlternatingBackgroundColor(int index)
        {
            if (index % 2 == 0)
            {
                // Even rows - lighter
                return EditorGUIUtility.isProSkin
                    ? new Color(0.25f, 0.25f, 0.25f, 1f)
                    : new Color(0.92f, 0.92f, 0.92f, 1f);
            }
            else
            {
                // Odd rows - darker
                return EditorGUIUtility.isProSkin
                    ? new Color(0.20f, 0.20f, 0.20f, 1f)
                    : new Color(0.88f, 0.88f, 0.88f, 1f);
            }
        }

        private static void DrawSelectionFrame(Rect fullBackgroundRect, ListKey key, int index)
        {
            Color originalBackgroundColor = GUI.backgroundColor;

            // Check if this element is selected
            if (ReorderableEditorGUIController.SelectedIndices.ContainsKey(key) && 
                ReorderableEditorGUIController.SelectedIndices[key].Contains(index))
            {
                bool isSmartSelection = IsSmartSelection(key, index);
                GUI.backgroundColor = isSmartSelection
                    ? new Color(0.2f, 0.9f, 0.4f, 1f)  // Smart selection - green
                    : new Color(0.4f, 0.6f, 1f, 1f);   // Manual selection - blue
            }
            else
            {
                GUI.backgroundColor = Color.white; // Normal
            }

            GUI.Box(fullBackgroundRect, "", EditorStyles.helpBox);
            GUI.backgroundColor = originalBackgroundColor;
        }

        private static bool DrawDeleteButton(Rect r, SerializedProperty arrayProp, int index)
        {
            Rect deleteButtonRect = new Rect(r.xMax - 10, r.y - 3, 10, r.height);
            GUIStyle deleteButtonStyle = CreateDeleteButtonStyle();

            if (GUI.Button(deleteButtonRect, "×", deleteButtonStyle))
            {
                arrayProp.DeleteArrayElementAtIndex(index);
                return true; // Element was deleted
            }
            return false;
        }

        private static GUIStyle CreateDeleteButtonStyle()
        {
            var style = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            Color iconColor = EditorGUIUtility.isProSkin 
                ? new Color(0.7f, 0.7f, 0.7f) 
                : new Color(0.4f, 0.4f, 0.4f);
            
            style.normal.textColor = iconColor;
            style.hover.textColor = Color.white;
            style.normal.background = null;
            style.hover.background = null;

            return style;
        }

        private static void HandleElementInteractions(ListKey key, int index, Rect r, Event currentEvent, SerializedProperty arrayProp)
        {
            // Block mouse events on reorder handle area
            Rect handleBlockRect = new Rect(r.x - 20, r.y, 15, r.height);
            if (handleBlockRect.IsBlockClick(currentEvent)) return;

            // Handle keyboard shortcuts
            if (ReorderableEditorGUIController.CheckAnyKeyboardShortcutPressed(arrayLists, key, currentEvent))
            {
                return;
            }

            // Handle custom drag & drop
            ReorderableEditorGUIController.HandleCustomDragAndDrop(key, index, r, currentEvent, arrayProp);
        }

        private static void DrawReorderIcon(Rect elementRect, Event currentEvent)
        {
            if (currentEvent.type != EventType.Repaint) return;

            Rect reorderIconRect = new Rect(elementRect.x + 6, elementRect.y + (elementRect.height - 12) / 2, 12, 12);
            Color reorderIconColor = EditorGUIUtility.isProSkin 
                ? new Color(0.6f, 0.6f, 0.6f) 
                : new Color(0.4f, 0.4f, 0.4f);

            // Draw three horizontal lines to simulate reorder handle
            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new Rect(reorderIconRect.x + 1, reorderIconRect.y + 2 + i * 3, 10, 1);
                EditorGUI.DrawRect(lineRect, reorderIconColor);
            }
        }

        private static void DrawPropertyField(Rect elementRect, SerializedProperty arrayProp, int index)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
            int indentLevel = EditorGUI.indentLevel;
            float indent = indentLevel * INDENT_WIDTH;

            // Calculate property field rect with proper padding
            Rect propertyRect = new Rect(
                elementRect.x + 22.0f + indent, // Space for reorder icon
                elementRect.y + 1.0f,
                elementRect.width - 44.0f - indent, // Account for both icon and delete button
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.PropertyField(propertyRect, element, true);
        }

        private static float GetElementHeight(SerializedProperty arrayProp, int index)
        {
            if (!arrayProp.isExpanded) return 0f;
            return EditorGUI.GetPropertyHeight(arrayProp.GetArrayElementAtIndex(index)) + 4.0f;
        }

        private static void DrawNoneElement(SerializedProperty arrayProp, Rect rr, ReorderableList reorderableList)
        {
            if (!arrayProp.isExpanded) return;
            
            int indentLevel = EditorGUI.indentLevel;
            float indent = indentLevel * INDENT_WIDTH;
            Rect indentedRect = new Rect(rr.x + indent, rr.y, rr.width - indent, rr.height);
            
            ReorderableList.defaultBehaviours.DrawNoneElement(indentedRect, reorderableList.draggable);
        }

        private static void DrawFooter(SerializedProperty arrayProp, Rect fr, ReorderableList reorderableList)
        {
            if (!arrayProp.isExpanded) return;
            
            int indentLevel = EditorGUI.indentLevel;
            float indent = indentLevel * INDENT_WIDTH;
            // Reduce footer height by 1px to tighten the container
            Rect indentedRect = new Rect(fr.x + indent, fr.y, fr.width - indent, fr.height);
            
            ReorderableList.defaultBehaviours.DrawFooter(indentedRect, reorderableList);
        }

        private static void InvalidateListCache(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            arrayLists.Remove(key);
            ReorderableEditorGUIController.SelectedIndices.Remove(key);
            ReorderableEditorGUIController.MouseDownPositions.Remove(key);
            _elementBackgrounds.Remove(key);
            InternalEditorUtility.RepaintAllViews();
        }

        private static bool IsSmartSelection(ListKey key, int index)
        {
            // Check if this selection was created by smart grouping
            // vs manual Ctrl+click selection
            if (!ReorderableEditorGUIController.SelectedIndices.ContainsKey(key))
                return false;
                
            var selection = ReorderableEditorGUIController.SelectedIndices[key];
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

        #region Array Utility Methods
        
        private static void ClearArray(SerializedProperty arrayProp)
        {
            Undo.RecordObject(arrayProp.serializedObject.targetObject, "Clear Array");
            arrayProp.ClearArray();
            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        private static void RemoveDuplicates(SerializedProperty arrayProp)
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

        private static bool CanSortArray(SerializedProperty arrayProp)
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

        private static string GetSortMenuText(SerializedProperty arrayProp)
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

        private static void SortArray(SerializedProperty arrayProp)
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

        #region Array Utility Methods (Continued)

        private static bool HasNullReferences(SerializedProperty arrayProp)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null)
                    return true;
            }
            return false;
        }

        private static void RemoveNullReferences(SerializedProperty arrayProp)
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

        private static void ReverseArray(SerializedProperty arrayProp)
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

        #region Type Detection & Validation
        
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
    }
}