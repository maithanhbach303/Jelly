using System.Collections;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    public enum TrayColorMode
    {
        Single,
        PerSubCube,
        None,
    }

    public class CubeTray : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool")]
        [Tooltip("Data asset describing which shapes can spawn and how often.")]
        [SerializeField] private CubeShapeDefinition shapeDefinition;

        [Header("Color")]
        [SerializeField] private CubePalette palette;
        [SerializeField] private TrayColorMode colorMode = TrayColorMode.PerSubCube;

        [SerializeField]
        private CubeColor[] colorPool =
        {
            CubeColor.Red,
            CubeColor.Blue,
            CubeColor.Green,
            CubeColor.Yellow,
            CubeColor.Purple,
        };

        [SerializeField] private bool avoidConsecutiveColorRepeats = true;

        [Header("Stock")]
        [SerializeField] private int stockCount = -1;
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Spawn Area")]
        [SerializeField] private Transform slot;
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;
        [SerializeField] private float spawnJitter = 0f;

        [Header("Board Reference")]
        [SerializeField] private GridManager gridManager;

        #endregion

        #region Runtime State

        private GameObject _activeCube;
        private int _remainingStock;
        private CubeColor _lastColor = CubeColor.None;

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;

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

            Vector3 basePos = slot != null ? slot.position : transform.position;
            Vector3 jitter = new Vector3(
                Random.Range(-spawnJitter, spawnJitter),
                0f,
                Random.Range(-spawnJitter, spawnJitter)
            );
            Vector3 spawnPos = basePos + spawnOffset + jitter;

            GameObject cube = Instantiate(cubePrefab, spawnPos, Quaternion.identity, transform);

            ConfigureCube(cube);

            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.OnPlacedOnBoard += HandleCubePlaced;

            _activeCube = cube;
            if (stockCount >= 0) _remainingStock--;
        }

        private void ConfigureCube(GameObject cube)
        {
            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            float cell = (gridManager != null && gridManager.Board != null)
                ? gridManager.Board.cellSize
                : 1f;
            shape.SetCellSize(cell);
            shape.SetPalette(palette);

            // --- Pick a shape via the ScriptableObject ---
            CubeShapeType type;
            int rotation;

            if (shapeDefinition != null && shapeDefinition.TryPick(out type, out rotation, out _))
            {
                // Picked from data
            }
            else
            {
                // Fallback if no definition assigned
                type = CubeShapeType.Whole;
                rotation = 0;
            }

            // Apply shape
            shape.SetShape(type, rotation);

            // Apply color mode
            switch (colorMode)
            {
                case TrayColorMode.Single:
                    shape.SetColor(PickSingleColor());
                    break;

                case TrayColorMode.PerSubCube:
                    shape.SetRandomPerSlotColors(colorPool);
                    break;

                case TrayColorMode.None:
                default:
                    break;
            }
        }

        private CubeColor PickSingleColor()
        {
            if (colorPool == null || colorPool.Length == 0) return CubeColor.None;

            if (!avoidConsecutiveColorRepeats || colorPool.Length == 1)
                return colorPool[Random.Range(0, colorPool.Length)];

            CubeColor picked;
            int guard = 0;
            do
            {
                picked = colorPool[Random.Range(0, colorPool.Length)];
                guard++;
            } while (picked == _lastColor && guard < 16);

            _lastColor = picked;
            return picked;
        }

        private void HandleCubePlaced(DraggableCube cube)
        {
            if (cube == null || cube.gameObject != _activeCube) return;

            cube.OnPlacedOnBoard -= HandleCubePlaced;
            _activeCube = null;

            if (respawnDelay > 0f)
                StartCoroutine(RespawnAfterDelay());
            else
                SpawnNext();
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnNext();
        }

        #endregion
    }
}