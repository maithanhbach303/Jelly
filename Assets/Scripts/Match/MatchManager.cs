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

        [Tooltip("If true, runs the resolution cascade automatically on placement.")]
        [SerializeField] private bool autoResolve = true;

        [Header("Merge Animation")]
        [Tooltip("The animator that handles the merge visual. Auto-created if empty.")]
        [SerializeField] private MergeAnimator mergeAnimator;

        private readonly SubCubeMatchDetector _detector = new();
        private SubCubeGrowthResolver _resolver;

        public event System.Action<IReadOnlyList<MatchResult>> MatchesFound;
        public event System.Action<int> ResolveCompleted;

        private readonly HashSet<DraggableCube> _subscribed = new();

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<GridManager>();

            // Animator — create one on this GameObject if none is assigned
            if (mergeAnimator == null)
            {
                var go = new GameObject("MergeAnimator");
                go.transform.SetParent(transform, false);
                mergeAnimator = go.AddComponent<MergeAnimator>();
            }

            _resolver = new SubCubeGrowthResolver { Animator = mergeAnimator };
        }

        private void OnEnable()
        {
            if (grid != null) grid.OnCubePlaced += HandleCubePlaced;
            if (grid != null) grid.OnCubeRemoved += HandleCubeRemoved;
        }

        private void OnDisable()
        {
            if (grid != null) grid.OnCubePlaced -= HandleCubePlaced;
            if (grid != null) grid.OnCubeRemoved -= HandleCubeRemoved;
            UnsubscribeAll();
        }

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

            var initial = _detector.FindMatches(grid, minMatchSize);
            if (initial.Count == 0) return;

            MatchesFound?.Invoke(initial);

            if (!autoResolve) return;

            // Start the async resolve
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