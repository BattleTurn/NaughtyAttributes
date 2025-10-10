using System;
using UnityEngine;

namespace StylizeAttributes.Core
{
    [CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_STYLE_PATH + "/" + nameof(UIButtonStyle), fileName = nameof(UIButtonStyle))]
    public class UIButtonStyle : UIStyle<ButtonAttribute>
    {
    }
}
