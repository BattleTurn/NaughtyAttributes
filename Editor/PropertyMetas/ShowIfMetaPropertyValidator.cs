using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class ShowIfMetaPropertyValidator : ShowIfMetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // Delegate to base ShowIf logic (supports enum/boolean conditions and inversion via attribute)
            return base.ValidateMetaProperty(property);
        }
    }
}