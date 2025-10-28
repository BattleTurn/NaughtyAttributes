using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace StylizeAttributes.Core
{
    [CreateAssetMenu(menuName = PathNameConst.UI_CONFIG_STYLE_PATH + "/" + nameof(UIListStyle), fileName = nameof(UIListStyle))]
    public class UIListStyle : UIStyle<IList>
    {
        public VisualTreeAsset element_uxml;
        public StyleSheet element_uss;
    }
}
