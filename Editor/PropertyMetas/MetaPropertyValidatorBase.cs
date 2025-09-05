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
    }
}
