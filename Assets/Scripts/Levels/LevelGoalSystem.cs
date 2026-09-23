using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;

namespace MyGame.Levels
{
    public class LevelGoalSystem : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] private GridManager grid;

        private readonly List<GoalTracker> _trackers = new();
        private int _completedCount;

        public IReadOnlyList<GoalTracker> Trackers => _trackers;
        public bool AllGoalsComplete => _trackers.Count > 0 && _completedCount >= _trackers.Count;

        public event System.Action<GoalTracker> OnGoalCompleted;
        public event System.Action OnAllGoalsCompleted;

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<GridManager>();
        }

        private void OnDestroy()
        {
            DisposeTrackers();
        }

        public void LoadGoals(GoalDefinition[] goals)
        {
            DisposeTrackers();
            _completedCount = 0;

            if (goals == null || goals.Length == 0)
                return;

            for (int i = 0; i < goals.Length; i++)
            {
                var goal = goals[i];
                if (goal == null) continue;

                var tracker = goal.CreateTracker();
                tracker.OnCompleted += HandleGoalCompleted;
                tracker.Initialize();

                _trackers.Add(tracker);
            }

            CheckAllComplete();
        }

        private void DisposeTrackers()
        {
            for (int i = 0; i < _trackers.Count; i++)
            {
                var t = _trackers[i];
                if (t == null) continue;
                t.OnCompleted -= HandleGoalCompleted;
                t.Dispose();
            }
            _trackers.Clear();
        }

        private void HandleGoalCompleted(GoalTracker tracker)
        {
            _completedCount++;
            OnGoalCompleted?.Invoke(tracker);
            CheckAllComplete();
        }

        private void CheckAllComplete()
        {
            if (AllGoalsComplete)
                OnAllGoalsCompleted?.Invoke();
        }
    }
}