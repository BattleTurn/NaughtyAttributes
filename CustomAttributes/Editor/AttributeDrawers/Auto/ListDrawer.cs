using System;
using System.Collections;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using StylizeAttributes.Core;
using System.Diagnostics;

namespace StylizeAttributes.Editor
{
    /// <summary>
    /// Auto drawer for any IList (covers List<T> and arrays). Picked up by StylizedAttributeInspector.
    /// </summary>
    public class ListDrawer : AutoDrawerBase
    {
        // Bind to non-generic IList so List<T> and arrays match via IsAssignableFrom
        public override Type BindType => typeof(IList);

        public override void BindTargetAttribute(IStylized attribute)
        {
            // No attribute binding for auto drawers
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property, VisualElement root)
        {
            // Optional style load (safe if not configured)
            LoadUXML(root);
            LoadUSS(root);

            // Not a list/array? fallback
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                var fallback = new PropertyField(property.Copy());
                root.Add(fallback);
                return root;
            }

            // Expect UXML-defined structure
            var container = root.Q<VisualElement>("ListDrawerRoot") ?? root;
            var header = container.Q<Label>("Header");
            var addBtn = container.Q<Button>("AddButton");
            var contentArea = container.Q<VisualElement>("ListContent");

            if (header != null)
            {
                header.text = property.displayName;
            }

            // Load element row template asset
            // Wire add button from UXML
            if (addBtn != null)
            {
                addBtn.clicked -= null; // ensure no duplicate
                addBtn.clicked += () =>
                {
                    var p = property.Copy();
                    p.serializedObject.Update();
                    p.arraySize++;
                    p.serializedObject.ApplyModifiedProperties();
                    RefreshList(contentArea, property, (styleBase as UIListStyle).element_uxml);
                };
            }

            // Initial population
            RefreshList(contentArea, property, (styleBase as UIListStyle).element_uxml);
            return root;
        }

        private void RefreshList(VisualElement contentArea, SerializedProperty listProp, VisualTreeAsset elementTemplate)
        {
            if (contentArea == null || listProp == null) return;

            contentArea.Clear();
            listProp.serializedObject.Update();

            UnityEngine.Debug.Log($"Element Template: {elementTemplate}");

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elementProp = listProp.GetArrayElementAtIndex(i);

                VisualElement row;
                if (elementTemplate != null)
                {
                    row = elementTemplate.CloneTree();
                    row.AddToClassList("list-element-row");
                    var fieldLabel = row.Q<Label>("ElementFieldLabel");
                    var fieldContainer = row.Q<VisualElement>("ElementFieldContainer") ?? row;
                    var removeBtn = row.Q<Button>("RemoveButton");

                    var field = new PropertyField(elementProp.Copy());
                    field.style.flexGrow = 1f;
                    fieldLabel.text = elementProp.displayName;
                    field.label = "";
                    field.BindProperty(elementProp);
                    fieldContainer.Add(field);

                    if (removeBtn != null)
                    {
                        int index = i;
                        removeBtn.clicked += () =>
                        {
                            var p = listProp.Copy();
                            p.serializedObject.Update();
                            int oldSize = p.arraySize;
                            p.DeleteArrayElementAtIndex(index);
                            if (p.arraySize == oldSize)
                                p.DeleteArrayElementAtIndex(index);
                            p.serializedObject.ApplyModifiedProperties();
                            RefreshList(contentArea, listProp, elementTemplate);
                        };
                    }

                    // Handle drag button 🧲
                    var dragBtn = row.Q<VisualElement>("DragButton");
                    if (dragBtn != null)
                    {
                        int dragIndex = i;
                        VisualElement draggedElement = null;
                        int targetIndex = -1;

                        dragBtn.RegisterCallback<PointerDownEvent>(evt =>
                        {
                            draggedElement = row;
                            draggedElement.AddToClassList("dragging");
                            contentArea.CaptureMouse();
                            // evt.StopPropagation();
                            UnityEngine.Debug.Log("Drag down");
                        });

                        dragBtn.RegisterCallback<PointerMoveEvent>(evt =>
                        {
                            if (draggedElement == null || !contentArea.HasMouseCapture()) return;

                            // Detect element hover
                            var localPos = evt.localPosition;
                            foreach (var child in contentArea.Children())
                            {
                                if (child.worldBound.Contains(evt.position) && child != draggedElement)
                                {
                                    targetIndex = contentArea.IndexOf(child);
                                    break;
                                }
                            }
                            
                            UnityEngine.Debug.Log("Drag moving");
                            // evt.StopPropagation();
                        });

                        dragBtn.RegisterCallback<PointerUpEvent>(evt =>
                        {
                            if (draggedElement == null) return;

                            draggedElement.RemoveFromClassList("dragging");
                            contentArea.ReleaseMouse();

                            if (targetIndex >= 0 && targetIndex < listProp.arraySize && targetIndex != dragIndex)
                            {
                                listProp.serializedObject.Update();
                                listProp.MoveArrayElement(dragIndex, targetIndex);
                                listProp.serializedObject.ApplyModifiedProperties();
                            }

                            RefreshList(contentArea, listProp, elementTemplate);
                            // evt.StopPropagation();
                            UnityEngine.Debug.Log("Drag up");
                        });
                    }
                }
                else
                {
                    // Fallback if element template missing
                    row = new VisualElement();
                    row.AddToClassList("list-element-row");
                    var field = new PropertyField(elementProp.Copy());
                    field.style.flexGrow = 1f;
                    field.BindProperty(elementProp);
                    row.Add(field);
                    var removeBtn = new Button(() =>
                    {
                        var p = listProp.Copy();
                        p.serializedObject.Update();
                        int oldSize = p.arraySize;
                        p.DeleteArrayElementAtIndex(i);
                        if (p.arraySize == oldSize) p.DeleteArrayElementAtIndex(i);
                        p.serializedObject.ApplyModifiedProperties();
                        RefreshList(contentArea, listProp, elementTemplate);
                    })
                    { text = "−" };
                    removeBtn.AddToClassList("list-btn-remove");
                    row.Add(removeBtn);
                }

                contentArea.Add(row);
            }
        }
    }
}
