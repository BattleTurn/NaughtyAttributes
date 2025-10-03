using System;
using UnityEngine;

namespace CustomAttributes.Runtime
{
    [CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_STYLE_PATH + "/" + nameof(UIButtonStyle), fileName = nameof(UIButtonStyle))]
    public class UIButtonStyle : UIStyle
    {
        public override Type Type => typeof(UIButtonAttribute);
    }
}
