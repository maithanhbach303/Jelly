using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    public enum TrayColorMode
    {
        /// <summary>Every sub-cube in the shape gets the same color.</summary>
        Single,

        /// <summary>Each sub-cube gets its own color (unique within the shape).</summary>
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
        [Tooltip("Data asset describing which shapes can spawn and how often.")]
        [SerializeField] private CubeShapeDefinition shapeDefinition;

        [Header("Color")]
        [Tooltip("Palette asset. Both the sub-cube colors and the color pool are read from here.")]
        [SerializeField] private CubePalette palette;

        [Tooltip("How colors are applied to the shape's sub-cubes.")]
        [SerializeField] private TrayColorMode colorMode = TrayColorMode.PerSubCube;

        [Tooltip("If true, avoids giving consecutive spawns the same single-color in Single mode.")]
        [SerializeField] private bool avoidConsecutiveColorRepeats = true;

        [Header("Stock")]
        [Tooltip("How many cubes this tray hands out in total. -1 = infinite.")]
        [SerializeField] private int stockCount = -1;

        [Tooltip("Delay (seconds) after a cube is placed before spawning the next one.")]
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Board Reference")]
        [Tooltip("Used to read cellSize before spawning. Auto-found if empty.")]
        [SerializeField] private GridManager gridManager;

        #endregion

        #region Runtime State

        private GameObject _activeCube;
        private int _remainingStock;
        private CubeColor _lastColor = CubeColor.None;

        // Cached color list built from the palette — rebuilt if the palette changes.
        private readonly List<CubeColor> _colorPool = new();

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();

            RefreshColorPoolFromPalette();

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
            RefreshColorPoolFromPalette();

            stockCount = newStockCount;
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        /// <summary>Swap palette at runtime and rebuild the color pool.</summary>
        public void SetPalette(CubePalette newPalette)
        {
            palette = newPalette;
            RefreshColorPoolFromPalette();
        }

        #endregion

        #region Spawning

        private void SpawnNext()
        {
            if (_activeCube != null) return;
            if (RemainingStock <= 0) return;
            if (cubePrefab == null) return;

            // The tray's own transform is the spawn point.
            Vector3 spawnPos = transform.position;

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

            // Cell size
            float cell = (gridManager != null && gridManager.Board != null)
                ? gridManager.Board.cellSize
                : 1f;
            shape.SetCellSize(cell);
            shape.SetPalette(palette);

            // Shape
            CubeShapeType type;
            int rotation;

            if (shapeDefinition != null && shapeDefinition.TryPick(out type, out rotation, out _))
            {
                // picked from data
            }
            else
            {
                type = CubeShapeType.Whole;
                rotation = 0;
            }

            shape.SetShape(type, rotation);

            // Color
            switch (colorMode)
            {
                case TrayColorMode.Single:
                    shape.SetColor(PickSingleColor());
                    break;

                case TrayColorMode.PerSubCube:
                    shape.SetRandomPerSlotColors(_colorPool.ToArray());
                    break;

                case TrayColorMode.None:
                default:
                    break;
            }
        }

        #endregion

        #region Color

        /// <summary>Populate the internal pool from the palette's entries.</summary>
        private void RefreshColorPoolFromPalette()
        {
            _colorPool.Clear();

            if (palette == null) return;

            foreach (var entry in palette.Entries)
            {
                // Skip None — it's a placeholder, not a spawnable color
                if (entry.color == CubeColor.None) continue;
                _colorPool.Add(entry.color);
            }
        }

        private CubeColor PickSingleColor()
        {
            if (_colorPool.Count == 0) return CubeColor.None;

            if (!avoidConsecutiveColorRepeats || _colorPool.Count == 1)
                return _colorPool[Random.Range(0, _colorPool.Count)];

            CubeColor picked;
            int guard = 0;
            do
            {
                picked = _colorPool[Random.Range(0, _colorPool.Count)];
                guard++;
            } while (picked == _lastColor && guard < 16);

            _lastColor = picked;
            return picked;
        }

        #endregion

        #region Placement

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