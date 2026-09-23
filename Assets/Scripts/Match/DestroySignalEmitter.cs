using MyGame.Interaction;
using MyGame.Levels;

namespace MyGame.Match
{
    /// <summary>
    /// Static helper that packages a sub-cube's destruction metadata
    /// and raises SubCubeDestroyedSignal. Called by the resolver before
    /// a sub-cube is destroyed.
    /// </summary>
    public static class DestroySignalEmitter
    {
        public static void Emit(SubCube sub)
        {
            if (sub == null) return;

            var kind = CubeShape.KindFromSlotSize(sub.SlotSize);
            var color = sub.CurrentColor;
            var originalShape = sub.Owner != null ? sub.Owner.ShapeType : CubeShapeType.Whole;

            SubCubeDestroyedSignal.Raise(sub, kind, color, originalShape);
        }
    }
}