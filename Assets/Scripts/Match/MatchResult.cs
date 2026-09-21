using System.Collections.Generic;
using MyGame.Interaction;

namespace MyGame.Match
{
    public class MatchResult
    {
        public CubeColor Color { get; }
        public IReadOnlyList<SubCube> Blocks { get; }
        public bool IsHorizontal { get; }
        public bool IsVertical { get; }
        public int Count => Blocks.Count;

        public MatchResult(CubeColor color, List<SubCube> blocks, bool horizontal, bool vertical)
        {
            Color = color;
            Blocks = blocks;
            IsHorizontal = horizontal;
            IsVertical = vertical;
        }

        public override string ToString()
            => $"{Color} × {Count} ({(IsHorizontal ? "H" : IsVertical ? "V" : "?")})";
    }
}