using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Levels
{
    /// <summary>
    /// A goal consists of one or more color requirements. The goal is complete
    /// when every requirement has met its target count.
    ///
    /// Example: "Destroy 5 Red and 3 Blue" is one GoalDefinition with two entries.
    /// </summary>
    [CreateAssetMenu(fileName = "Goal_", menuName = "Levels/Goal Definition", order = 0)]
    public class GoalDefinition : ScriptableObject
    {
        #region Nested Types

        [System.Serializable]
        public struct ColorRequirement
        {
            [Tooltip("Which color must be destroyed. None = any color (usually only one such entry).")]
            public CubeColor color;

            [Tooltip("How many sub-cubes of this color must be destroyed.")]
            [Min(1)] public int count;

            public ColorRequirement(CubeColor color, int count)
            {
                this.color = color;
                this.count = count;
            }
        }

        #endregion

        #region Inspector Fields

        [Header("Identity")]
        public string displayName = "Objective";
        [TextArea(1, 3)] public string description;
        public Sprite icon;

        [Header("Color Requirements")]
        [Tooltip("Every entry must be satisfied to complete the goal.")]
        [SerializeField]
        private ColorRequirement[] requirements =
        {
            new ColorRequirement(CubeColor.Red, 5),
        };

        [Header("Optional Filters")]
        [Tooltip("If set, only sub-cubes from cubes originally spawned with this shape count. Null = any shape.")]
        public CubeShapeType? requiredOriginalShape = null;

        #endregion

        #region Public Accessors

        public ColorRequirement[] Requirements => requirements;
        public int RequirementCount => requirements != null ? requirements.Length : 0;

        public bool IsEmpty => RequirementCount == 0;

        #endregion

        #region Tracker Factory

        public GoalTracker CreateTracker() => new GoalTracker(this);

        #endregion

        #region Summary

        public string GetSummary()
        {
            if (IsEmpty) return $"{displayName} (no requirements)";

            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < requirements.Length; i++)
            {
                if (i > 0) parts.Append(", ");
                var r = requirements[i];
                string colorName = r.color == CubeColor.None ? "Any" : r.color.ToString();
                parts.Append($"{r.count} {colorName}");
            }
            return parts.ToString();
        }

        #endregion

        #region Validation

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(displayName)) displayName = name;

            if (requirements == null) return;

            for (int i = 0; i < requirements.Length; i++)
            {
                if (requirements[i].count < 1) requirements[i].count = 1;

                for (int j = i + 1; j < requirements.Length; j++)
                {
                    if (requirements[i].color == requirements[j].color &&
                        requirements[i].color != CubeColor.None)
                    {
                        Debug.LogWarning(
                            $"[GoalDefinition '{name}'] Duplicate color entry: " +
                            $"{requirements[i].color}. Merge them to avoid confusion.",
                            this
                        );
                    }
                }
            }
        }
#endif

        #endregion
    }
}