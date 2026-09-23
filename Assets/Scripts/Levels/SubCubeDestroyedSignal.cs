using MyGame.Interaction;

namespace MyGame.Levels
{
    /// <summary>
    /// Static event hub for sub-cube destruction events.
    /// Raised by SubCubeGrowthResolver (via DestroySignalEmitter) before a sub-cube is destroyed.
    /// Consumed by GoalTracker instances.
    /// </summary>
    public static class SubCubeDestroyedSignal
    {
        public delegate void SubCubeDestroyedHandler(
            SubCube sub,
            CubeShape.BlockKind kind,
            CubeColor color,
            CubeShapeType originalShape);

        public static event SubCubeDestroyedHandler OnSubCubeDestroyed;

        public static void Raise(
            SubCube sub,
            CubeShape.BlockKind kind,
            CubeColor color,
            CubeShapeType originalShape)
        {
            OnSubCubeDestroyed?.Invoke(sub, kind, color, originalShape);
        }
    }
}