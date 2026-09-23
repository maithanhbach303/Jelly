using System;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Levels
{
    [CreateAssetMenu(fileName = "Sequence_", menuName = "Levels/Tray Sequence", order = 0)]
    public class TraySequence : ScriptableObject
    {
        [Serializable]
        public struct CubeSpec
        {
            [Tooltip("Which shape to spawn.")]
            public CubeShapeType shapeType;

            [Tooltip("Optional quarter-turns for the shape layout (0-3).")]
            [Range(0, 3)] public int rotationQuarterTurns;

            [Tooltip("One color per sub-cube slot. Empty = deal colors from the palette.")]
            public CubeColor[] colors;

            [Tooltip("Optional palette override. Null = use the tray's palette.")]
            public CubePalette paletteOverride;
        }

        [Header("Identity")]
        public string sequenceName = "New Sequence";

        [Header("Cubes (in order)")]
        [SerializeField]
        private List<CubeSpec> cubes = new();

        [Header("Loop")]
        [Tooltip("If true, the sequence restarts after the last entry. If false, the tray falls back to random.")]
        public bool loop = false;

        public int Count => cubes != null ? cubes.Count : 0;
        public IReadOnlyList<CubeSpec> Cubes => cubes;
        public CubeSpec Get(int index) => cubes[index];

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(sequenceName)) sequenceName = name;
        }
#endif
    }
}

