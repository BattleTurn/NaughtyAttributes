using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class HideIfMetaPropertyValidator : ShowIfMetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // HideIfAttribute inherits ShowIfAttributeBase with Inverted=true set by the runtime attribute
            // so the base validator already computes the correct visibility.
            return base.ValidateMetaProperty(property);
        }
    }
}