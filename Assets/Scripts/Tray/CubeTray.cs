using System.Collections;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;
using MyGame.Levels;

namespace MyGame.Tray
{
    public class CubeTray : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool (fallback for random spawns)")]
        [SerializeField] private CubeShapeDefinition shapeDefinition;

        [Header("Color")]
        [SerializeField] private CubePalette palette;

        [Header("Stock")]
        [SerializeField] private int stockCount = -1;
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Board Reference")]
        [SerializeField] private GridManager gridManager;

        [Header("Debug")]
        [SerializeField] private bool logSpawns = true;

        #endregion

        #region Runtime State

        private GameObject _activeCube;
        private int _remainingStock;
        private CubeTrayManager _manager;

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;
        public GameObject ActiveCube => _activeCube;

        #endregion

        #region Events

        public event System.Action<CubeTray, GameObject> OnCubeSpawned;
        public event System.Action<CubeTray, DraggableCube> OnCubePlaced;

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;

            StartCoroutine(InitialSpawnNextFrame());
        }

        private IEnumerator InitialSpawnNextFrame()
        {
            yield return null;
            if (_activeCube == null) SpawnNext();
        }

        #endregion

        #region Public API

        public void SetOwningManager(CubeTrayManager manager)
        {
            _manager = manager;
        }

        public void Configure(
            CubeShapeDefinition shapeDefinition,
            CubePalette palette,
            int stockCount,
            float respawnDelay,
            bool reset = true)
        {
            this.shapeDefinition = shapeDefinition;
            this.palette = palette;
            this.stockCount = stockCount;
            this.respawnDelay = respawnDelay;

            if (reset) ResetTray(stockCount);
        }

        public void ResetTray(int newStockCount = -1)
        {
            StopAllCoroutines();

            if (_activeCube != null)
            {
                var d = _activeCube.GetComponent<DraggableCube>();
                if (d != null) d.OnPlacedOnBoard -= HandleCubePlaced;
                Destroy(_activeCube);
                _activeCube = null;
            }

            shapeDefinition?.ResetPicker();

            stockCount = newStockCount;
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        #endregion

        #region Spawning

        private void SpawnNext()
        {
            if (_activeCube != null) return;
            if (RemainingStock <= 0) return;
            if (cubePrefab == null) return;

            GameObject cube = Instantiate(cubePrefab, transform.position, Quaternion.identity, transform);

            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.IsTrayOwned = true;

            // --- Draw from shared sequence if available ---
            bool drewFromSequence = false;

            if (_manager != null && _manager.TryDrawNextSequenceCube(out var spec))
            {
                ConfigureShapeFromSpec(cube, draggable, spec);
                drewFromSequence = true;

                if (logSpawns)
                {
                    Debug.Log($"[CubeTray '{name}'] Drawn spec: " +
                              $"shape={spec.shapeType}, " +
                              $"colors=[{ColorArrayToString(spec.colors)}], " +
                              $"paletteOverride={(spec.paletteOverride == null ? "none" : spec.paletteOverride.name)}");
                }
            }

            if (!drewFromSequence)
                ConfigureShapeRandom(cube, draggable);

            cube.name = drewFromSequence ? $"TrayCube_{name}_Seq" : $"TrayCube_{name}_Random";

            draggable.OnPlacedOnBoard += HandleCubePlaced;

            _activeCube = cube;
            if (stockCount >= 0) _remainingStock--;

            if (logSpawns)
                LogSpawnedCube(cube, drewFromSequence);

            OnCubeSpawned?.Invoke(this, cube);
        }

        private void ConfigureShapeFromSpec(GameObject cube, DraggableCube draggable, TraySequence.CubeSpec spec)
        {
            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            float cell = CellSize();
            shape.SetCellSize(cell);

            var finalPalette = spec.paletteOverride != null ? spec.paletteOverride : palette;
            shape.SetPalette(finalPalette);
            shape.SetRandomizePlacement(false);
            shape.SetShape(spec.shapeType);
            shape.Build();

            // Hand the spec to the DraggableCube so it re-applies colors after
            // any future shape.Build() (its own Initialize, board rebuilds, etc.).
            draggable.SetPendingSpec(spec, finalPalette);
        }

        private void ConfigureShapeRandom(GameObject cube, DraggableCube draggable)
        {
            // Random cubes should not carry a sequence spec.
            draggable.ClearPendingSpec();

            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            float cell = CellSize();
            shape.SetCellSize(cell);
            shape.SetPalette(palette);

            if (shapeDefinition != null && shapeDefinition.TryPick(out var type, out _))
                shape.SetShape(type);
            else
                shape.SetShape(CubeShapeType.Whole);
        }

        private float CellSize()
        {
            return (gridManager != null && gridManager.Board != null)
                ? gridManager.Board.cellSize
                : 1f;
        }

        private void LogSpawnedCube(GameObject cube, bool fromSequence)
        {
            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            var sb = new System.Text.StringBuilder();
            sb.Append($"[CubeTray '{name}'] Spawned {(fromSequence ? "SEQUENCE" : "RANDOM")} cube: ");
            sb.Append($"shape={shape.ShapeType}, blocks={shape.Blocks.Count}, colors=[");

            for (int i = 0; i < shape.Blocks.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(shape.Blocks[i] != null ? shape.Blocks[i].CurrentColor.ToString() : "null");
            }

            sb.Append($"], remainingStock={RemainingStock}");
            Debug.Log(sb.ToString(), cube);
        }

        private static string ColorArrayToString(CubeColor[] colors)
        {
            if (colors == null) return "NULL";
            if (colors.Length == 0) return "EMPTY";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < colors.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(colors[i]);
            }
            return sb.ToString();
        }

        private void HandleCubePlaced(DraggableCube cube)
        {
            if (cube == null || cube.gameObject != _activeCube) return;

            cube.OnPlacedOnBoard -= HandleCubePlaced;
            _activeCube = null;

            OnCubePlaced?.Invoke(this, cube);

            if (respawnDelay > 0f) StartCoroutine(RespawnAfterDelay());
            else SpawnNext();
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnNext();
        }

        #endregion
    }
}