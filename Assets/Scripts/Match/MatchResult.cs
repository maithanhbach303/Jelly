using System.Collections.Generic;
using MyGame.Interaction;

namespace MyGame.Match
{
    public class MatchResult
    {
        public CubeColor Color { get; }
        public IReadOnlyList<SubCube> Blocks { get; }
        public int Count => Blocks.Count;
        public bool IsHorizontal { get; }
        public bool IsVertical { get; }

        public MatchResult(CubeColor color, List<SubCube> blocks, bool isHorizontal, bool isVertical)
        {
            Color = color;
            Blocks = blocks;
            IsHorizontal = isHorizontal;
            IsVertical = isVertical;
        }

        public override string ToString()
            => $"{Color} × {Count} ({(IsHorizontal ? "H" : IsVertical ? "V" : "?")})";
    }
}