using System.Collections.Generic;
using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class ReorderableListTest : MonoBehaviour
    {
        public int[] intArray;
        [Space]
        public List<Vector3> vectorList;
        [Space]
        [Space]
        public List<SomeStruct> structList;
        [Space]
        [Space]
        [Space]
        [SerializeField]
        private Nested nested;
        [SerializeField]
        private Nested2 nested2;
        [Space]
        [Space]
        [Space]
        [Space]
        public List<Transform> transformsList;
        [Space]
        [Space]
        [Space]
        [Space]
        [Space]
        public List<MonoBehaviour> monoBehavioursList;
        public List<Sprite> spritesList;
    }

    [System.Serializable]
    public struct SomeStruct
    {
        public int Int;
        public float Float;
        public Vector3 Vector;
    }

    [System.Serializable]
    sealed class Nested
    {
        [SerializeField]
        private List<int> _ints;
    }

    [System.Serializable]
    sealed class Nested2
    {
        [SerializeField]
        private List<int> _ints;
        [SerializeField]
        private Nested _nested;
    }
}
