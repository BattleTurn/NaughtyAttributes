using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    public static class ReorderableEditorUtilities
    {

        internal static void DeleteSelectedElements(this Dictionary<ListKey, ReorderableList> arrayLists, ListKey key, HashSet<int> selectedIndices)
        {
            var arrayProp = FindArrayPropertyByKey(arrayLists, key);
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
            ReorderableEditorGUIController.SelectedIndices[key].Clear();

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        internal static void DuplicateSelectedElements(this Dictionary<ListKey, ReorderableList> arrayLists, ListKey key, HashSet<int> selectedIndices)
        {
            var arrayProp = arrayLists.FindArrayPropertyByKey(key);
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
            ReorderableEditorGUIController.SelectedIndices[key] = newSelection;

            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        internal static SerializedProperty FindArrayPropertyByKey(this Dictionary<ListKey, ReorderableList> arrayLists, ListKey key)
        {
            // Try to find the array property using the cached ReorderableList
            if (arrayLists.ContainsKey(key))
            {
                var reorderableList = arrayLists[key];
                return reorderableList.serializedProperty;
            }
            return null;
        }
    }
}