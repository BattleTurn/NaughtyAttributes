using System.Collections.Generic;
using UnityEditor;

namespace NaughtyAttributes.Editor
{
    public class LabelMetaPropertyValidator : MetaPropertyValidatorBase
    {
        public override bool ValidateMetaProperty(SerializedProperty property)
        {
            // Label is applied during draw via PropertyUtility.GetLabel; nothing to mutate here
            // Return false to indicate no serialized changes were made
            return false;
        }
    }
}
