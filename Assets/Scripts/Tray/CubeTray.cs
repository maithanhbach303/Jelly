using System.Collections;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    public class CubeTray : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool")]
        [SerializeField] private CubeShapeDefinition shapeDefinition;

        [Header("Color")]
        [SerializeField] private CubePalette palette;

        [Header("Stock")]
        [SerializeField] private int stockCount = -1;
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Board Reference")]
        [SerializeField] private GridManager gridManager;

        #endregion

        #region Runtime State

        private GameObject _activeCube;
        private int _remainingStock;

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;

        #endregion

        #region Events

        /// <summary>Fires right after a new cube is spawned in this tray.</summary>
        public event System.Action<CubeTray, GameObject> OnCubeSpawned;

        /// <summary>Fires when this tray's cube is successfully placed on the board.</summary>
        public event System.Action<CubeTray, DraggableCube> OnCubePlaced;

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Reconfigure the tray per level. Called by LevelLoader or CubeTrayManager.
        /// </summary>
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

            ConfigureCubeShape(cube);

            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.OnPlacedOnBoard += HandleCubePlaced;

            _activeCube = cube;
            if (stockCount >= 0) _remainingStock--;

            OnCubeSpawned?.Invoke(this, cube);
        }

        private void ConfigureCubeShape(GameObject cube)
        {
            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            float cell = (gridManager != null && gridManager.Board != null)
                ? gridManager.Board.cellSize
                : 1f;

            shape.SetCellSize(cell);
            shape.SetPalette(palette);

            if (shapeDefinition != null && shapeDefinition.TryPick(out var type, out _))
                shape.SetShape(type);
            else
                shape.SetShape(CubeShapeType.Whole);
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