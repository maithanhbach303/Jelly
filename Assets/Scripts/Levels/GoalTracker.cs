using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Levels
{
    /// <summary>
    /// Runtime progress tracker for one GoalDefinition.
    /// Subscribes to SubCubeDestroyedSignal and increments per-color counters.
    /// </summary>
    public class GoalTracker
    {
        public GoalDefinition Goal { get; }

        private readonly int[] _progress;
        private readonly int[] _targets;

        public bool IsComplete { get; private set; }
        public bool IsInitialized { get; private set; }

        public event System.Action<GoalTracker> OnCompleted;
        public event System.Action<GoalTracker> OnProgressChanged;
        public event System.Action<GoalTracker, int> OnRequirementProgressChanged;

        public GoalTracker(GoalDefinition goal)
        {
            Goal = goal;

            int n = goal != null ? goal.RequirementCount : 0;
            _progress = new int[n];
            _targets = new int[n];

            if (goal != null && goal.Requirements != null)
            {
                for (int i = 0; i < n; i++)
                    _targets[i] = Mathf.Max(1, goal.Requirements[i].count);
            }
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (Goal == null || Goal.IsEmpty)
            {
                IsComplete = true;
                OnCompleted?.Invoke(this);
                return;
            }

            SubCubeDestroyedSignal.OnSubCubeDestroyed += HandleSubCubeDestroyed;
            EvaluateCompletion();
        }

        public void Dispose()
        {
            if (!IsInitialized) return;
            IsInitialized = false;

            SubCubeDestroyedSignal.OnSubCubeDestroyed -= HandleSubCubeDestroyed;
        }

        private void HandleSubCubeDestroyed(
            SubCube sub,
            CubeShape.BlockKind kind,
            CubeColor color,
            CubeShapeType originalShape)
        {
            if (IsComplete) return;
            if (Goal == null) return;

            if (Goal.requiredOriginalShape.HasValue &&
                originalShape != Goal.requiredOriginalShape.Value)
                return;

            var reqs = Goal.Requirements;
            if (reqs == null) return;

            bool anyChanged = false;

            for (int i = 0; i < reqs.Length; i++)
            {
                var r = reqs[i];

                if (r.color != CubeColor.None && r.color != color) continue;
                if (_progress[i] >= _targets[i]) continue;

                _progress[i]++;
                anyChanged = true;

                OnRequirementProgressChanged?.Invoke(this, i);
            }

            if (!anyChanged) return;

            OnProgressChanged?.Invoke(this);
            EvaluateCompletion();
        }

        public int GetProgress(int i) => (i >= 0 && i < _progress.Length) ? _progress[i] : 0;
        public int GetTarget(int i)   => (i >= 0 && i < _targets.Length)  ? _targets[i]  : 0;

        public float GetProgress01(int i)
        {
            int t = GetTarget(i);
            return t > 0 ? Mathf.Clamp01((float)GetProgress(i) / t) : 0f;
        }

        public bool IsRequirementComplete(int i) => GetProgress(i) >= GetTarget(i);

        public float GetTotalProgress01()
        {
            if (_targets.Length == 0) return 1f;

            int totalTarget = 0;
            int totalCurrent = 0;
            for (int i = 0; i < _targets.Length; i++)
            {
                totalTarget += _targets[i];
                totalCurrent += Mathf.Min(_progress[i], _targets[i]);
            }
            return totalTarget > 0 ? Mathf.Clamp01((float)totalCurrent / totalTarget) : 0f;
        }

        private void EvaluateCompletion()
        {
            if (IsComplete) return;

            for (int i = 0; i < _targets.Length; i++)
                if (_progress[i] < _targets[i]) return;

            IsComplete = true;
            OnCompleted?.Invoke(this);
        }
    }
}