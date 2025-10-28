using UnityEngine;
using StylizeAttributes.Core;
using System.Collections.Generic;

namespace StylizeAttributes.Tests
{
    public class AttributeTester : MonoBehaviour
    {
        [ReadOnly]
        public int testField;
        [Space]
        [Header("Another Field")]
        [SerializeField]
        private string anotherField;

        public List<Vector3> vectorList;

        [Button("PrintHello")]
        private void PrintHello()
        {
            Debug.Log("Hello from UIButton!");
        }
    }
}