using System;
using UnityEngine;

namespace MyGame.Interaction
{
    public enum CubeColor
    {
        None = 0,
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange,
        Cyan,
        Pink,
    }

    [Serializable]
    public struct CubeColorEntry
    {
        public CubeColor color;
        public Color rgb;
        public Material material;
        public string displayName;
    }
}