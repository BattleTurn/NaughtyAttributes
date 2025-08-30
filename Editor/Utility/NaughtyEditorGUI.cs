using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class NaughtyEditorGUI
    {
        public const float IndentLength = 15.0f;
        public const float HorizontalSpacing = 2.0f;

        private static GUIStyle _buttonStyle = new GUIStyle(GUI.skin.button) { richText = true };

        private delegate void PropertyFieldFunction(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren);

        public static void PropertyField(Rect rect, SerializedProperty property, bool includeChildren)
        {
            PropertyField_Implementation(rect, property, includeChildren, DrawPropertyField);
        }

        /// <summary>
        /// Draw a property and (optionally) its children using the Naughty pipeline.
        /// No Unity built-in recursion is used; we recurse manually so nested members
        /// get full Naughty behavior (callbacks, validators, extras).
        /// </summary>
        public static void PropertyField_Layout(SerializedProperty property, bool includeChildren)
        {
            if (!ShouldDraw(property)) return;

            var (owner, field) = ResolveOwnerAndField(property);

            // 1) Draw THIS node (single line; no Unity recursion)
            bool changed = DrawThisNode(property);

            // 2) If changed → commit and fire OnValueChanged callbacks
            if (changed)
            {
                property.serializedObject.ApplyModifiedProperties();
                InvokeOnValueChanged(owner, field, property);
            }

            // 3) Validate THIS node and show a helpbox if invalid
            ValidateThisNode(owner, field, property);

            // 4) Manually recurse children (each child repeats steps 1–3)
            if (!includeChildren || !property.hasVisibleChildren || !property.isExpanded) return;

            int savedIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = savedIndent + 1;

            IterateChildren(property, child =>
            {
                if (child.name == "m_Script") return;
                PropertyField_Layout(child.Copy(), includeChildren: true);
            });

            // 5) After serialized children, draw nested extras (ShowNonSerializedField + Button)
            DrawExtrasForContainer(property);

            EditorGUI.indentLevel = savedIndent;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 0: Gate & resolution
        // ─────────────────────────────────────────────────────────────────────────────

        private static bool ShouldDraw(SerializedProperty property)
        {
            if (property == null) return false;
            if (!PropertyUtility.IsVisible(property)) return false;
            return true;
        }

        private static (object owner, FieldInfo field) ResolveOwnerAndField(SerializedProperty property)
        {
            object owner = PropertyUtility.GetTargetObjectWithProperty(property);
            FieldInfo fi = owner != null ? ReflectionUtility.GetField(owner, property.name) : null;
            return (owner, fi);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 1: Draw THIS node (single line)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws only this property (no recursion). Returns true if value changed.
        /// Uses one reserved rect to avoid extra spacing.
        /// </summary>
        private static bool DrawThisNode(SerializedProperty property)
        {
            bool enabled = PropertyUtility.IsEnabled(property);

            float h = EditorGUI.GetPropertyHeight(property, includeChildren: false);
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: true, height: h);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginProperty(rect, new GUIContent(property.displayName), property);
                EditorGUI.BeginChangeCheck();

                EditorGUI.PropertyField(rect, property, includeChildren: false);

                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.EndProperty();
                return changed;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 2: OnValueChanged
        // ─────────────────────────────────────────────────────────────────────────────

        private static void InvokeOnValueChanged(object owner, FieldInfo fi, SerializedProperty property)
        {
            if (owner == null || fi == null) return;

            foreach (var a in fi.GetCustomAttributes(true))
            {
                var at = a.GetType();
                var name = at.Name;
                if (name != "OnValueChanged" && name != "OnValueChangedAttribute") continue;

                string cb = GetAttributeString(at, a, "CallbackName");
                if (string.IsNullOrEmpty(cb)) continue;

                MethodInfo mi =
                    ReflectionUtility.GetMethod(owner, cb) ??
                    owner.GetType().GetMethod(cb, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (mi == null) continue;

                try
                {
                    if (mi.GetParameters().Length == 1)
                    {
                        object current = PropertyUtility.GetTargetObjectOfProperty(property);
                        mi.Invoke(owner, new[] { current });
                    }
                    else
                    {
                        mi.Invoke(owner, null);
                    }
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 3: ValidateInput
        // ─────────────────────────────────────────────────────────────────────────────

        private static void ValidateThisNode(object owner, FieldInfo fi, SerializedProperty property)
        {
            if (owner == null || fi == null) return;

            var attr = fi.GetCustomAttributes(true)
                         .FirstOrDefault(a =>
                         {
                             var n = a.GetType().Name;
                             return n == "ValidateInput" || n == "ValidateInputAttribute";
                         });

            if (attr == null) return;

            string cb = GetAttributeString(attr.GetType(), attr, "CallbackName");
            string msg = GetAttributeString(attr.GetType(), attr, "Message");

            bool isValid = true;

            if (!string.IsNullOrEmpty(cb))
            {
                MethodInfo vm =
                    ReflectionUtility.GetMethod(owner, cb) ??
                    owner.GetType().GetMethod(cb, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (vm != null)
                {
                    try
                    {
                        object result;
                        if (vm.GetParameters().Length == 1)
                        {
                            object current = PropertyUtility.GetTargetObjectOfProperty(property);
                            result = vm.Invoke(owner, new[] { current });
                        }
                        else
                        {
                            result = vm.Invoke(owner, null);
                        }

                        if (result is bool b) isValid = b;
                        else if (result is string s)
                        {
                            isValid = string.IsNullOrEmpty(s);
                            if (!isValid && !string.IsNullOrEmpty(s)) msg = s;
                        }
                    }
                    catch (Exception ex)
                    {
                        isValid = false;
                        if (string.IsNullOrEmpty(msg)) msg = ex.Message;
                    }
                }
            }

            if (!isValid)
            {
                Rect helpRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(helpRect, string.IsNullOrEmpty(msg) ? "Validation failed" : msg, MessageType.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 4: Recurse children
        // ─────────────────────────────────────────────────────────────────────────────

        private static void IterateChildren(SerializedProperty parent, Action<SerializedProperty> drawChild)
        {
            var it = parent.Copy();
            var end = it.GetEndProperty();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                drawChild(it);
                enterChildren = false; // horizontal traversal
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Step 5: Draw extras for nested containers
        // ─────────────────────────────────────────────────────────────────────────────

        private static void DrawExtrasForContainer(SerializedProperty property)
        {
            // We draw extras based on the actual instance backing this node.
            object instance = PropertyUtility.GetTargetObjectOfProperty(property);
            if (instance == null) return;

            // Slight indent so extras align like nested fields
            int prev = EditorGUI.indentLevel;
            EditorGUI.indentLevel = prev + 1;

            DrawNestedExtras(instance); // implemented elsewhere in this partial (ShowNonSerializedField + Button)

            EditorGUI.indentLevel = prev;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Small helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private static string GetAttributeString(Type attrType, object attrInstance, string propName)
        {
            var p = attrType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(attrInstance) as string;
        }

        private static void DrawPropertyField(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            EditorGUI.PropertyField(rect, property, label, includeChildren);
        }

        private static void DrawPropertyField_Layout(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            EditorGUILayout.PropertyField(property, label, includeChildren);
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

                // Validate
                ValidatorAttribute[] validatorAttributes = PropertyUtility.GetAttributes<ValidatorAttribute>(property);
                foreach (var validatorAttribute in validatorAttributes)
                {
                    validatorAttribute.GetValidator().ValidateProperty(property);
                }

                // Check if enabled and draw
                EditorGUI.BeginChangeCheck();
                bool enabled = PropertyUtility.IsEnabled(property);

                using (new EditorGUI.DisabledScope(disabled: !enabled))
                {
                    propertyFieldFunction.Invoke(rect, property, PropertyUtility.GetLabel(property), includeChildren);
                }

                // Call OnValueChanged callbacks
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
        /// Minimal inline clamping for [MinValue] / [MaxValue] if no central ValidatorUtility exists.
        /// Extend this as needed for your other validators.
        /// </summary>
        public static void ApplyBasicMinMaxValidation(SerializedProperty property)
        {
            // Try to read attributes off the backing FieldInfo
            var target = PropertyUtility.GetTargetObjectWithProperty(property);
            if (target == null) return;

            var fi = ReflectionUtility.GetField(target, property.name);
            if (fi == null) return;

            var minAttr = fi.GetCustomAttribute<MinValueAttribute>(inherit: true);
            var maxAttr = fi.GetCustomAttribute<MaxValueAttribute>(inherit: true);

            if (minAttr == null && maxAttr == null) return;

            float? min = minAttr?.MinValue;
            float? max = maxAttr?.MaxValue;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    {
                        int v = property.intValue;
                        if (min.HasValue) v = Mathf.Max(v, Mathf.RoundToInt(min.Value));
                        if (max.HasValue) v = Mathf.Min(v, Mathf.RoundToInt(max.Value));
                        if (v != property.intValue) property.intValue = v;
                        break;
                    }
                case SerializedPropertyType.Float:
                    {
                        float v = property.floatValue;
                        if (min.HasValue) v = Mathf.Max(v, min.Value);
                        if (max.HasValue) v = Mathf.Min(v, max.Value);
                        if (!Mathf.Approximately(v, property.floatValue)) property.floatValue = v;
                        break;
                    }
                case SerializedPropertyType.Vector2:
                    {
                        Vector2 v = property.vector2Value;
                        if (min.HasValue) v = Vector2.Max(v, new Vector2(min.Value, min.Value));
                        if (max.HasValue) v = Vector2.Min(v, new Vector2(max.Value, max.Value));
                        if (v != property.vector2Value) property.vector2Value = v;
                        break;
                    }
                    // Add other types if needed
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
