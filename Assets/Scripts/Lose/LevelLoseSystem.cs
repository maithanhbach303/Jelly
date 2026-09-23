using UnityEngine;
using MyGame.Board;
using MyGame.Match;

namespace MyGame.Lose
{
    /// <summary>
    /// Lose condition: every valid cell on the board is filled.
    /// Checks after placements and after match resolution.
    /// </summary>
    public class LevelLoseSystem : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Systems")]
        [SerializeField] private GridManager grid;
        [SerializeField] private MatchManager matchManager;

        [Header("Debug")]
        [SerializeField] private bool logChecks = true;

        #endregion

        #region Runtime State

        private bool _dirty;
        private bool _hasLost;
        private bool _isResolving;

        public bool HasLost => _hasLost;

        #endregion

        #region Events

        public event System.Action OnLevelLost;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<GridManager>();
            if (matchManager == null) matchManager = FindFirstObjectByType<MatchManager>();
        }

        private void OnEnable()
        {
            if (grid != null)
            {
                grid.OnCubePlaced += HandleCubePlaced;
                grid.OnCubeRemoved += HandleCubeRemoved;
                grid.OnBoardBuilt += HandleBoardBuilt;
            }

            if (matchManager != null)
            {
                matchManager.ResolveCompleted += HandleResolveCompleted;
            }
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.OnCubePlaced -= HandleCubePlaced;
                grid.OnCubeRemoved -= HandleCubeRemoved;
                grid.OnBoardBuilt -= HandleBoardBuilt;
            }

            if (matchManager != null)
            {
                matchManager.ResolveCompleted -= HandleResolveCompleted;
            }
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;

            if (_isResolving) return;   // wait for match resolution to finish
            if (_hasLost) return;

            Evaluate();
        }

        #endregion

        #region Event Handlers

        private void HandleCubePlaced(Vector2Int cell, GameObject cubeGO)
        {
            _isResolving = true;
            _dirty = true;
        }
        private void HandleCubeRemoved(Vector2Int cell, GameObject cubeGO) => _dirty = true;
        private void HandleBoardBuilt(BoardDefinition def) => _dirty = true;

        private void HandleResolveCompleted(int removed)
        {
            _isResolving = false;
            _dirty = true;
        }

        #endregion

        #region Reset

        /// <summary>Call on level load / retry to clear the lose state.</summary>
        public void ResetState()
        {
            _hasLost = false;
            _dirty = false;
            _isResolving = false;
        }

        #endregion

        #region Evaluation

        private void Evaluate()
        {
            if (grid == null || grid.Board == null) return;

            int totalCells = 0;
            int filledCells = 0;

            foreach (var cell in grid.Board.EnumerateValidCells())
            {
                totalCells++;
                if (grid.IsOccupied(cell)) filledCells++;
            }

            if (logChecks)
                Debug.Log($"[LevelLoseSystem] Check: {filledCells}/{totalCells} filled.");

            if (totalCells > 0 && filledCells >= totalCells)
                FireLose();
        }

        private void FireLose()
        {
            if (_hasLost) return;
            _hasLost = true;

            Debug.Log("[LevelLoseSystem] Board is full — lose.");
            OnLevelLost?.Invoke();
        }

        #endregion
    }
}