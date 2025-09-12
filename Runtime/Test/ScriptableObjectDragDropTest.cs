using System.Collections.Generic;
using UnityEngine;

namespace NaughtyAttributes.Test
{
    [CreateAssetMenu(fileName = "ScriptableObjectDragDropTest", menuName = "NaughtyAttributes/ScriptableObject Drag Drop Test")]
    public class ScriptableObjectDragDropTest : ScriptableObject
    {
        [Header("ScriptableObject Arrays")]
        [Expandable]
        public List<ScriptableObject> genericScriptableObjects = new();
        
        [Expandable]
        public List<_TestScriptableObjectA> specificScriptableObjects = new();
        
        [Header("Mixed Object Arrays")]
        [Expandable]
        public List<Object> mixedObjects = new();
        
        [Header("Single ScriptableObject")]
        [Expandable]
        public ScriptableObject singleScriptable;
        
        [Expandable]
        public _TestScriptableObjectA specificScriptable;
    }
}