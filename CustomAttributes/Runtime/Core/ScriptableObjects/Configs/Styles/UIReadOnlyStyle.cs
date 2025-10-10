using System;
using UnityEngine;

namespace StylizeAttributes.Core
{
    [CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_STYLE_PATH + "/" + nameof(UIReadOnlyStyle), fileName = nameof(UIReadOnlyStyle))]
    public class UIReadOnlyStyle : UIStyle<ReadOnlyAttribute>
    {
    }
}