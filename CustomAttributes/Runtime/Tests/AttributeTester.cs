using UnityEngine;
using CustomAttributes.Core;

namespace CustomAttributes.Tests
{
    public class AttributeTester : MonoBehaviour
    {
        [UIReadOnly]
        public int testField;
        [Space]
        [Header("Another Field")]
        [SerializeField]
        private string anotherField;
        [UIButton("PrintHello")]
        public string buttonField;

        private void PrintHello()
        {
            Debug.Log("Hello from UIButton!");
        }
    }
}