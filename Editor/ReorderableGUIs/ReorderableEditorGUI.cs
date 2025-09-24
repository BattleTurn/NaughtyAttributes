
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
    public sealed class ReorderableEditorGUI
    {
        #region Constant Fields
        private const float INDENT_WIDTH = 15.0f;
        #endregion

        #region Public API
        internal ReorderableList CreateNewReorderableList(Dictionary<ListKey, HashSet<int>> selectedIndices, SerializedProperty arrayProp, ListKey key)
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
                DrawHeader(selectedIndices, arrayProp, headerRect);
            };

            reorderableList.drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) =>
            {
                // Completely override Unity's default element drawing
                // This ensures no reorder handles are drawn
                if (index >= 0 && index < arrayProp.arraySize)
                {
                    DrawElement(selectedIndices, arrayProp, key, r, index);
                }
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
        
        
        internal void ShowElementContextMenu(ListKey key, HashSet<int> selectedIndices)
        {
            GenericMenu menu = new GenericMenu();

            if (selectedIndices.Count == 1)
            {
                menu.AddItem(new GUIContent($"Delete Element"), false, () => ReorderableEditorController.DeleteSelectedElements(key, selectedIndices));
                menu.AddItem(new GUIContent($"Duplicate Element"), false, () => ReorderableEditorController.DuplicateSelectedElements(key, selectedIndices));
            }
            else if (selectedIndices.Count > 1)
            {
                menu.AddItem(new GUIContent($"Delete {selectedIndices.Count} Elements"), false, () => ReorderableEditorController.DeleteSelectedElements(key, selectedIndices));
                menu.AddItem(new GUIContent($"Duplicate {selectedIndices.Count} Elements"), false, () => ReorderableEditorController.DuplicateSelectedElements(key, selectedIndices));
            }

            menu.ShowAsContext();
        }

        private static void ShowArrayContextMenu(Dictionary<ListKey, HashSet<int>> selectedIndices, SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            bool hasSelection = selectedIndices.ContainsKey(key) && selectedIndices[key].Count > 0;
            
            GenericMenu menu = new GenericMenu();

            // Selection-based operations
            if (hasSelection)
            {
                int selectedCount = selectedIndices[key].Count;
                menu.AddItem(new GUIContent($"Delete Selected ({selectedCount})"), false, () => ReorderableEditorController.DeleteSelectedElements(key, selectedIndices[key]));
                menu.AddItem(new GUIContent($"Duplicate Selected ({selectedCount})"), false, () => ReorderableEditorController.DuplicateSelectedElements(key, selectedIndices[key]));
                menu.AddSeparator("");
            }

            // Clear Array
            menu.AddItem(new GUIContent("Clear Array"), false, () => ReorderableEditorController.ClearArray(arrayProp));

            menu.AddSeparator("");

            // Remove Duplicates (only for ObjectReference arrays)
            if (ReorderableEditorController.IsObjectReferenceArray(arrayProp) && arrayProp.arraySize > 1)
            {
                menu.AddItem(new GUIContent("Remove Duplicates"), false, () => ReorderableEditorController.RemoveDuplicates(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove Duplicates"));
            }

            // Remove Null References
            if (ReorderableEditorController.IsObjectReferenceArray(arrayProp) && ReorderableEditorController.HasNullReferences(arrayProp))
            {
                menu.AddItem(new GUIContent("Remove Null References"), false, () => ReorderableEditorController.RemoveNullReferences(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Remove Null References"));
            }

            menu.AddSeparator("");

            // Sort Array (only for compatible types)
            if (ReorderableEditorController.CanSortArray(arrayProp) && arrayProp.arraySize > 1)
            {
                var sortMenuText = ReorderableEditorController.GetSortMenuText(arrayProp);
                menu.AddItem(new GUIContent(sortMenuText), false, () => ReorderableEditorController.SortArray(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Sort"));
            }

            // Reverse Array
            if (arrayProp.arraySize > 1)
            {
                menu.AddItem(new GUIContent("Reverse"), false, () => ReorderableEditorController.ReverseArray(arrayProp));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Reverse"));
            }

            menu.ShowAsContext();
        }
        #endregion

        #region Public API - Layout & Rect Calculations
        public Rect CalculateListRect(Rect rect, SerializedProperty arrayProp, ReorderableList reorderableList)
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

        public Rect CalculateExpandedDropRect(Rect listRect, SerializedProperty arrayProp)
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
        #endregion

        #region Public API Drawing Methods
        public void DrawInsertionLine(int dragInsertIndex, Rect elementRect, int index)
        {
            if (dragInsertIndex == index)
            {
                float y = elementRect.y - 1;
                EditorGUI.DrawRect(new Rect(elementRect.x, y, elementRect.width, 2), Color.cyan);
            }
            else if (dragInsertIndex == index + 1)
            {
                float y = elementRect.y + elementRect.height - 1;
                EditorGUI.DrawRect(new Rect(elementRect.x, y, elementRect.width, 2), Color.cyan);
            }
        }
        #endregion

        #region Drawing Methods
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
            Rect indentedRect = new Rect(fr.x + indent, fr.y, fr.width - indent, fr.height);

            ReorderableList.defaultBehaviours.DrawFooter(indentedRect, reorderableList);
        }
        
        private void DrawHeader(Dictionary<ListKey, HashSet<int>> selectedIndices, SerializedProperty arrayProp, Rect r)
        {
            var so = arrayProp.serializedObject;
            var key = new ListKey(so.targetObject ? so.targetObject.GetInstanceID() : 0, arrayProp.propertyPath);
            
            // Build header label with selection info and helpful tooltip
            string label = $"{arrayProp.displayName}: {arrayProp.arraySize}";
            string tooltip = "💡 Drag elements to auto-select similar adjacent items\n• Green highlight = Smart selection\n• Blue highlight = Manual selection\n• Ctrl+Click = Manual multi-select\n• Right-click = Context menu";
            
            if (selectedIndices.ContainsKey(key) && selectedIndices[key].Count > 0)
            {
                label += $" ({selectedIndices[key].Count} selected)";
                tooltip = $"Selected: {selectedIndices[key].Count} items\n" + tooltip;
            }

            var headerContent = new GUIContent(label, tooltip);

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
            }

            // Handle context menu on right click
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.ContextClick && headerRect.Contains(currentEvent.mousePosition))
            {
                ShowArrayContextMenu(selectedIndices, arrayProp);
                currentEvent.Use();
            }

            arrayProp.isExpanded = EditorGUI.Foldout(headerRect, arrayProp.isExpanded, headerContent, true);

            if (lastExpanded != arrayProp.isExpanded)
            {
                ReorderableEditorController.InvalidateListCache(arrayProp);
            }
        }

        private void DrawElement(Dictionary<ListKey, HashSet<int>> selectedIndices, SerializedProperty arrayProp, ListKey key, Rect r, int index)
        {
            if (!arrayProp.isExpanded) return;

            Event currentEvent = Event.current;

            // Draw alternating background colors for better visibility
            if (currentEvent.type == EventType.Repaint)
            {
                Color backgroundColor = GetAlternatingBackgroundColor(index);
                
                EditorGUI.DrawRect(r, backgroundColor);
            }

            // Draw delete button (X) on the right side
            Rect deleteButtonRect = new Rect(r.xMax - 20, r.y - 3, 10, r.height);

            // Style for the X button - no background, just text like reorder icon
            GUIStyle deleteButtonStyle = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal, // Normal weight for thin appearance
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            // Set colors - gray like reorder icon, white on hover
            Color iconColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);
            deleteButtonStyle.normal.textColor = iconColor;
            deleteButtonStyle.hover.textColor = Color.white;
            deleteButtonStyle.normal.background = null; // No background
            deleteButtonStyle.hover.background = null;   // No background on hover

            // Draw the delete button
            if (GUI.Button(deleteButtonRect, "×", deleteButtonStyle))
            {
                // Delete this element
                arrayProp.DeleteArrayElementAtIndex(index);
                return;
            }

            // Adjust element drawing area to not overlap with delete button
            r.width -= 25;

            // Block mouse events on potential reorder handle area (without visual overlay)
            Rect handleBlockRect = new Rect(r.x - 20, r.y, 15, r.height);
            if (handleBlockRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDown ||
                    currentEvent.type == EventType.MouseDrag ||
                    currentEvent.type == EventType.MouseUp)
                {
                    currentEvent.Use(); // Block Unity's reorder without visual overlay
                }
            }

            if (!ReorderableEditorController.HasAnyKeyboardShortcutPressed(key, currentEvent))
            {
                return;
            }

            // Custom drag & drop handling
            ReorderableEditorController.HandleCustomDragAndDrop(key, index, r, currentEvent, arrayProp);

            // Draw selection background with different intensity for better visibility
            r = DrawSelectionIndex(selectedIndices, key, r, index);

            SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
            int indentLevel = EditorGUI.indentLevel;
            float indent = indentLevel * INDENT_WIDTH;

            r.y += 1.0f;
            r.x += 10.0f + indent; // Back to normal spacing
            r.width -= 10.0f + indent;

            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), element, true);
        }

        private static void DrawElementBackground(Dictionary<ListKey, HashSet<int>> selectedIndices, Rect fullBackgroundRect, ListKey key, int index, Event currentEvent)
        {
            if (currentEvent.type != EventType.Repaint) return;

            // Draw alternating background
            Color backgroundColor = GetAlternatingBackgroundColor(index);
            EditorGUI.DrawRect(fullBackgroundRect, backgroundColor);

            // Draw selection frame
            DrawSelectionFrame(selectedIndices, fullBackgroundRect, key, index);
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

        private static void DrawSelectionFrame(Dictionary<ListKey, HashSet<int>> selectedIndices, Rect fullBackgroundRect, ListKey key, int index)
        {
            // Check if this element is selected
            if (selectedIndices.ContainsKey(key) && selectedIndices[key].Contains(index))
            {
                bool isSmartSelection = ReorderableEditorController.IsSmartSelection(key, index);
                GUI.backgroundColor = isSmartSelection
                    ? new Color(0.2f, 0.9f, 0.4f, 1f)  // Smart selection - green
                    : new Color(0.4f, 0.6f, 1f, 1f);   // Manual selection - blue
            }
            else
            {
                GUI.backgroundColor = Color.white; // Normal
            }

            GUI.Box(fullBackgroundRect, "", EditorStyles.helpBox);
        }

        private static Rect DrawSelectionIndex(Dictionary<ListKey, HashSet<int>> selectedIndices, ListKey key, Rect r, int index)
        {
            if (selectedIndices.ContainsKey(key) && selectedIndices[key].Contains(index))
            {
                Color selectionColor;
                // Check if this is a smart selection (adjacent elements) vs manual selection
                bool isSmartSelection = ReorderableEditorController.IsSmartSelection(key, index);

                if (isSmartSelection)
                {
                    // Smart selection - greenish color for grouped elements
                    selectionColor = new Color(0.2f, 0.8f, 0.4f, 0.4f);
                }
                else
                {
                    // Manual selection - blue color
                    selectionColor = new Color(0.3f, 0.5f, 1f, 0.4f);
                }

                if (selectedIndices[key].Count == 1)
                    selectionColor.a = 0.3f; // Single selection - lighter
                else
                    selectionColor.a = 0.5f; // Multi selection - more prominent

                EditorGUI.DrawRect(r, selectionColor);

                // Add a small indicator for smart selection
                if (isSmartSelection && selectedIndices[key].Count > 1)
                {
                    var indicatorRect = new Rect(r.x + r.width - 15, r.y + 2, 12, 12);
                    EditorGUI.DrawRect(indicatorRect, new Color(0.1f, 0.6f, 0.2f, 0.8f));
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.white;
                    style.fontSize = 8;
                    style.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(indicatorRect, "●", style);
                }
            }

            return r;
        }
        #endregion
    }
}