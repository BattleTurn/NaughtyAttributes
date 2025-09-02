using System.Collections.Generic;
using UnityEngine;

namespace NaughtyAttributes.Test
{
    public class ReorderableListTest : MonoBehaviour
    {
        public int[] intArray;

        public List<Vector3> vectorList;

        public List<SomeStruct> structList;
        public GameObject[] gameObjectsList;

        public List<Transform> transformsList;

        public List<MonoBehaviour> monoBehavioursList;
    }

    [System.Serializable]
    public struct SomeStruct
    {
        public int Int;
        public float Float;
        public Vector3 Vector;
    }
}
