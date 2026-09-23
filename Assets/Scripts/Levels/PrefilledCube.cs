using System;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Levels
{
    [Serializable]
    public struct PrefilledCube
    {
        [Tooltip("Which board cell (grid coordinates).")]
        public Vector2Int cell;

        [Tooltip("Which shape to build. Uses the shape's default (non-random) layout.")]
        public CubeShapeType shapeType;

        [Tooltip("Optional quarter-turns for the shape layout (0-3).")]
        [Range(0, 3)] public int rotationQuarterTurns;

        [Tooltip("One color per sub-cube slot. Fewer entries than slots = extras use the last color. " +
                 "Empty = deal colors from the palette.")]
        public CubeColor[] colors;

        [Tooltip("Optional palette override. Null = use the level's palette.")]
        public CubePalette paletteOverride;
    }
}