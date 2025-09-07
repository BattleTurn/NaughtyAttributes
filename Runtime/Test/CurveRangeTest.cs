using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class CurveRangeTest : MonoBehaviour
    {
    [CurveRange(0f, 0f, 1f, 1f, 1f, 1f, 0f)] // yellow
        public AnimationCurve[] curves;

    [CurveRange(-1, -1, 1, 1, 1f, 0f, 0f)] // red
        public AnimationCurve curve;

    [CurveRange(0f, 0f, 1f, 1f, 1f, 0.5f, 0f)] // orange (custom range + color)
        public AnimationCurve curve1;

        [CurveRange(0, 0, 10, 10)]
        public AnimationCurve curve2;

        public CurveRangeNest1 nest1;

        [System.Serializable]
        public class CurveRangeNest1
        {
            [CurveRange(0, 0, 1, 1, 0f, 1f, 0f)] // green
            public AnimationCurve curve;

            public CurveRangeNest2 nest2;
        }

        [System.Serializable]
        public class CurveRangeNest2
        {
            [CurveRange(0, 0, 5, 5, 0f, 0f, 1f)] // blue
            public AnimationCurve curve;
        }
    }
}
