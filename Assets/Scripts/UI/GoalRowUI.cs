using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyGame.Interaction;
using MyGame.Levels;

namespace MyGame.UI
{
    /// <summary>
    /// Renders a single color requirement of a GoalDefinition.
    /// One instance per (goal, requirement index) pair.
    /// </summary>
    public class GoalRowUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image colorSwatch;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text progressText;

        private GoalTracker _tracker;
        private int _requirementIndex;
        private CubePalette _palette;

        public void Bind(GoalTracker tracker, int requirementIndex, CubePalette palette)
        {
            // Unbind any previous
            if (_tracker != null)
                _tracker.OnRequirementProgressChanged -= HandleProgressChanged;

            _tracker = tracker;
            _requirementIndex = requirementIndex;
            _palette = palette;

            if (_tracker != null)
                _tracker.OnRequirementProgressChanged += HandleProgressChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            if (_tracker != null)
                _tracker.OnRequirementProgressChanged -= HandleProgressChanged;
        }

        private void HandleProgressChanged(GoalTracker tracker, int index)
        {
            if (index != _requirementIndex) return;
            Refresh();
        }

        private void Refresh()
        {
            if (_tracker == null || _tracker.Goal == null) return;

            var reqs = _tracker.Goal.Requirements;
            if (reqs == null || _requirementIndex < 0 || _requirementIndex >= reqs.Length) return;

            var req = reqs[_requirementIndex];

            if (colorSwatch != null)
            {
                Color c = req.color == CubeColor.None
                    ? Color.gray
                    : (_palette != null ? _palette.GetRGB(req.color) : Color.white);
                colorSwatch.color = c;
            }

            if (icon != null && _tracker.Goal.icon != null)
            {
                icon.sprite = _tracker.Goal.icon;
                icon.enabled = true;
            }

            if (progressText != null)
            {
                int cur = _tracker.GetProgress(_requirementIndex);
                int tgt = _tracker.GetTarget(_requirementIndex);
                progressText.text = $"{cur} / {tgt}";

                bool complete = cur >= tgt;
                progressText.color = complete ? new Color(0.5f, 1f, 0.5f) : Color.white;
            }
        }
    }
}