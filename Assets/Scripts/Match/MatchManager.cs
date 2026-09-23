using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    public class MatchManager : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] private GridManager grid;

        [Header("Detection")]
        [SerializeField] private int minMatchSize = 2;
        [SerializeField] private bool autoResolve = true;

        [Header("Merge Animation")]
        [SerializeField] private MergeAnimator mergeAnimator;

        private SubCubeMatchDetector _detector;
        private SubCubeGrowthResolver _resolver;
        private bool _initialized;

        public event System.Action<IReadOnlyList<MatchResult>> MatchesFound;
        public event System.Action<int> ResolveCompleted;

        private readonly HashSet<DraggableCube> _subscribed = new();

        #region Lifecycle

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            // Ensure is safe to call multiple times.
            EnsureInitialized();

            if (grid != null)
            {
                grid.OnCubePlaced += HandleCubePlaced;
                grid.OnCubeRemoved += HandleCubeRemoved;
            }
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.OnCubePlaced -= HandleCubePlaced;
                grid.OnCubeRemoved -= HandleCubeRemoved;
            }
            UnsubscribeAll();
        }

        /// <summary>
        /// Lazily initializes internal state. Safe to call from anywhere, any number of times.
        /// Needed because LevelLoader runs before this component's Awake (DefaultExecutionOrder).
        /// </summary>
        public void EnsureInitialized()
        {
            if (_initialized) return;

            if (grid == null) grid = FindFirstObjectByType<GridManager>();

            if (mergeAnimator == null)
            {
                var go = new GameObject("MergeAnimator");
                go.transform.SetParent(transform, false);
                mergeAnimator = go.AddComponent<MergeAnimator>();
            }

            _detector = new SubCubeMatchDetector();
            _resolver = new SubCubeGrowthResolver { Animator = mergeAnimator };

            _initialized = true;
        }

        #endregion

        #region Subscription

        private void HandleCubePlaced(Vector2Int cell, GameObject cubeGO)
        {
            if (cubeGO == null) return;
            if (!cubeGO.TryGetComponent(out DraggableCube cube)) return;
            if (!_subscribed.Add(cube)) return;

            cube.OnPlacedOnBoard += HandleCubeFullyPlaced;
            cube.OnRemovedFromBoard += HandleCubeRemovedFromBoard;
        }

        private void HandleCubeRemoved(Vector2Int cell, GameObject cubeGO)
        {
            if (cubeGO == null) return;
            if (!cubeGO.TryGetComponent(out DraggableCube cube)) return;

            if (_subscribed.Remove(cube))
            {
                cube.OnPlacedOnBoard -= HandleCubeFullyPlaced;
                cube.OnRemovedFromBoard -= HandleCubeRemovedFromBoard;
            }
        }

        private void HandleCubeRemovedFromBoard(DraggableCube cube)
        {
            if (cube == null) return;
            if (_subscribed.Remove(cube))
            {
                cube.OnPlacedOnBoard -= HandleCubeFullyPlaced;
                cube.OnRemovedFromBoard -= HandleCubeRemovedFromBoard;
            }
        }

        private void UnsubscribeAll()
        {
            foreach (var cube in _subscribed)
            {
                if (cube == null) continue;
                cube.OnPlacedOnBoard -= HandleCubeFullyPlaced;
                cube.OnRemovedFromBoard -= HandleCubeRemovedFromBoard;
            }
            _subscribed.Clear();
        }

        #endregion

        #region Detection & Resolution

        private void HandleCubeFullyPlaced(DraggableCube cube)
        {
            if (cube == null) return;

            EnsureInitialized();

            if (grid == null)
            {
                Debug.LogWarning("[MatchManager] grid is null in HandleCubeFullyPlaced.");
                return;
            }

            var initial = _detector.FindMatches(grid, minMatchSize);
            if (initial.Count == 0)
            {
                ResolveCompleted?.Invoke(0);
                return;
            }

            MatchesFound?.Invoke(initial);

            if (!autoResolve)
            {
                ResolveCompleted?.Invoke(0);
                return;
            }
            StartCoroutine(ResolveRoutine());
        }

        private IEnumerator ResolveRoutine()
        {
            int removed = 0;
            yield return _resolver.ResolveAll(grid, minMatchSize, r => removed = r);
            ResolveCompleted?.Invoke(removed);
        }

        #endregion

        #region Manual API

        public void SetMinMatchSize(int size) => minMatchSize = Mathf.Max(2, size);

        public IEnumerator ResolveNowAsync(System.Action<int> onComplete = null)
        {
            EnsureInitialized();

            if (grid == null)
            {
                Debug.LogWarning("[MatchManager] ResolveNowAsync called with no grid.");
                onComplete?.Invoke(0);
                yield break;
            }

            int removed = 0;
            yield return _resolver.ResolveAll(grid, minMatchSize, r => removed = r);
            onComplete?.Invoke(removed);
        }

        public void ResolveNow()
        {
            StartCoroutine(ResolveNowAsync(r =>
            {
                if (r > 0) ResolveCompleted?.Invoke(r);
            }));
        }

        #endregion
    }
}