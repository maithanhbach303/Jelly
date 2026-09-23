using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;
using MyGame.Tray;

namespace MyGame.Levels
{
    [CreateAssetMenu(fileName = "Level_", menuName = "Levels/Level Definition", order = 0)]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string levelName = "New Level";
        [TextArea(2, 4)] public string description;
        public Sprite thumbnail;
        public int difficulty = 1;

        [Header("Content")]
        public BoardDefinition board;
        public CubeShapeDefinition shapePool;
        public CubePalette palette;

        [Header("Gameplay Rules")]
        [Min(2)] public int minMatchSize = 2;
        public int stockCount = -1;
        [Min(0f)] public float respawnDelay = 0.15f;

        [Header("Goals")]
        public GoalDefinition[] goals;

        [Header("Prefilled Cubes (on the board)")]
        public PrefilledCube[] prefill;

        [Header("Tray Sequence (shared)")]
        [Tooltip("One ordered queue of cubes shared by all trays. Empty = trays spawn randomly.")]
        public TraySequence traySequence;

        public bool HasBoard => board != null;
        public bool HasShapePool => shapePool != null;
        public bool HasPalette => palette != null;

        public bool IsValid(out string error)
        {
            if (board == null)     { error = "Missing BoardDefinition."; return false; }
            if (shapePool == null) { error = "Missing CubeShapeDefinition."; return false; }
            if (palette == null)   { error = "Missing CubePalette."; return false; }
            error = null;
            return true;
        }
    }
}