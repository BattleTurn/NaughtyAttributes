
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class EnableIfMetaPropertyValidator : EnableIfMetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // Custom validation logic for EnableIfMetaProperty
            return base.ValidateMetaProperty(property);
        }
    }
}