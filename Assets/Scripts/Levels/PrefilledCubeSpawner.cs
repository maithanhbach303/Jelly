using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Levels
{
    /// <summary>
    /// Spawns a prefill cube at a cell and places it via DraggableCube.PlaceImmediate.
    /// Does NOT assign colors — the LevelLoader owns that.
    /// </summary>
    public class PrefilledCubeSpawner : MonoBehaviour
    {
        #region Inspector

        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Board")]
        [SerializeField] private GridManager gridManager;

        [Header("Placement")]
        [SerializeField] private bool playLandingPulse = false;

        [Header("Debug")]
        [SerializeField] private bool logSpawns = false;

        #endregion

        #region Runtime State

        private readonly List<GameObject> _spawned = new();
        public IReadOnlyList<GameObject> Spawned => _spawned;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        }

        #endregion

        #region Public API

        /// <summary>Spawn one prefill at the entry's cell. Returns the cube or null.</summary>
        public GameObject SpawnAt(PrefilledCube entry, CubePalette levelPalette)
        {
            if (cubePrefab == null) { Debug.LogError("[PrefilledCubeSpawner] No cube prefab.", this); return null; }
            if (gridManager == null) { Debug.LogError("[PrefilledCubeSpawner] No GridManager.", this); return null; }
            if (gridManager.Board == null) { Debug.LogError("[PrefilledCubeSpawner] Board is null.", this); return null; }

            if (!gridManager.IsValidGridPosition(entry.cell))
            {
                Debug.LogWarning($"[PrefilledCubeSpawner] Cell {entry.cell} invalid.", this);
                return null;
            }
            if (gridManager.IsOccupied(entry.cell))
            {
                Debug.LogWarning($"[PrefilledCubeSpawner] Cell {entry.cell} occupied.", this);
                return null;
            }

            // Rotation from the entry
            Quaternion rotation = Quaternion.Euler(0f, entry.rotationQuarterTurns * 90f, 0f);

            Vector3 worldPos = gridManager.GetWorldPosition(entry.cell);
            GameObject cube = Instantiate(cubePrefab, worldPos, rotation, gridManager.transform);
            _spawned.Add(cube);

            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.IsPrefilled = true;
            draggable.SetYawQuarterTurns(entry.rotationQuarterTurns);

            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null)
            {
                Debug.LogWarning("[PrefilledCubeSpawner] No CubeShape on prefab.", cube);
                Destroy(cube);
                _spawned.Remove(cube);
                return null;
            }

            float cellSize = gridManager.Board.cellSize;
            shape.SetCellSize(cellSize);

            var palette = entry.paletteOverride != null ? entry.paletteOverride : levelPalette;
            shape.SetPalette(palette);

            shape.SetRandomizePlacement(false);
            shape.SetShape(entry.shapeType);

            if (shape.Blocks.Count == 0)
            {
                Debug.LogError(
                    $"[PrefilledCubeSpawner] Shape built 0 sub-cubes for {entry.shapeType}. " +
                    $"Check the prefab's wholeCube/halfCube/smallCube references.", cube);
                Destroy(cube);
                _spawned.Remove(cube);
                return null;
            }

            draggable.InitializeForPrefill(gridManager);
            draggable.PlaceImmediate(fireEvents: true, playPulse: playLandingPulse);

            if (logSpawns)
                Debug.Log($"[PrefilledCubeSpawner] Spawned {entry.shapeType} at {entry.cell} " +
                          $"(yaw={entry.rotationQuarterTurns * 90f}°, {shape.Blocks.Count} sub-cubes).", cube);

            return cube;
        }

        public void ClearAll()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go == null) continue;

                if (go.TryGetComponent(out DraggableCube cube))
                    cube.CleanupForDestroy();

                Destroy(go);
            }
            _spawned.Clear();
        }

        #endregion
    }
}