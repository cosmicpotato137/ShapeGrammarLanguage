using System;
using System.Collections.Generic;
using UnityEngine;

namespace cosmicpotato.sgl
{
    [CreateAssetMenu(fileName = "New Shape", menuName = "Shape Grammar/Add shape type")]
    public class Shape : ScriptableObject
    {
        //public string name;
        public Mesh mesh;
        public Material material;
    }
}