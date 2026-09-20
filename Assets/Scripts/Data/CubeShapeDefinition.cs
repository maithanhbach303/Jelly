using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    [Serializable]
    public struct ShapeBlock
    {
        [Tooltip("Center of this block, in cell units, relative to the shape's bottom-left corner.")]
        public Vector2 position;

        [Tooltip("Footprint of this block in cell units (width × height).")]
        public Vector2 size;

        [Tooltip("Prefab to spawn for this block. Owns its own visuals + logic.")]
        public GameObject prefab;

        public ShapeBlock(Vector2 position, Vector2 size, GameObject prefab)
        {
            this.position = position;
            this.size = size;
            this.prefab = prefab;
        }
    }

    [CreateAssetMenu(fileName = "Shape_", menuName = "Cubes/Cube Shape", order = 0)]
    public class CubeShapeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string shapeName = "New Shape";

        [Header("Blocks (in cell units)")]
        [Tooltip("Each block is a slot in the shape. Position/size are in cell units; " +
                 "prefab is the sub-cube to instantiate there.")]
        public List<ShapeBlock> blocks = new();

        // ----- Derived data -----

        public Vector2 ShapeSize()
        {
            if (blocks == null || blocks.Count == 0) return Vector2.zero;

            float maxX = 0f, maxY = 0f;
            foreach (var b in blocks)
            {
                maxX = Mathf.Max(maxX, b.position.x + b.size.x * 0.5f);
                maxY = Mathf.Max(maxY, b.position.y + b.size.y * 0.5f);
            }
            return new Vector2(maxX, maxY);
        }

        public Vector2 ShapeCenter()
        {
            var s = ShapeSize();
            return new Vector2(s.x * 0.5f, s.y * 0.5f);
        }

#if UNITY_EDITOR
        private void OnValidate() { /* nothing to cache for now */ }
#endif
    }
}