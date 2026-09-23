using System.Collections;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Tray
{
    public class CubeTray : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject cubePrefab;

        [Header("Shape Pool")]
        [SerializeField] private CubeShapeDefinition shapeDefinition;

        [Header("Color")]
        [SerializeField] private CubePalette palette;

        [Header("Stock")]
        [SerializeField] private int stockCount = -1;
        [SerializeField] private float respawnDelay = 0.15f;

        [Header("Board")]
        [SerializeField] private GridManager gridManager;

        private GameObject _activeCube;
        private int _remainingStock;

        public bool HasCube => _activeCube != null;
        public int RemainingStock => stockCount < 0 ? int.MaxValue : _remainingStock;

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        public void ResetTray(int newStock = -1)
        {
            StopAllCoroutines();

            if (_activeCube != null)
            {
                var d = _activeCube.GetComponent<DraggableCube>();
                if (d != null) d.OnPlacedOnBoard -= HandlePlaced;
                Destroy(_activeCube);
                _activeCube = null;
            }

            shapeDefinition?.ResetPicker();
            stockCount = newStock;
            _remainingStock = stockCount < 0 ? int.MaxValue : stockCount;
            SpawnNext();
        }

        private void SpawnNext()
        {
            if (_activeCube != null) return;
            if (RemainingStock <= 0) return;
            if (cubePrefab == null) return;

            GameObject cube = Instantiate(cubePrefab, transform.position, Quaternion.identity, transform);

            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape != null)
            {
                float cell = (gridManager != null && gridManager.Board != null)
                    ? gridManager.Board.cellSize : 1f;

                shape.SetCellSize(cell);
                shape.SetPalette(palette);

                if (shapeDefinition != null && shapeDefinition.TryPick(out var type, out _))
                    shape.SetShape(type);
                else
                    shape.SetShape(CubeShapeType.Whole);
            }

            if (!cube.TryGetComponent(out DraggableCube draggable))
                draggable = cube.AddComponent<DraggableCube>();

            draggable.OnPlacedOnBoard += HandlePlaced;
            _activeCube = cube;
            if (stockCount >= 0) _remainingStock--;
        }

        private void HandlePlaced(DraggableCube cube)
        {
            if (cube == null || cube.gameObject != _activeCube) return;
            cube.OnPlacedOnBoard -= HandlePlaced;
            _activeCube = null;

            if (respawnDelay > 0f) StartCoroutine(RespawnAfterDelay());
            else SpawnNext();
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnNext();
        }
    }
}