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
            _metaValidatorsByAttributeType[typeof(LabelAttribute)] = new LabelMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(EnableIfAttribute)] = new EnableIfMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(OnValueChangedAttribute)] = new OnValueChangedMetaPropertyValidator();
            _metaValidatorsByAttributeType[typeof(OnValidateAttribute)] = new OnValidateMetaPropertyValidator();
        }

        public static MetaPropertyValidatorBase GetValidator(this MetaAttribute attr)
        {
            MetaPropertyValidatorBase validator;
            if (_metaValidatorsByAttributeType.TryGetValue(attr.GetType(), out validator))
            {
                return validator;
            }
            else
            {
                return null;
            }
        }
    }
}
