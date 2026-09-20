using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    /// <summary>
    /// Holds exactly one cube at a time. When the current cube is successfully
    /// placed on the board, spawns a replacement with a randomly chosen shape.
    /// If the cube is dropped illegally (snaps back to the tray), nothing changes.
    /// </summary>
    public class CubeTray : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [Tooltip("The cube prefab. Must have a DraggableCube, a CubeShape, and a collider.")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool")]
        [Tooltip("Which shapes can be picked. Empty = all four.")]
        [SerializeField]
        private CubeShapeType[] shapePool =
        {
            CubeShapeType.Whole,
            CubeShapeType.FourSmall,
            CubeShapeType.TwoHalf,
            CubeShapeType.HalfAndTwoSmall,
        };

        [Tooltip("If true, applies a random 0-3 quarter-turn rotation to each spawned shape.")]
        [SerializeField] private bool randomizeRotation = true;

        [Tooltip("Skip rotation for shapes that are visually symmetric (Whole, FourSmall).")]
        [SerializeField] private bool skipRotationForSymmetricShapes = true;

        [Header("Stock")]
        [Tooltip("How many cubes the tray will hand out in total. -1 = infinite.")]
        [SerializeField] private int stockCount = -1;

        [Tooltip("Delay (seconds) after a cube is placed before spawning the next one.")]
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Spawn Area")]
        [Tooltip("Where the cube's pivot spawns. If null, uses this transform.")]
        [SerializeField] private Transform slot;

        [Tooltip("Extra offset from the slot position.")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        [Tooltip("Random horizontal jitter (X/Z) added to the spawn point.")]
        [SerializeField] private float spawnJitter = 0f;

        [Header("Board Reference")]
        [Tooltip("Used to read cellSize before spawning. Auto-found if empty.")]
        [SerializeField] private GridManager gridManager;

        #endregion

        #region Runtime State

        private GameObject _activeCube;
        private int _remainingStock;

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

        /// <summary>Reset the tray — destroys the current cube and spawns a fresh one.</summary>
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

        /// <summary>Force a spawn (e.g. for testing). No-op if the tray already holds a cube.</summary>
        public GameObject ForceSpawn()
        {
            if (_activeCube != null) return _activeCube;
            SpawnNext();
            return _activeCube;
        }

        #endregion

        #region Spawning

        private void SpawnNext()
        {
            if (_activeCube != null) return;      // already holding one
            if (RemainingStock <= 0) return;      // out of cubes
            if (cubePrefab == null) return;

            // Compute spawn position
            Vector3 basePos = slot != null ? slot.position : transform.position;
            Vector3 jitter = new Vector3(
                Random.Range(-spawnJitter, spawnJitter),
                0f,
                Random.Range(-spawnJitter, spawnJitter)
            );
            Vector3 spawnPos = basePos + spawnOffset + jitter;

            // Instantiate
            GameObject cube = Instantiate(cubePrefab, spawnPos, Quaternion.identity, transform);

            // Configure the shape BEFORE the cube's Start() runs
            ConfigureShape(cube);

            // Ensure DraggableCube exists and hook the placed event
            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.OnPlacedOnBoard += HandleCubePlaced;

            _activeCube = cube;
            if (stockCount >= 0) _remainingStock--;
        }

        private void ConfigureShape(GameObject cube)
        {
            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            // Set cell size first so any runtime scale math uses the right unit
            float cell = (gridManager != null && gridManager.Board != null)
                ? gridManager.Board.cellSize
                : 1f;
            shape.SetCellSize(cell);

            // Pick a shape
            CubeShapeType type = PickRandomShape();

            // Pick a rotation
            int rotation = 0;
            if (randomizeRotation)
            {
                if (!skipRotationForSymmetricShapes || IsRotationallyDistinct(type))
                    rotation = Random.Range(0, 4);
            }

            // SetShape rebuilds synchronously; the cube's Start() will see _built == true
            shape.SetShape(type, rotation);
        }

        private CubeShapeType PickRandomShape()
        {
            if (shapePool == null || shapePool.Length == 0)
            {
                // Fallback: any of the four
                var all = System.Enum.GetValues(typeof(CubeShapeType)) as CubeShapeType[];
                return all[Random.Range(0, all.Length)];
            }

            return shapePool[Random.Range(0, shapePool.Length)];
        }

        private static bool IsRotationallyDistinct(CubeShapeType type)
        {
            // Whole and FourSmall look the same at any 90° rotation
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

            // Unsubscribe from the placed cube
            cube.OnPlacedOnBoard -= HandleCubePlaced;
            _activeCube = null;

            // Respawn with a small delay so the placement feels settled
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