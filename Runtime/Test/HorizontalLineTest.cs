using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class HorizontalLineTest : MonoBehaviour
    {
    [HorizontalLine(0f, 0f, 0f)] // Black
        [Header("Black")]
    [HorizontalLine(0f, 0.529f, 0.741f)] // Blue (0,135,189)
        [Header("Blue")]
    [HorizontalLine(0.502f, 0.502f, 0.502f)] // Gray (128,128,128)
        [Header("Gray")]
    [HorizontalLine(0.384f, 0.784f, 0.310f)] // Green (98,200,79)
        [Header("Green")]
    [HorizontalLine(0.294f, 0f, 0.510f)] // Indigo (75,0,130)
        [Header("Indigo")]
    [HorizontalLine(1f, 0.502f, 0f)] // Orange (255,128,0)
        [Header("Orange")]
    [HorizontalLine(1f, 0.596f, 0.796f)] // Pink (255,152,203)
        [Header("Pink")]
    [HorizontalLine(1f, 0f, 0.247f)] // Red (255,0,63)
        [Header("Red")]
    [HorizontalLine(0.502f, 0f, 1f)] // Violet (128,0,255)
        [Header("Violet")]
    [HorizontalLine(1f, 1f, 1f)] // White
        [Header("White")]
    [HorizontalLine(1f, 0.827f, 0f)] // Yellow (255,211,0)
        [Header("Yellow")]
        [HorizontalLine(10.0f)]
        [Header("Thick")]
        public int line0;

        public HorizontalLineNest1 nest1;
    }

    [System.Serializable]
    public class HorizontalLineNest1
    {
        [HorizontalLine]
        public int line1;

        public HorizontalLineNest2 nest2;
    }

    [System.Serializable]
    public class HorizontalLineNest2
    {
        [HorizontalLine]
        public int line2;
    }
}
