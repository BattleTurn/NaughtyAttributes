using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class FoldoutTest : MonoBehaviour
    {
        [System.Serializable]
        internal sealed class NestedClass
        {
            [Foldout("Nested Ints")]
            public int nestedInt0;
            [Foldout("Nested Ints")]
            public int nestedInt1;
        }

        [System.Serializable]
        private sealed class NestedClass2
        {
            [Foldout("Nested Ints")]
            public int nestedInt0;
            [Foldout("Nested Ints")]
            public NestedClass nestedInt1;
        }

        [Foldout("Integers")]
        public int int0;
        [Foldout("Integers")]
        public int int1;
        [Foldout("Integers")]
        public int int2;

        [Foldout("Floats")]
        public float float0;
        [Foldout("Floats")]
        public float float1;

        [Foldout("Sliders")]
        [MinMaxSlider(0, 1)]
        public Vector2 slider0;
        [Foldout("Sliders")]
        [MinMaxSlider(0, 1)]
        public Vector2 slider1;

        public string str0;
        public string str1;

        [Foldout("Transforms")]
        public Transform trans0;
        [Foldout("Transforms")]
        public Transform trans1;

        [Foldout("Nested Classes")]
        [SerializeField]
        internal NestedClass nestedClass0;

        [Foldout("Nested Classes")]
        [SerializeField]
        private NestedClass2 nestedClass1;
    }
}
