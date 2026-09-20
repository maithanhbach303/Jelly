using System.Collections;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Tray
{
    /// <summary>
    /// Holds exactly one cube at a time. Spawns a replacement
    /// the moment the current cube is placed on the board.
    /// If the cube is dropped illegally (snaps back), no new cube is spawned.
    /// </summary>
    public class CubeTray : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Stock")]
        [Tooltip("How many cubes this tray will hand out in total. -1 = infinite.")]
        [SerializeField] private int stockCount = -1;

        [Tooltip("Delay (seconds) after a cube is placed before spawning the next one.")]
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Spawn Area")]
        [Tooltip("Offset from the tray's transform where the cube spawns.")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        [Tooltip("Random horizontal jitter (X/Z) when spawning, for a hand-dropped feel.")]
        [SerializeField] private float spawnJitter = 0f;

        // Runtime
        private GameObject _activeCube;
        private int _remainingStock;

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;

        private void Start()
        {
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        // ---------- Spawning ----------

        private void SpawnNext()
        {
            if (_activeCube != null) return;      // already one sitting there
            if (RemainingStock <= 0) return;      // out of cubes
            if (cubePrefab == null) return;

            Vector3 jitter = new Vector3(
                Random.Range(-spawnJitter, spawnJitter),
                0f,
                Random.Range(-spawnJitter, spawnJitter)
            );

            Vector3 pos = transform.position + spawnOffset + jitter;
            GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity, transform);

            // Ensure it has the draggable component (idempotent)
            if (!cube.TryGetComponent(out DraggableCube _))
                cube.AddComponent<DraggableCube>();

            // Subscribe to the placed event
            var draggable = cube.GetComponent<DraggableCube>();
            draggable.OnPlacedOnBoard += HandleCubePlaced;

            _activeCube = cube;

            if (stockCount >= 0) _remainingStock--;
        }

        private void HandleCubePlaced(DraggableCube cube)
        {
            // Only react if the placed cube is *our* active cube
            if (cube == null || cube.gameObject != _activeCube) return;

            // Unsubscribe — this cube has left the tray for good
            cube.OnPlacedOnBoard -= HandleCubePlaced;

            _activeCube = null;

            // Small delay so the placement animation finishes before the next appears
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

        // ---------- Optional utilities ----------

        /// <summary>Call this if you want to reset the tray (e.g. on level restart).</summary>
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

        /// <summary>Manually spawn a cube (useful for testing or power-ups).</summary>
        public GameObject ForceSpawn()
        {
            if (_activeCube != null) return _activeCube;
            SpawnNext();
            return _activeCube;
        }
    }
}