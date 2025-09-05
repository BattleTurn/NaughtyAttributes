
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class DisableIfMetaPropertyValidator : EnableIfMetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // DisableIfAttribute inherits EnableIfAttributeBase with Inverted=true set by the runtime attribute,
            // so the base validator already computes the correct enabled state.
            return base.ValidateMetaProperty(property);
        }
    }
}