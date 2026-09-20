using System.Collections;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    public enum TrayColorMode
    {
        /// <summary>Every sub-cube in the shape gets the same random color.</summary>
        Single,

        /// <summary>Each sub-cube gets its own independent random color.</summary>
        PerSubCube,

        /// <summary>No color override — sub-cubes use whatever their prefab has.</summary>
        None,
    }

    public class CubeTray : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool")]
        [SerializeField]
        private CubeShapeType[] shapePool =
        {
            CubeShapeType.Whole,
            CubeShapeType.FourSmall,
            CubeShapeType.TwoHalf,
            CubeShapeType.HalfAndTwoSmall,
        };

        [SerializeField] private bool randomizeRotation = true;
        [SerializeField] private bool skipRotationForSymmetricShapes = true;

        [Header("Color")]
        [SerializeField] private CubePalette palette;
        [SerializeField] private TrayColorMode colorMode = TrayColorMode.PerSubCube;

        [Tooltip("Which colors can appear. Duplicates = weighting.")]
        [SerializeField]
        private CubeColor[] colorPool =
        {
            CubeColor.Red,
            CubeColor.Blue,
            CubeColor.Green,
            CubeColor.Yellow,
            CubeColor.Purple,
        };

        [Tooltip("If true (Single mode only), avoids giving the same color to two consecutive spawns.")]
        [SerializeField] private bool avoidConsecutiveRepeats = true;

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

            CubeShapeType type = PickRandomShape();

            int rotation = 0;
            if (randomizeRotation)
                if (!skipRotationForSymmetricShapes || IsRotationallyDistinct(type))
                    rotation = Random.Range(0, 4);

            // 1. Build shape first (sub-cubes created with placeholder colors)
            shape.SetShape(type, rotation);

            // 2. Apply color strategy
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
                    // Leave whatever color the prefab / inspector had
                    break;
            }
        }

        private CubeShapeType PickRandomShape()
        {
            if (shapePool == null || shapePool.Length == 0) return CubeShapeType.Whole;
            return shapePool[Random.Range(0, shapePool.Length)];
        }

        private CubeColor PickSingleColor()
        {
            if (colorPool == null || colorPool.Length == 0) return CubeColor.None;

            if (!avoidConsecutiveRepeats || colorPool.Length == 1)
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

        private static bool IsRotationallyDistinct(CubeShapeType type)
        {
            return type switch
            {
                CubeShapeType.Whole => false,
                CubeShapeType.FourSmall => false,
                _ => true,
            };
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