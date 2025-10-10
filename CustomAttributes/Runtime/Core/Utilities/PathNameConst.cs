namespace StylizeAttributes.Core
{
    /// <summary>
    /// Contains constant path names for organizing ScriptableObject assets in the Unity Editor.
    /// </summary>
    public static class PathNameConst
    {
        public const string ROOT = "CustomUI";
        public const string UI_CONFIG_PATH = ROOT + "/Configs";
        public const string UI_CONFIG_STYLE_PATH = UI_CONFIG_PATH + "/Styles";
        public const string PACKAGE_PATH = "Packages/NaughtyAttributes/CustomAttributes/Editor/AttributeDrawers/UI";
        public const string PACKAGE_UXML_PATH = PACKAGE_PATH + "/UXML";
        public const string PACKAGE_USS_PATH = PACKAGE_PATH + "/Styles";
    }
}