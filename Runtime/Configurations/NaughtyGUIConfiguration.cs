using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes
{
#if UNITY_EDITOR
    /// <summary>
    /// Configuration ScriptableObject for NaughtyAttributes GUI settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NaughtyGUIConfiguration", menuName = "NaughtyAttributes/GUI Configuration")]
    public class NaughtyGUIConfiguration : ScriptableObject
    {
        private const string RESOURCE_PATH = "Plugins/NaughtyAttributes/GUIConfiguration";

        private static NaughtyGUIConfiguration _instance;

        [Header("Reorderable List Settings")]
        [SerializeField]
        private Color _reorderLightColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField]
        private Color _reorderDarkColor = new Color(0.4f, 0.4f, 0.4f);
        [Header("Element Settings")]
        [SerializeField]
        private Color _evenLightColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField]
        private Color _evenDarkColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField]
        private Color _oddLightColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        [SerializeField]
        private Color _oddDarkColor = new Color(0.20f, 0.20f, 0.20f, 1f);

        public static NaughtyGUIConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:NaughtyGUIConfiguration");
                    if (guids != null && guids.Length > 0)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<NaughtyGUIConfiguration>(assetPath);
                    }

                    // 2. If not found, try to load from Resources
                    if (_instance == null)
                    {
                        _instance = Resources.Load<NaughtyGUIConfiguration>(RESOURCE_PATH);
                    }

                    // 3. If still not found, create and save a new asset at RESOURCE_PATH
                    if (_instance == null)
                    {
                        _instance = CreateInstance<NaughtyGUIConfiguration>();
                        string assetDir = System.IO.Path.GetDirectoryName("Assets/" + RESOURCE_PATH);
                        if (!AssetDatabase.IsValidFolder(assetDir))
                        {
                            System.IO.Directory.CreateDirectory(assetDir);
                            AssetDatabase.Refresh();
                        }
                        string assetPath = "Assets/" + RESOURCE_PATH + ".asset";
                        AssetDatabase.CreateAsset(_instance, assetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                return _instance;
            }
        }

        private void OnEnable()
        {
            hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInBuild;
        }

        #region Public API Methods
        public Color GetReorderIconColor()
        {
            return EditorGUIUtility.isProSkin ? _reorderDarkColor : _reorderLightColor;
        }

        public Color GetElementColor(int index)
        {
            if (index % 2 == 0)
                return EditorGUIUtility.isProSkin ? _evenDarkColor : _evenLightColor;
            return EditorGUIUtility.isProSkin ? _oddDarkColor : _oddLightColor;
        }
        #endregion
    }
#endif
}
