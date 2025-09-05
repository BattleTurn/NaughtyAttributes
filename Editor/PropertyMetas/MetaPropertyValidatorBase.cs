using System;
using System.Collections.Generic;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public abstract class MetaPropertyValidatorBase
    {
        public abstract bool ValidateMetaProperty(SerializedProperty property);
    }

    public static class MetaAttributeExtensions
    {
        private static Dictionary<Type, MetaPropertyValidatorBase> _metaValidatorsByAttributeType;

        static MetaAttributeExtensions()
        {
            _metaValidatorsByAttributeType = new Dictionary<Type, MetaPropertyValidatorBase>();
            _metaValidatorsByAttributeType[typeof(FoldoutAttribute)] = new FoldoutMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(BoxGroupAttribute)] = new BoxGroupMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(LabelAttribute)] = new LabelMetaPropertyValidator();
            // Enable/Disable/ShowIf can be declared via base or concrete types; register base handlers
            _metaValidatorsByAttributeType[typeof(EnableIfAttributeBase)] = new EnableIfMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(ShowIfAttributeBase)] = new ShowIfMetaPropertyValidatorBase();
            _metaValidatorsByAttributeType[typeof(OnValueChangedAttribute)] = new OnValueChangedMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(OnValidateAttribute)] = new OnValidateMetaPropertyValidator();
        }

        public static MetaPropertyValidatorBase GetValidator(this MetaAttribute attr)
        {
            if (attr == null) return null;
            var t = attr.GetType();
            // Try exact, then walk base types until MetaAttribute
            while (t != null && typeof(MetaAttribute).IsAssignableFrom(t))
            {
                if (_metaValidatorsByAttributeType.TryGetValue(t, out var v))
                    return v;
                t = t.BaseType;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Centralized helpers so NaughtyEditorGUI stays lean
        // ─────────────────────────────────────────────────────────────────────

        public static bool IsVisible(SerializedProperty property)
        {
            if (property == null) return false;
            var show = PropertyUtility.GetAttribute<ShowIfAttributeBase>(property);
            if (show == null) return true;
            var v = show.GetValidator();
            return v != null && v.ValidateMetaProperty(property);
        }

        public static bool IsEnabled(SerializedProperty property)
        {
            if (property == null) return false;
            var readOnly = PropertyUtility.GetAttribute<ReadOnlyAttribute>(property);
            if (readOnly != null) return false;

            var enable = PropertyUtility.GetAttribute<EnableIfAttributeBase>(property);
            if (enable == null) return true;
            var v = enable.GetValidator();
            return v == null || v.ValidateMetaProperty(property);
        }

        public static void RunAfterChangeCallbacks(SerializedProperty property)
        {
            if (property == null) return;

            var onValue = PropertyUtility.GetAttribute<OnValueChangedAttribute>(property);
            if (onValue != null)
            {
                var v = onValue.GetValidator();
                v?.ValidateMetaProperty(property);
            }

            var onValidate = PropertyUtility.GetAttribute<OnValidateAttribute>(property);
            if (onValidate != null)
            {
                var v = onValidate.GetValidator();
                v?.ValidateMetaProperty(property);
            }
        }

        // Returns true when any group attribute drew content so the caller should skip default drawing
        public static bool DrawGroupsIfAny(SerializedProperty property, bool allowFoldout, bool allowBox)
        {
            bool consumed = false;

            if (allowFoldout)
            {
                var fold = PropertyUtility.GetAttribute<FoldoutAttribute>(property);
                if (fold != null)
                {
                    var v = fold.GetValidator();
                    v?.ValidateMetaProperty(property);
                    consumed = true;
                }
            }

            if (!consumed && allowBox)
            {
                var box = PropertyUtility.GetAttribute<BoxGroupAttribute>(property);
                if (box != null)
                {
                    var v = box.GetValidator();
                    v?.ValidateMetaProperty(property);
                    consumed = true;
                }
            }

            return consumed;
        }

        public struct MetaProcessResult
        {
            public bool visible;
            public bool enabled;
            public bool consumedByGroup;
        }

        public static MetaProcessResult ProcessAllMetas(SerializedProperty property, bool allowFoldoutGroup, bool allowBoxGroup)
        {
            var result = new MetaProcessResult { visible = false, enabled = false, consumedByGroup = false };
            if (property == null) return result;

            // 1) Visibility
            result.visible = IsVisible(property);
            if (!result.visible) return result;

            // 2) Groups (Foldout → BoxGroup)
            result.consumedByGroup = DrawGroupsIfAny(property, allowFoldoutGroup, allowBoxGroup);
            if (result.consumedByGroup) return result;

            // 3) Enabled
            result.enabled = IsEnabled(property);
            return result;
        }
    }
}
