using UnityEngine;
using CustomAttributes.Runtime;

namespace CustomAttributes.Tests
{
    public class AttributeTester : MonoBehaviour
    {
        // [Test("Hello from TestAttribute!")]
        [UIReadOnly]
        public int testField;
        [Space]
        [Header("Another Field")]
        [SerializeField]
        private string anotherField;
        [UIButton("PrintHello")]
        public string buttonField; // field này sẽ thành button trong Inspector

        private void PrintHello()
        {
            Debug.Log("Hello from UIButton!");
        }
    }
}