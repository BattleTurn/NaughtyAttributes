using System.Collections.Generic;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class LabelMetaPropertyValidator : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // Implement validation logic for LabelAttribute
            return true;
        }
    }
}
