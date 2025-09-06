using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class BoxGroupTest : MonoBehaviour
    {
        [System.Serializable]
        public sealed class NestedClass
        {
            [BoxGroup("Nested Ints")]
            public int nestedInt0;
            
            public int nestedInt1;
        }

        [BoxGroup("Integers")]
        public int int0;
        [BoxGroup("Integers")]
        public int int1;

        [BoxGroup("Floats")]
        public float float0;
        [BoxGroup("Floats")]
        public float float1;

        [BoxGroup("Sliders")]
        [MinMaxSlider(0, 1)]
        public Vector2 slider0;
        [BoxGroup("Sliders")]
        [MinMaxSlider(0, 1)]
        public Vector2 slider1;

        public string str0;
        public string str1;

        [BoxGroup]
        public Transform trans0;
        [BoxGroup]
        public Transform trans1;

        [BoxGroup("Nested Classes")]
        public NestedClass nestedClass0;

        [BoxGroup("Nested Classes")]
        public NestedClass nestedClass1;
    }
}
