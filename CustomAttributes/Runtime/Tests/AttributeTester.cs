using UnityEngine;
using StylizeAttributes.Core;

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

        [Button("PrintHello")]
        private void PrintHello()
        {
            Debug.Log("Hello from UIButton!");
        }
    }
}