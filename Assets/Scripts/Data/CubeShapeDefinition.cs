using System;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Tray
{
    [Serializable]
    public struct ShapeEntry
    {
        public CubeShapeType shape;
        public bool enabled;
        [Min(0f)] public float weight;
    }

    [CreateAssetMenu(fileName = "ShapePool_", menuName = "Cubes/Cube Shape Pool", order = 2)]
    public class CubeShapeDefinition : ScriptableObject
    {
        [SerializeField]
        private List<ShapeEntry> entries = new()
        {
            new ShapeEntry { shape = CubeShapeType.Whole,          enabled = true, weight = 2f },
            new ShapeEntry { shape = CubeShapeType.FourSmall,      enabled = true, weight = 2f },
            new ShapeEntry { shape = CubeShapeType.TwoHalf,        enabled = true, weight = 2f },
            new ShapeEntry { shape = CubeShapeType.HalfAndTwoSmall, enabled = true, weight = 1f },
        };

        [SerializeField] private bool avoidImmediateRepeat = false;

        private int _lastPicked = -1;

        public IReadOnlyList<ShapeEntry> Entries => entries;

        public bool TryPick(out CubeShapeType shape, out int rotation)
        {
            shape = CubeShapeType.Whole;
            rotation = 0;

            float total = 0f;
            foreach (var e in entries) if (e.enabled) total += e.weight;
            if (total <= 0f) return false;

            int picked;
            do
            {
                float r = UnityEngine.Random.value * total;
                float acc = 0f;
                picked = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (!e.enabled) continue;
                    acc += e.weight;
                    if (r <= acc) { picked = i; break; }
                }
            } while (avoidImmediateRepeat && picked == _lastPicked && entries.Count > 1);

            if (picked < 0) picked = 0;
            _lastPicked = picked;
            shape = entries[picked].shape;
            rotation = UnityEngine.Random.Range(0, 4);
            return true;
        }

        public void ResetPicker() => _lastPicked = -1;
    }
}