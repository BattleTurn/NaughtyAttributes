using System;
using UnityEngine;

namespace CustomAttributes.Runtime
{
    [CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_STYLE_PATH + "/" + nameof(UIReadOnlyStyle), fileName = nameof(UIReadOnlyStyle))]
    public class UIReadOnlyStyle : UIStyle
    {
        public override Type Type => typeof(UIReadOnlyAttribute);
    }
}