using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Synchronous match resolution. Detect on the whole board, destroy matched
    /// sub-cubes, and free emptied cells — all in one call.
    /// </summary>
    public class MatchResolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager grid;

        [Header("Detection")]
        [SerializeField] private int minMatchSize = 3;

        [Header("Effects")]
        [SerializeField] private GameObject destroyEffectPrefab;

        private readonly SubCubeMatchDetector _detector = new();
        private readonly List<MatchResult> _matches = new();
        private readonly List<SubCube> _toDestroy = new();
        private readonly HashSet<SubCube> _seen = new();
        private readonly List<GameObject> _emptyCubes = new();

        public event System.Action<IReadOnlyList<SubCube>, IReadOnlyList<MatchResult>> Resolved;

        public int MinMatchSize { get => minMatchSize; set => minMatchSize = Mathf.Max(3, value); }

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<GridManager>();
        }

        public IReadOnlyList<MatchResult> ResolveAt(Vector2Int cell)
        {
            if (grid == null || grid.SubGrid == null)
                return System.Array.Empty<MatchResult>();

            _detector.FindMatches(grid.SubGrid, _matches, minMatchSize);
            if (_matches.Count == 0) return System.Array.Empty<MatchResult>();

            _toDestroy.Clear();
            _seen.Clear();
            for (int i = 0; i < _matches.Count; i++)
            {
                var blocks = _matches[i].Blocks;
                for (int b = 0; b < blocks.Count; b++)
                {
                    var sub = blocks[b];
                    if (sub == null) continue;
                    if (_seen.Add(sub)) _toDestroy.Add(sub);
                }
            }

            for (int i = 0; i < _toDestroy.Count; i++)
            {
                var sub = _toDestroy[i];
                if (sub == null) continue;

                if (destroyEffectPrefab != null)
                    Instantiate(destroyEffectPrefab, sub.transform.position, Quaternion.identity);

                var shape = sub.Owner;
                if (shape != null) shape.RemoveBlock(sub);

                if (grid.SubGrid != null) grid.SubGrid.Unregister(sub);

                Destroy(sub.gameObject);
            }

            CleanupEmptyCubes();
            Resolved?.Invoke(_toDestroy, _matches);
            return _matches;
        }

        private void CleanupEmptyCubes()
        {
            if (grid == null) return;

            _emptyCubes.Clear();
            foreach (var kvp in grid.Occupied)
            {
                var go = kvp.Value;
                if (go == null) continue;
                if (!go.TryGetComponent(out DraggableCube cube)) continue;
                var shape = cube.Shape;
                if (shape == null) continue;
                if (shape.Blocks.Count == 0) _emptyCubes.Add(go);
            }

            for (int i = 0; i < _emptyCubes.Count; i++)
            {
                var go = _emptyCubes[i];
                if (go == null) continue;
                var cell = FindCellFor(go);
                if (cell.HasValue) grid.Release(cell.Value, go);
                Destroy(go);
            }
        }

        private Vector2Int? FindCellFor(GameObject cube)
        {
            foreach (var kvp in grid.Occupied)
                if (kvp.Value == cube) return kvp.Key;
            return null;
        }
    }
}