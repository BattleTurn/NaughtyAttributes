using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

namespace NaughtyAttributes.Editor
{
    public static class NaughtyEditorGUI
    {
        public const float IndentLength = 15.0f;
        public const float HorizontalSpacing = 2.0f;

        private static GUIStyle _buttonStyle = new GUIStyle(GUI.skin.button) { richText = true };
        private static int _suppressFoldoutHandlingDepth = 0;
        private static int _suppressBoxGroupHandlingDepth = 0;

        private delegate void PropertyFieldFunction(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren);

        public static void PropertyField(Rect rect, SerializedProperty property, bool includeChildren)
        {
            PropertyField_Implementation(rect, property, includeChildren, DrawPropertyField);
        }

        /// <summary>
        /// Draw a property using the Naughty pipeline. We DO NOT use Unity's built-in recursion.
        /// Instead, we draw this node only, then manually handle its children to ensure nested
        /// members (including non-serialized extras) are processed and ordered like in code.
        /// </summary>
        /// <summary>
        /// Draw a property using the Naughty pipeline. We DO NOT use Unity's built-in recursion.
        /// Instead, we draw this node only, then manually handle its children to ensure nested
        /// members (including non-serialized extras) are processed and ordered like in code.
        /// </summary>
        public static void PropertyField_Layout(SerializedProperty property, bool includeChildren)
        {
            // One-pass meta processing: visibility, grouping, enabled
            var meta = MetaAttributeExtensions.ProcessAllMetas(property,
                allowFoldoutGroup: _suppressFoldoutHandlingDepth == 0,
                allowBoxGroup: _suppressBoxGroupHandlingDepth == 0);
            if (!meta.visible) return;

            // >>> Special-case attributes (e.g., [ReorderableList]) take over drawing
            if (TryDrawSpecialCase(property)) return;

            // >>> Arrays/Lists (except string): draw with our ReorderableList helper
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                ReorderableEditorGUI.CreateReorderableList(default, property);
                return;
            }

            // Force built-in PropertyDrawer path for [Expandable]
            if (PropertyUtility.GetAttribute<ExpandableAttribute>(property) != null)
            {
                EditorGUILayout.PropertyField(property, includeChildren: false); // drawer handles expansion & children
                return;
            }

            if (meta.consumedByGroup) return; // group already drew content

            // === Case: Generic object (class/struct, not array) ===
            if (property.propertyType == SerializedPropertyType.Generic && !property.isArray)
            {
                // Chỉ vẽ foldout + label cho parent
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);

                if (property.isExpanded && includeChildren)
                {
                    object instance = PropertyUtility.GetTargetObjectOfProperty(property);
                    if (instance != null)
                    {
                        int savedIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = savedIndent + 1;
                        DrawMembersInDeclaredOrder(property, instance);
                        EditorGUI.indentLevel = savedIndent;
                    }
                    else
                    {
                        // fallback nếu không resolve được instance
                        int savedIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = savedIndent + 1;

                        IterateChildren(property, child =>
                        {
                            if (child.name == "m_Script") return;
                            PropertyField_Layout(child.Copy(), true);
                        });

                        EditorGUI.indentLevel = savedIndent;
                    }
                }

                return;
            }

            // === Default: draw node như thường ===
            var (owner, field) = ResolveOwnerAndField(property);

            bool enabled = meta.enabled;
            bool changedByUser = DrawThisNode(property, enabled);
            if (changedByUser)
            {
                property.serializedObject.ApplyModifiedProperties();
                MetaAttributeExtensions.RunAfterChangeCallbacks(property);
            }

            // 2) Run validators once (Min/Max…)
            RunValidatorsOnce(property);

            // 3) Children
            if (!includeChildren || !property.isExpanded) return;
            if (!property.hasVisibleChildren) return;

            object inst = PropertyUtility.GetTargetObjectOfProperty(property);
            if (inst != null)
            {
                int savedIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = savedIndent + 1;
                DrawMembersInDeclaredOrder(property, inst);
                EditorGUI.indentLevel = savedIndent;
            }
            else
            {
                int savedIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = savedIndent + 1;

                IterateChildren(property, child =>
                {
                    if (child.name == "m_Script") return;
                    PropertyField_Layout(child.Copy(), true);
                });

                EditorGUI.indentLevel = savedIndent;
            }
        }


        /// <summary>
        /// Draw a property but ignore foldout grouping for this call (used by Foldout validator when drawing grouped children).
        /// </summary>
        public static void PropertyField_Layout_IgnoreFoldout(SerializedProperty property, bool includeChildren)
        {
            _suppressFoldoutHandlingDepth++;
            try
            {
                PropertyField_Layout(property, includeChildren);
            }
            finally
            {
                _suppressFoldoutHandlingDepth--;
            }
        }

        /// <summary>
        /// Draw a property but ignore foldout and box groups for this call (used by group validators when drawing grouped children).
        /// </summary>
        public static void PropertyField_Layout_IgnoreGroups(SerializedProperty property, bool includeChildren)
        {
            _suppressFoldoutHandlingDepth++;
            _suppressBoxGroupHandlingDepth++;
            try
            {
                PropertyField_Layout(property, includeChildren);
            }
            finally
            {
                _suppressBoxGroupHandlingDepth--;
                _suppressFoldoutHandlingDepth--;
            }
        }

        // Draw only a static group header (no arrow). Supports expanding to boxStyle padding.
        public static Rect DrawGroupHeader(string label, int itemCount, GUIStyle boxStyle = null, float labelOffsetX = 4f, bool expandBackground = true, bool useIndent = true, bool nestedContainer = false)
        {
            float startX;
            Rect bg = PrepareGroupHeader(boxStyle, labelOffsetX, expandBackground, useIndent, nestedContainer, out startX);
            string text = string.IsNullOrEmpty(label) ? $": {itemCount}" : $"{label}: {itemCount}";
            Rect labelRect = new Rect(startX, bg.y, bg.xMax - startX, bg.height);
            EditorGUI.LabelField(labelRect, text, EditorStyles.boldLabel);
            return bg;
        }

        // Draw a foldout-enabled header (arrow + label) with padding expansion support.
        public static Rect DrawExtendableGroupHeader(string label, int itemCount, ref bool expanded, GUIStyle boxStyle = null, float labelOffsetX = 4f, bool expandBackground = true, bool useIndent = true, bool nestedContainer = false)
        {
            float startX;
            Rect bg = PrepareGroupHeader(boxStyle, labelOffsetX, expandBackground, useIndent, nestedContainer, out startX);
            // Ensure label text aligns exactly like BoxGroup (startX already = bg.x + labelOffsetX).
            // Place arrow to the left so label begins at the same X as non-foldout group labels.
            const float arrowW = 14f;
            const float gap = 4f; // khoảng cách mong muốn giữa arrow và label
            float labelStart = startX; // target for label text
            float arrowX = labelStart - (arrowW + gap);
            // Clamp so arrow doesn't go outside background visually
            if (arrowX < bg.x + 1f) arrowX = bg.x + 1f;
            Rect arrowRect = new Rect(arrowX - 2f, bg.y + (bg.height - EditorGUIUtility.singleLineHeight) * 0.5f, arrowW, EditorGUIUtility.singleLineHeight);
            bool newExp = EditorGUI.Foldout(arrowRect, expanded, GUIContent.none, true);
            if (newExp != expanded) expanded = newExp;
            startX = labelStart; // keep label at aligned start
            string text = string.IsNullOrEmpty(label) ? $": {itemCount}" : $"{label}: {itemCount}";
            Rect labelRect = new Rect(startX, bg.y, bg.xMax - startX, bg.height);
            EditorGUI.LabelField(labelRect, text, EditorStyles.boldLabel);
            if (Event.current.type == EventType.MouseDown && bg.Contains(Event.current.mousePosition))
            {
                expanded = !expanded;
                GUI.changed = true;
                Event.current.Use();
            }
            return bg;
        }

        // Shared internal setup for group headers (background + starting X after indent + offset)
        private static Rect PrepareGroupHeader(GUIStyle boxStyle, float labelOffsetX, bool expandBackground, bool useIndent, bool nestedContainer, out float startX)
        {
            const float headerHeight = 20f;
            Rect raw = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
            Rect bg = raw;
            if (expandBackground && boxStyle != null)
            {
                bg.x -= boxStyle.padding.left;
                bg.width += boxStyle.padding.left + boxStyle.padding.right;
                bg.y -= boxStyle.padding.top;
            }
            var rlHeader = GUI.skin.FindStyle("RL Header");
            if (rlHeader != null) GUI.Label(bg, GUIContent.none, rlHeader);
            else
            {
                Color c = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.80f, 0.80f, 0.80f);
                EditorGUI.DrawRect(bg, c);
            }
            // Base X = bg.x + labelOffset. Nếu là root group chúng ta đã chèn GUILayout.Space(indent) phía ngoài.
            // Nếu là nestedContainer, đã có cả GUILayout.Space(indent) + indentLevel áp cho fields => trừ bớt một cấp indent.
            // Dùng chung một công thức cho root và nested để không bị chênh lệch (padding.left đã áp vào bg)
            startX = bg.x + labelOffsetX;
            return bg;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Group body helpers (shared BoxGroup & Foldout)
        // ─────────────────────────────────────────────────────────────────────────────
        private static int _groupBodyDepth;
        private static int _suppressAutoMemberIndentDepth; // skip +1 indent for nested group bodies
        private static Stack<int> _indentRestoreStack = new Stack<int>();

        public static void BeginGroupBody(bool nestedContainer, GUIStyle boxStyle)
        {
            GUILayout.BeginHorizontal();
            float indentPixels = EditorGUI.indentLevel * IndentLength;
            if (indentPixels > 0f) GUILayout.Space(indentPixels);
            EditorGUILayout.BeginVertical(boxStyle ?? EditorStyles.helpBox);
            if (nestedContainer) _suppressAutoMemberIndentDepth++;
            // Nested: remove internal label indent (we keep outer horizontal shift from GUILayout.Space)
            if (nestedContainer)
            {
                _indentRestoreStack.Push(EditorGUI.indentLevel);
                EditorGUI.indentLevel = 0;
            }
            else
            {
                _indentRestoreStack.Push(int.MinValue);
            }
        }

        public static void EndGroupBody()
        {
            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_suppressAutoMemberIndentDepth > 0) _suppressAutoMemberIndentDepth--;
            if (_indentRestoreStack.Count > 0)
            {
                int prev = _indentRestoreStack.Pop();
                if (prev != int.MinValue) EditorGUI.indentLevel = prev;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 0: gates & resolution
        // ─────────────────────────────────────────────────────────────────────────────

        private static bool IsVisibleByMeta(SerializedProperty property)
        {
            if (property == null) return false;
            // If a ShowIf-like meta exists, resolve its validator via MetaAttributeExtensions
            var showAttr = PropertyUtility.GetAttribute<ShowIfAttributeBase>(property);
            if (showAttr != null)
            {
                var validator = showAttr.GetValidator();
                return validator != null && validator.ValidateMetaProperty(property);
            }
            // Visible by default when no ShowIf
            return true;
        }

        private static (object owner, FieldInfo field) ResolveOwnerAndField(SerializedProperty property)
        {
            object owner = PropertyUtility.GetTargetObjectWithProperty(property);
            FieldInfo fi = owner != null ? ReflectionUtility.GetField(owner, property.name) : null;
            return (owner, fi);
        }

        private static bool IsArrayLike(SerializedProperty p) => p.isArray && p.propertyType != SerializedPropertyType.String;

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 1: draw THIS node (single line; no recursion)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws only this property (no recursion). Returns true if value changed.
        /// Uses a single reserved rect to avoid extra spacing.
        /// </summary>
        private static bool DrawThisNode(SerializedProperty property, bool enabled)
        {
            // Detect if there is any custom PropertyDrawer (DrawerAttribute subclass) so we let Unity handle full height.
            bool hasCustomDrawer = PropertyUtility.GetAttributes<DrawerAttribute>(property)?.Length > 0;
            if (!hasCustomDrawer)
            {
                // Generic, future‑proof detection of ANY external Unity PropertyDrawer:
                // 1) Attribute-based drawers (field has a PropertyAttribute with a corresponding CustomPropertyDrawer)
                // 2) Type-based drawers (field type itself has a CustomPropertyDrawer)
                var owner = PropertyUtility.GetTargetObjectWithProperty(property);
                var fi = owner != null ? ReflectionUtility.GetField(owner, property.name) : null;
                if (fi != null)
                {
                    bool attrDrawer = HasExternalUnityPropertyDrawer(fi);
                    bool typeDrawer = FieldTypeHasCustomDrawer(fi.FieldType);
                    // Removed hardcoded attribute name checks; now relies solely on general detection
                    // for any external PropertyAttribute or field type with a custom PropertyDrawer.
                    if (attrDrawer || typeDrawer)
                    {
                        hasCustomDrawer = true;
                    }
                }
            }
            bool includeChildren = false; // default for performance
            float h;
            if (hasCustomDrawer)
            {
                // Let Unity ask the drawer for correct height (e.g. ShowAssetPreview adds preview texture height)
                h = EditorGUI.GetPropertyHeight(property, includeChildren: true);
            }
            else
            {
                h = EditorGUI.GetPropertyHeight(property, includeChildren: false);
            }
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: true, height: h);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                var label = PropertyUtility.GetLabel(property);
                EditorGUI.BeginProperty(rect, label, property);
                EditorGUI.BeginChangeCheck();

                if (hasCustomDrawer)
                {
                    // Use default path so Unity dispatches to our PropertyDrawerBase subclass (ShowAssetPreviewPropertyDrawer etc.)
                    EditorGUI.PropertyField(rect, property, label, includeChildren: true);
                }
                else
                {
                    EditorGUI.PropertyField(rect, property, label, includeChildren: includeChildren);
                }

                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.EndProperty();
                return changed;
            }
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // External PropertyDrawer detection (attribute + type) — cached
        // ─────────────────────────────────────────────────────────────────────────────

        private static bool _drawerCacheBuilt;
        private static readonly HashSet<Type> _attrDrawerTargets = new HashSet<Type>();
        private static readonly HashSet<Type> _typeDrawerTargets = new HashSet<Type>();

        private static void BuildExternalDrawerCache()
        {
            if (_drawerCacheBuilt) return;
            _drawerCacheBuilt = true;
            
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    ProcessAssemblyForDrawers(assembly);
                }
            }
            catch { /* swallow – cache best effort */ }
        }

        private static void ProcessAssemblyForDrawers(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { return; }

            foreach (var drawerType in types)
            {
                if (!IsValidPropertyDrawerType(drawerType)) continue;
                ProcessPropertyDrawerType(drawerType, types);
            }
        }

        private static bool IsValidPropertyDrawerType(Type type)
        {
            return type != null 
                && !type.IsAbstract 
                && typeof(PropertyDrawer).IsAssignableFrom(type);
        }

        private static void ProcessPropertyDrawerType(Type drawerType, Type[] allTypes)
        {
            var customDrawerAttributes = GetCustomPropertyDrawerAttributes(drawerType);
            if (customDrawerAttributes.Length == 0) return;

            foreach (var attribute in customDrawerAttributes)
            {
                ProcessCustomPropertyDrawerAttribute(attribute, allTypes);
            }
        }

        private static object[] GetCustomPropertyDrawerAttributes(Type drawerType)
        {
            return drawerType.GetCustomAttributes(true)
                           .Where(a => a.GetType().Name == "CustomPropertyDrawer")
                           .ToArray();
        }

        private static void ProcessCustomPropertyDrawerAttribute(object customDrawerAttribute, Type[] allTypes)
        {
            var (targetType, useChildren) = ExtractDrawerAttributeInfo(customDrawerAttribute);
            if (targetType == null) return;

            if (typeof(PropertyAttribute).IsAssignableFrom(targetType))
            {
                _attrDrawerTargets.Add(targetType);
            }
            else
            {
                _typeDrawerTargets.Add(targetType);
                if (useChildren)
                {
                    AddDerivedTypes(targetType, allTypes);
                }
            }
        }

        private static (Type targetType, bool useChildren) ExtractDrawerAttributeInfo(object customDrawerAttribute)
        {
            var attributeType = customDrawerAttribute.GetType();
            var typeField = attributeType.GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
            var useChildrenField = attributeType.GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (typeField == null) return (null, false);
            
            var targetType = typeField.GetValue(customDrawerAttribute) as Type;
            bool useChildren = false;
            
            if (useChildrenField != null)
            {
                try { useChildren = (bool)useChildrenField.GetValue(customDrawerAttribute); }
                catch { /* ignore reflection errors */ }
            }
            
            return (targetType, useChildren);
        }

        private static void AddDerivedTypes(Type baseType, Type[] allTypes)
        {
            foreach (var derivedType in allTypes)
            {
                if (derivedType == null || derivedType == baseType) continue;
                if (baseType.IsAssignableFrom(derivedType))
                {
                    _typeDrawerTargets.Add(derivedType);
                }
            }
        }

        private static bool HasExternalUnityPropertyDrawer(FieldInfo fi)
        {
            if (fi == null) return false;
            BuildExternalDrawerCache();
            // Any non-Naughty PropertyAttribute (built-in or custom) is fine – Unity will merge them; we only need to know if a drawer exists.
            foreach (var attr in fi.GetCustomAttributes(true))
            {
                var at = attr.GetType();
                if (at.Namespace != null && at.Namespace.StartsWith("NaughtyAttributes")) continue; // internal handled elsewhere
                if (typeof(PropertyAttribute).IsAssignableFrom(at))
                {
                    if (_attrDrawerTargets.Contains(at)) return true; // explicit drawer
                    // Even without an explicit drawer mapping, still allow default PropertyField so built-in behavior (e.g. Range slider) works.
                    return true;
                }
            }
            return false;
        }

        private static bool FieldTypeHasCustomDrawer(Type fieldType)
        {
            if (fieldType == null) return false;
            BuildExternalDrawerCache();
            return _typeDrawerTargets.Contains(fieldType);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 4: ValidateAttributes
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Run all ValidatorAttributes on this property exactly once.
        /// Returns true if ANY validator actually modified the property's value (so we can Apply once).
        /// </summary>
        private static bool RunValidatorsOnce(SerializedProperty property)
        {
            var validators = PropertyUtility.GetAttributes<ValidatorAttribute>(property);
            bool modified = false;

            foreach (var attr in validators)
            {
                // IMPORTANT: validators must return true ONLY when they WRITE a new value to 'property'
                modified |= attr.GetValidator().ValidateProperty(property);
            }

            if (modified)
                property.serializedObject.ApplyModifiedProperties();

            return modified;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 5: Arrays/lists
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw arrays/lists in a reliable way:
        ///  - Header row was already drawn by DrawThisNode (foldout).
        ///  - If expanded, draw "Size" then each element through the Naughty pipeline.
        ///  - If Unity gives us something odd (managed refs, etc.), fallback to default drawing.
        /// </summary>
        private static void DrawArrayChildren(SerializedProperty arrayProp)
        {
            // Not expanded? Nothing to show.
            if (!arrayProp.isExpanded) return;

            // If the field has a special-case drawer (e.g., [ReorderableList]), it should have been
            // handled earlier by TryDrawSpecialCase() and we shouldn't be here.
            // If you want to be extra safe, you can early-return when attribute exists.

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = prevIndent + 1;

            try
            {
                // 1) Draw "Size" like Unity does
                SerializedProperty sizeProp = arrayProp.FindPropertyRelative("Array.size");
                if (sizeProp != null)
                {
                    // Use our pipeline for the size as well (no includeChildren needed)
                    PropertyField_Layout(sizeProp, includeChildren: false);
                }

                // 2) Draw each element via Naughty pipeline
                int count = arrayProp.arraySize;

                // If Unity can't give us elements properly, bail out to default drawing.
                // (This protects against odd cases like managed references / corrupted data.)
                bool fallbackNeeded = false;

                for (int i = 0; i < count; i++)
                {
                    SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
                    if (element == null)
                    {
                        fallbackNeeded = true;
                        break;
                    }

                    // Important: pass includeChildren:true so nested attributes inside element still work
                    PropertyField_Layout(element, includeChildren: true);
                }

                if (fallbackNeeded)
                {
                    // Safe fallback: let Unity render the whole thing with its built-in recursion.
                    // This guarantees the user still sees the list even if we can't traverse it.
                    EditorGUILayout.PropertyField(arrayProp, includeChildren: true);
                    return;
                }
            }
            finally
            {
                EditorGUI.indentLevel = prevIndent;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 4B: Managed containers (class/struct) — ordered rendering
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws members of a managed container in declaration order, mixing:
        ///   - serialized fields (Unity) → drawn via PropertyField_Layout on relative SP
        ///   - [ShowNonSerializedField] → drawn read-only with proper Unity-looking controls
        ///   - [Button] methods → drawn after fields (per type)
        /// </summary>
        private static void DrawMembersInDeclaredOrder(SerializedProperty containerProp, object instance)
        {
            var type = instance.GetType();

            // Fields: base → derived, each block in declaration order.
            foreach (var fi in GetFieldsInDeclarationOrder(type))
            {
                if (IsUnitySerializedField(fi))
                {
                    // Serialized: use relative path from the container property
                    SerializedProperty child = containerProp.FindPropertyRelative(fi.Name);
                    if (child == null)
                    {
                        // Field is not serialized or name mismatch, skip
                        continue;
                    }

                    // Only draw if property is visible
                    if (!child.hasVisibleChildren && child.propertyType == SerializedPropertyType.Generic && !child.isArray)
                    {
                        // Generic type with no children and not an array, skip
                        continue;
                    }

                    // Draw property, include children if it has them
                    if (child.isArray && child.propertyType != SerializedPropertyType.String)
                    {
                        ReorderableEditorGUI.CreateReorderableList(default, child);
                        continue;
                    }

                    // Draw property, include children if it has them
                    if (child.hasVisibleChildren || child.propertyType == SerializedPropertyType.Generic)
                    {
                        PropertyField_Layout(child, includeChildren: true);
                    }
                    else
                    {
                        PropertyField_Layout(child, includeChildren: false);
                    }
                    continue;
                }

                if (HasAttributeByName(fi, "ShowNonSerializedField") ||
                    HasAttributeByName(fi, "ShowNonSerializedFieldAttribute"))
                {
                    object value = null;
                    try { value = fi.GetValue(fi.IsStatic ? null : instance); } catch { }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawReadOnlyLikeSerialized(
                            fi.FieldType,
                            ObjectNames.NicifyVariableName(fi.Name),
                            value);
                    }
                }
            }

            // Methods with [Button] — draw after fields (per usual Inspector expectations).
            foreach (var mi in GetButtonMethods(type))
            {
                string label = GetButtonLabel(mi);
                if (GUILayout.Button(label))
                {
                    var target = mi.IsStatic ? null : instance;
                    var uobj = target as Object;
                    if (uobj != null) Undo.RecordObject(uobj, $"Invoke {mi.Name}");

                    try { mi.Invoke(target, null); }
                    catch (Exception ex) { Debug.LogException(ex); }

                    if (uobj != null) EditorUtility.SetDirty(uobj);
                }
            }
        }

        // Catch and draw any SpecialCaseDrawerAttribute (e.g., ReorderableList) and return true if handled.
        private static bool TryDrawSpecialCase(SerializedProperty property)
        {
            // Respect visibility (consistent with your SpecialCasePropertyDrawerBase.OnGUI)
            if (!IsVisibleByMeta(property))
                return true; // visible=false means "handled" by skipping draw

            // Collect special-case attributes on this field
            var specials = PropertyUtility.GetAttributes<SpecialCaseDrawerAttribute>(property);
            if (specials == null || specials.Length == 0)
                return false;

            // Pick first matching special-case drawer and let it render
            foreach (var attr in specials)
            {
                var drawer = attr.GetDrawer(); // mapped in SpecialCaseDrawerAttributeExtensions
                if (drawer != null)
                {
                    // Use layout path: passing default Rect triggers DoLayoutList() in ReorderableListPropertyDrawer
                    drawer.OnGUI(default, property);
                    return true;
                }
            }

            return false;
        }

        // Handle FoldoutAttribute grouping at draw time. Returns true when the group was drawn and this field should be skipped.
        private static bool HandleFoldoutGroup(SerializedProperty property)
        {
            var fold = PropertyUtility.GetAttribute<FoldoutAttribute>(property);
            if (fold == null) return false;

            // Let the mapped validator render the group (first field draws)
            var v = fold.GetValidator();
            v?.ValidateMetaProperty(property);
            return true;
        }

        private static bool HandleBoxGroup(SerializedProperty property)
        {
            var box = PropertyUtility.GetAttribute<BoxGroupAttribute>(property);
            if (box == null) return false;

            var v = box.GetValidator();
            v?.ValidateMetaProperty(property);
            return true;
        }

        private static IEnumerable<FieldInfo> GetFieldsInDeclarationOrder(Type type)
        {
            // Base → Derived; within a type, MetadataToken preserves declaration order.
            var stack = new Stack<Type>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                stack.Push(t);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.DeclaredOnly)
                              .OrderBy(f => f.MetadataToken);
                foreach (var f in fields) yield return f;
            }
        }

        private static bool IsUnitySerializedField(FieldInfo fi)
        {
            if (fi == null) return false;
            if (fi.IsDefined(typeof(NonSerializedAttribute), inherit: true)) return false;
            if (fi.IsDefined(typeof(HideInInspector), inherit: true)) return false;

            // Unity serializes:
            //  - public fields (except [NonSerialized])
            //  - non-public fields with [SerializeField]
            bool isPublicSerialized = fi.IsPublic;
            bool isPrivateSerialized = fi.IsDefined(typeof(SerializeField), inherit: true);
            return isPublicSerialized || isPrivateSerialized;
        }

        private static bool HasAttributeByName(MemberInfo mi, string shortOrFullName)
            => mi.GetCustomAttributes(true).Any(a => a.GetType().Name == shortOrFullName);

        private static IEnumerable<MethodInfo> GetButtonMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.Public | BindingFlags.NonPublic)
                       .Where(m =>
                       {
                           if (m.GetParameters().Length != 0) return false;
                           return m.GetCustomAttributes(true)
                                   .Any(a =>
                                   {
                                       var n = a.GetType().Name;
                                       return n == "Button" || n == "ButtonAttribute";
                                   });
                       });
        }

        private static string GetButtonLabel(MethodInfo mi)
        {
            // Try read "Text" from attribute; fallback to nicified method name.
            foreach (var a in mi.GetCustomAttributes(true))
            {
                var n = a.GetType().Name;
                if (n == "Button" || n == "ButtonAttribute")
                {
                    var p = a.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public);
                    string txt = p?.GetValue(a) as string;
                    if (!string.IsNullOrWhiteSpace(txt)) return txt;
                }
            }
            return ObjectNames.NicifyVariableName(mi.Name);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Generic serialized traversal helper (fallback)
        // ─────────────────────────────────────────────────────────────────────────────

        private static void IterateChildren(SerializedProperty parent, Action<SerializedProperty> drawChild)
        {
            var it = parent.Copy();
            var end = it.GetEndProperty();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                if (it.name == "m_Script") { enterChildren = false; continue; }
                drawChild(it);
                enterChildren = false; // horizontal traversal
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Small helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private static string GetAttrString(Type attrType, object attrInstance, string propName)
        {
            var p = attrType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(attrInstance) as string;
        }

        private static void DrawPropertyField(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            EditorGUI.PropertyField(rect, property, label, includeChildren);
        }

        private static void PropertyField_Implementation(Rect rect, SerializedProperty property, bool includeChildren, PropertyFieldFunction propertyFieldFunction)
        {
            SpecialCaseDrawerAttribute specialCaseAttribute = PropertyUtility.GetAttribute<SpecialCaseDrawerAttribute>(property);
            if (specialCaseAttribute != null)
            {
                specialCaseAttribute.GetDrawer().OnGUI(rect, property);
            }
            else
            {
                // Check if visible
                bool visible = PropertyUtility.IsVisible(property);
                if (!visible)
                {
                    return;
                }
                // Validate (run once here for direct rect-based draws)
                var validatorAttributes = PropertyUtility.GetAttributes<ValidatorAttribute>(property);
                foreach (var va in validatorAttributes)
                {
                    va.GetValidator().ValidateProperty(property);
                }

                EditorGUI.BeginChangeCheck();
                bool enabled = PropertyUtility.IsEnabled(property);
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    propertyFieldFunction.Invoke(rect, property, PropertyUtility.GetLabel(property), includeChildren);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    PropertyUtility.CallOnValueChangedCallbacks(property);
                    PropertyUtility.CallOnValidateCallbacks(property);
                }
            }
        }

        public static float GetIndentLength(Rect sourceRect)
        {
            Rect indentRect = EditorGUI.IndentedRect(sourceRect);
            float indentLength = indentRect.x - sourceRect.x;

            return indentLength;
        }

        private static Object GetAssignableObject(Object obj, SerializedProperty listProp)
        {
            var listType = PropertyUtility.GetPropertyType(listProp);
            var elementType = ReflectionUtility.GetListElementType(listType);
            if (elementType == null || obj == null) return null;

            var objType = obj.GetType();
            if (elementType.IsAssignableFrom(objType)) return obj;

            if (objType == typeof(GameObject))
            {
                if (typeof(Transform).IsAssignableFrom(elementType))
                {
                    var tr = ((GameObject)obj).transform;
                    if (elementType == typeof(RectTransform)) return tr as RectTransform;
                    return tr;
                }
                else if (typeof(MonoBehaviour).IsAssignableFrom(elementType))
                {
                    return ((GameObject)obj).GetComponent(elementType);
                }
            }
            return null;
        }

        public static void BeginBoxGroup_Layout(string label = "")
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }
        }

        public static void EndBoxGroup_Layout()
        {
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Creates a dropdown
        /// </summary>
        /// <param name="rect">The rect the defines the position and size of the dropdown in the inspector</param>
        /// <param name="serializedObject">The serialized object that is being updated</param>
        /// <param name="target">The target object that contains the dropdown</param>
        /// <param name="dropdownField">The field of the target object that holds the currently selected dropdown value</param>
        /// <param name="label">The label of the dropdown</param>
        /// <param name="selectedValueIndex">The index of the value from the values array</param>
        /// <param name="values">The values of the dropdown</param>
        /// <param name="displayOptions">The display options for the values</param>
        public static void Dropdown(
            Rect rect, SerializedObject serializedObject, object target, FieldInfo dropdownField,
            string label, int selectedValueIndex, object[] values, string[] displayOptions)
        {
            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUI.Popup(rect, label, selectedValueIndex, displayOptions);
            object newValue = values[newIndex];

            object dropdownValue = dropdownField.GetValue(target);
            if (dropdownValue == null || !dropdownValue.Equals(newValue))
            {
                Undo.RecordObject(serializedObject.targetObject, "Dropdown");

                // TODO: Problem with structs, because they are value type.
                // The solution is to make boxing/unboxing but unfortunately I don't know the compile time type of the target object
                dropdownField.SetValue(target, newValue);
            }
        }

        public static void Button(UnityEngine.Object target, MethodInfo methodInfo, int index, ref List<object[]> parametersDatas)
        {
            bool visible = ButtonUtility.IsVisible(target, methodInfo);
            if (!visible)
            {
                return;
            }

            if (methodInfo.GetParameters().All(p => p.IsOptional))
            {
                ButtonAttribute buttonAttribute = (ButtonAttribute)methodInfo.GetCustomAttributes(typeof(ButtonAttribute), true)[0];
                string buttonText = string.IsNullOrEmpty(buttonAttribute.Text) ? ObjectNames.NicifyVariableName(methodInfo.Name) : buttonAttribute.Text;

                bool buttonEnabled = ButtonUtility.IsEnabled(target, methodInfo);

                EButtonEnableMode mode = buttonAttribute.SelectedEnableMode;
                buttonEnabled &=
                    mode == EButtonEnableMode.Always ||
                    mode == EButtonEnableMode.Editor && !Application.isPlaying ||
                    mode == EButtonEnableMode.Playmode && Application.isPlaying;

                bool methodIsCoroutine = methodInfo.ReturnType == typeof(IEnumerator);
                if (methodIsCoroutine)
                {
                    buttonEnabled &= (Application.isPlaying ? true : false);
                }

                EditorGUI.BeginDisabledGroup(!buttonEnabled);

                if (GUILayout.Button(buttonText, _buttonStyle))
                {
                    object[] defaultParams = methodInfo.GetParameters().Select(p => p.DefaultValue).ToArray();
                    IEnumerator methodResult = methodInfo.Invoke(target, defaultParams) as IEnumerator;

                    if (!Application.isPlaying)
                    {
                        // Set target object and scene dirty to serialize changes to disk
                        EditorUtility.SetDirty(target);

                        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                        if (stage != null)
                        {
                            // Prefab mode
                            EditorSceneManager.MarkSceneDirty(stage.scene);
                        }
                        else
                        {
                            // Normal scene
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                    }
                    else if (methodResult != null && target is MonoBehaviour behaviour)
                    {
                        behaviour.StartCoroutine(methodResult);
                    }
                }

                EditorGUI.EndDisabledGroup();
            }
            else
            {
                /*string warning = typeof(ButtonAttribute).Name + " works only on methods with no parameters";
                HelpBox_Layout(warning, MessageType.Warning, context: target, logToConsole: true);*/
                var parameters = methodInfo.GetParameters();

                if (parametersDatas[index] == null || parametersDatas[index].Length != parameters.Length)
                {
                    parametersDatas[index] = new object[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        parametersDatas[index][i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
                    }
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    var draw = Parameter_Layout(parameters[i], ref parametersDatas[index][i]);
                    if (!draw)
                    {
                        HelpBox_Layout($"{methodInfo.Name} have parameter not support!", MessageType.Warning, context: target, logToConsole: false);
                        return;
                    }
                }

                ButtonAttribute buttonAttribute = (ButtonAttribute)methodInfo.GetCustomAttributes(typeof(ButtonAttribute), true)[0];
                string buttonText = string.IsNullOrEmpty(buttonAttribute.Text) ? ObjectNames.NicifyVariableName(methodInfo.Name) : buttonAttribute.Text;

                bool buttonEnabled = ButtonUtility.IsEnabled(target, methodInfo);

                EButtonEnableMode mode = buttonAttribute.SelectedEnableMode;
                buttonEnabled &=
                    mode == EButtonEnableMode.Always ||
                    mode == EButtonEnableMode.Editor && !Application.isPlaying ||
                    mode == EButtonEnableMode.Playmode && Application.isPlaying;

                bool methodIsCoroutine = methodInfo.ReturnType == typeof(IEnumerator);
                if (methodIsCoroutine)
                {
                    buttonEnabled &= (Application.isPlaying ? true : false);
                }

                EditorGUI.BeginDisabledGroup(!buttonEnabled);

                if (GUILayout.Button(buttonText, _buttonStyle))
                {
                    IEnumerator methodResult = methodInfo.Invoke(target, parametersDatas[index]) as IEnumerator;

                    if (!Application.isPlaying)
                    {
                        // Set target object and scene dirty to serialize changes to disk
                        EditorUtility.SetDirty(target);

                        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                        if (stage != null)
                        {
                            // Prefab mode
                            EditorSceneManager.MarkSceneDirty(stage.scene);
                        }
                        else
                        {
                            // Normal scene
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                    }
                    else if (methodResult != null && target is MonoBehaviour behaviour)
                    {
                        behaviour.StartCoroutine(methodResult);
                    }
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        public static void NativeProperty_Layout(UnityEngine.Object target, PropertyInfo property)
        {
            object value = property.GetValue(target, null);

            if (value == null)
            {
                string warning = string.Format("{0} is null. {1} doesn't support reference types with null value", ObjectNames.NicifyVariableName(property.Name), typeof(ShowNativePropertyAttribute).Name);
                HelpBox_Layout(warning, MessageType.Warning, context: target);
            }
            else if (!Field_Layout(value, ObjectNames.NicifyVariableName(property.Name)))
            {
                string warning = string.Format("{0} doesn't support {1} types", typeof(ShowNativePropertyAttribute).Name, property.PropertyType.Name);
                HelpBox_Layout(warning, MessageType.Warning, context: target);
            }
        }

        public static void NonSerializedField_Layout(UnityEngine.Object target, FieldInfo field)
        {
            object value = field.GetValue(target);

            if (value == null)
            {
                string warning = string.Format("{0} is null. {1} doesn't support reference types with null value", ObjectNames.NicifyVariableName(field.Name), typeof(ShowNonSerializedFieldAttribute).Name);
                HelpBox_Layout(warning, MessageType.Warning, context: target);
            }
            else if (!Field_Layout(value, ObjectNames.NicifyVariableName(field.Name)))
            {
                string warning = string.Format("{0} doesn't support {1} types", typeof(ShowNonSerializedFieldAttribute).Name, field.FieldType.Name);
                HelpBox_Layout(warning, MessageType.Warning, context: target);
            }
        }

        public static void HorizontalLine(Rect rect, float height, Color color)
        {
            rect.height = height;
            EditorGUI.DrawRect(rect, color);
        }

        public static void HelpBox(Rect rect, string message, MessageType type, UnityEngine.Object context = null, bool logToConsole = false)
        {
            EditorGUI.HelpBox(rect, message, type);

            if (logToConsole)
            {
                DebugLogMessage(message, type, context);
            }
        }

        public static void HelpBox_Layout(string message, MessageType type, UnityEngine.Object context = null, bool logToConsole = false)
        {
            EditorGUILayout.HelpBox(message, type);

            if (logToConsole)
            {
                DebugLogMessage(message, type, context);
            }
        }

        /// <summary>
        /// Draw non-serialized fields and [Button] methods for a nested object instance/type.
        /// Goal: mimic Unity's serialized look (same controls), but read-only.
        /// Supports instance + static; const & readonly are shown as disabled controls.
        /// </summary>
        public static void DrawNestedExtras(object owner)
        {
            if (owner == null) return;

            Type type = owner.GetType();

            // ---- Non-serialized fields with [ShowNonSerializedField] ----
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var fi in fields)
            {
                var showAttr = fi.GetCustomAttribute<ShowNonSerializedFieldAttribute>(inherit: true);
                if (showAttr == null) continue;

                bool isStatic = fi.IsStatic;
                bool isConst = fi.IsLiteral && !fi.IsInitOnly; // const
                bool isReadonly = fi.IsInitOnly;                  // readonly

                object fieldOwner = isStatic ? null : owner;

                object value = null;
                try { value = fi.GetValue(fieldOwner); }
                catch { /* ignore */ }

                // Use Unity-like label; avoid badges so the row looks like a normal serialized field.
                string label = ObjectNames.NicifyVariableName(fi.Name);

                using (new EditorGUI.DisabledScope(true)) // render read-only
                {
                    DrawReadOnlyLikeSerialized(fi.FieldType, label, value);
                }
            }

            // ---- [Button] methods (instance + static) ----
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var mi in methods)
            {
                var btn = mi.GetCustomAttribute<ButtonAttribute>(inherit: true);
                if (btn == null) continue;
                if (mi.GetParameters().Length != 0) continue;

                string text = string.IsNullOrWhiteSpace(btn.Text)
                                ? ObjectNames.NicifyVariableName(mi.Name)
                                : btn.Text;

                if (GUILayout.Button(text))
                {
                    var undoTarget = owner as UnityEngine.Object;
                    if (undoTarget != null) Undo.RecordObject(undoTarget, $"Invoke {mi.Name}");

                    try { mi.Invoke(mi.IsStatic ? null : owner, null); }
                    catch (Exception ex) { Debug.LogException(ex); }

                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                }
            }
        }

        /// <summary>
        /// Renders a value using the same widgets Unity uses for serialized fields,
        /// but as a read-only control so it visually matches the Inspector style.
        /// </summary>
        private static void DrawReadOnlyLikeSerialized(Type t, string label, object value)
        {
            // Null or unknown → fallback to label
            if (t == null)
            {
                EditorGUILayout.LabelField(label, value == null ? "<null>" : value.ToString());
                return;
            }

            // Handle common primitives and Unity types
            if (t == typeof(int))
                EditorGUILayout.IntField(label, value is int iv ? iv : 0);
            else if (t == typeof(float))
                EditorGUILayout.FloatField(label, value is float fv ? fv : 0f);
            else if (t == typeof(double))
                EditorGUILayout.DoubleField(label, value is double dv ? dv : 0d);
            else if (t == typeof(bool))
                EditorGUILayout.Toggle(label, value is bool bv && bv);
            else if (t == typeof(string))
                EditorGUILayout.TextField(label, value as string ?? string.Empty);
            else if (t.IsEnum)
                EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(t));

            // Vectors / Colors / Rects
            else if (t == typeof(Vector2))
                EditorGUILayout.Vector2Field(label, value is Vector2 v2 ? v2 : default);
            else if (t == typeof(Vector2Int))
                EditorGUILayout.Vector2IntField(label, value is Vector2Int v2i ? v2i : default);
            else if (t == typeof(Vector3))
                EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : default);
            else if (t == typeof(Vector3Int))
                EditorGUILayout.Vector3IntField(label, value is Vector3Int v3i ? v3i : default);
            else if (t == typeof(Vector4))
                EditorGUILayout.Vector4Field(label, value is Vector4 v4 ? v4 : default);
            else if (t == typeof(Color))
                EditorGUILayout.ColorField(label, value is Color c ? c : Color.white);
            else if (t == typeof(Rect))
                EditorGUILayout.RectField(label, value is Rect r ? r : default);
            else if (t == typeof(Bounds))
                EditorGUILayout.BoundsField(label, value is Bounds b ? b : default);

            // Quaternions → show as Vector4 (x,y,z,w) to mimic serialized representation
            else if (t == typeof(Quaternion))
            {
                var q = value is Quaternion qv ? qv : Quaternion.identity;
                Vector4 qv4 = new Vector4(q.x, q.y, q.z, q.w);
                EditorGUILayout.Vector4Field(label, qv4);
            }

            // UnityEngine.Object refs (Textures, Materials, ScriptableObjects, etc.)
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                var obj = value as UnityEngine.Object;
                EditorGUILayout.ObjectField(label, obj, t, allowSceneObjects: false);
            }
            else
            {
                // Fallback for other types
                EditorGUILayout.LabelField(label, value == null ? "<null>" : value.ToString());
            }
        }

        public static bool Field_Layout(object value, string label)
        {
            using (new EditorGUI.DisabledScope(disabled: true))
            {
                bool isDrawn = true;
                Type valueType = value.GetType();

                if (valueType == typeof(bool))
                {
                    EditorGUILayout.Toggle(label, (bool)value);
                }
                else if (valueType == typeof(short))
                {
                    EditorGUILayout.IntField(label, (short)value);
                }
                else if (valueType == typeof(ushort))
                {
                    EditorGUILayout.IntField(label, (ushort)value);
                }
                else if (valueType == typeof(int))
                {
                    EditorGUILayout.IntField(label, (int)value);
                }
                else if (valueType == typeof(uint))
                {
                    EditorGUILayout.LongField(label, (uint)value);
                }
                else if (valueType == typeof(long))
                {
                    EditorGUILayout.LongField(label, (long)value);
                }
                else if (valueType == typeof(ulong))
                {
                    EditorGUILayout.TextField(label, ((ulong)value).ToString());
                }
                else if (valueType == typeof(float))
                {
                    EditorGUILayout.FloatField(label, (float)value);
                }
                else if (valueType == typeof(double))
                {
                    EditorGUILayout.DoubleField(label, (double)value);
                }
                else if (valueType == typeof(string))
                {
                    EditorGUILayout.TextField(label, (string)value);
                }
                else if (valueType == typeof(Vector2))
                {
                    EditorGUILayout.Vector2Field(label, (Vector2)value);
                }
                else if (valueType == typeof(Vector3))
                {
                    EditorGUILayout.Vector3Field(label, (Vector3)value);
                }
                else if (valueType == typeof(Vector4))
                {
                    EditorGUILayout.Vector4Field(label, (Vector4)value);
                }
                else if (valueType == typeof(Vector2Int))
                {
                    EditorGUILayout.Vector2IntField(label, (Vector2Int)value);
                }
                else if (valueType == typeof(Vector3Int))
                {
                    EditorGUILayout.Vector3IntField(label, (Vector3Int)value);
                }
                else if (valueType == typeof(Color))
                {
                    EditorGUILayout.ColorField(label, (Color)value);
                }
                else if (valueType == typeof(Bounds))
                {
                    EditorGUILayout.BoundsField(label, (Bounds)value);
                }
                else if (valueType == typeof(Rect))
                {
                    EditorGUILayout.RectField(label, (Rect)value);
                }
                else if (valueType == typeof(RectInt))
                {
                    EditorGUILayout.RectIntField(label, (RectInt)value);
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
                {
                    EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, valueType, true);
                }
                else if (valueType.BaseType == typeof(Enum))
                {
                    EditorGUILayout.EnumPopup(label, (Enum)value);
                }
                else if (valueType.BaseType == typeof(System.Reflection.TypeInfo))
                {
                    EditorGUILayout.TextField(label, value.ToString());
                }
                else
                {
                    isDrawn = false;
                }

                return isDrawn;
            }
        }

        public static bool Parameter_Layout(ParameterInfo parameter, ref object value)
        {
            Type valueType = parameter.ParameterType;
            string label = parameter.Name;

            bool isDrawn = true;

            if (valueType == typeof(bool))
            {
                value = EditorGUILayout.Toggle(label, (bool)value);
            }
            else if (valueType == typeof(short))
            {
                value = EditorGUILayout.IntField(label, (short)value);
            }
            else if (valueType == typeof(ushort))
            {
                value = EditorGUILayout.IntField(label, (ushort)value);
            }
            else if (valueType == typeof(int))
            {
                value = EditorGUILayout.IntField(label, (int)value);
            }
            else if (valueType == typeof(uint))
            {
                value = EditorGUILayout.LongField(label, (uint)value);
            }
            else if (valueType == typeof(long))
            {
                value = EditorGUILayout.LongField(label, (long)value);
            }
            else if (valueType == typeof(ulong))
            {
                value = EditorGUILayout.TextField(label, ((ulong)value).ToString());
            }
            else if (valueType == typeof(float))
            {
                value = EditorGUILayout.FloatField(label, (float)value);
            }
            else if (valueType == typeof(double))
            {
                value = EditorGUILayout.DoubleField(label, (double)value);
            }
            else if (valueType == typeof(string))
            {
                value = EditorGUILayout.TextField(label, (string)value);
            }
            else if (valueType == typeof(Vector2))
            {
                value = EditorGUILayout.Vector2Field(label, (Vector2)value);
            }
            else if (valueType == typeof(Vector3))
            {
                value = EditorGUILayout.Vector3Field(label, (Vector3)value);
            }
            else if (valueType == typeof(Vector4))
            {
                value = EditorGUILayout.Vector4Field(label, (Vector4)value);
            }
            else if (valueType == typeof(Vector2Int))
            {
                value = EditorGUILayout.Vector2IntField(label, (Vector2Int)value);
            }
            else if (valueType == typeof(Vector3Int))
            {
                value = EditorGUILayout.Vector3IntField(label, (Vector3Int)value);
            }
            else if (valueType == typeof(Color))
            {
                value = EditorGUILayout.ColorField(label, (Color)value);
            }
            else if (valueType == typeof(Bounds))
            {
                value = EditorGUILayout.BoundsField(label, (Bounds)value);
            }
            else if (valueType == typeof(Rect))
            {
                value = EditorGUILayout.RectField(label, (Rect)value);
            }
            else if (valueType == typeof(RectInt))
            {
                value = EditorGUILayout.RectIntField(label, (RectInt)value);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
            {
                value = EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, valueType, true);
            }
            else if (valueType.BaseType == typeof(Enum))
            {
                value = EditorGUILayout.EnumPopup(label, (Enum)value);
            }
            else if (valueType.BaseType == typeof(System.Reflection.TypeInfo))
            {
                value = EditorGUILayout.TextField(label, value.ToString());
            }
            else
            {
                isDrawn = false;
            }

            return isDrawn;
        }

        private static void DebugLogMessage(string message, MessageType type, UnityEngine.Object context)
        {
            switch (type)
            {
                case MessageType.None:
                case MessageType.Info:
                    Debug.Log(message, context);
                    break;
                case MessageType.Warning:
                    Debug.LogWarning(message, context);
                    break;
                case MessageType.Error:
                    Debug.LogError(message, context);
                    break;
            }
        }

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = col;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
