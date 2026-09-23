using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;
using MyGame.Levels;

namespace MyGame.Tray
{
    public class CubeTrayManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Trays")]
        [SerializeField] private List<CubeTray> trays = new();
        [SerializeField] private bool autoDiscoverInChildren = true;
        [SerializeField] private bool autoDiscoverInScene = false;

        [Header("Shared Config (fallback)")]
        [SerializeField] private CubeShapeDefinition defaultShapeDefinition;
        [SerializeField] private CubePalette defaultPalette;
        [SerializeField] private int defaultStockCount = -1;
        [SerializeField] private float defaultRespawnDelay = 0.15f;

        [Header("Shared Sequence")]
        [SerializeField] private TraySequence sharedSequence;

        [Header("Debug")]
        [SerializeField] private bool logSequenceDraws = true;

        #endregion

        #region Runtime State

        private readonly TraySequenceCursor _sharedCursor = new(null);

        public IReadOnlyList<CubeTray> Trays => trays;
        public int TrayCount => trays.Count;
        public TraySequence SharedSequence => _sharedCursor.Sequence;
        public int SharedSequenceIndex => _sharedCursor.Index;
        public int SharedSequenceRemaining =>
            _sharedCursor.Sequence != null
                ? Mathf.Max(0, _sharedCursor.Sequence.Count - _sharedCursor.Index)
                : 0;

        public bool AllTraysEmpty
        {
            get
            {
                for (int i = 0; i < trays.Count; i++)
                {
                    var t = trays[i];
                    if (t == null) continue;
                    if (t.HasCube) return false;
                    if (t.RemainingStock > 0) return false;
                }
                return true;
            }
        }

        public bool AnyTrayHasWork => !AllTraysEmpty;

        #endregion

        #region Events

        public event System.Action<CubeTray, GameObject> OnAnyCubeSpawned;
        public event System.Action<CubeTray, DraggableCube> OnAnyCubePlaced;
        public event System.Action OnAllTraysEmpty;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            DiscoverTrays();
            HookTrays();
            _sharedCursor.SetSequence(sharedSequence);
        }

        private void OnDestroy() => UnhookTrays();

        #endregion

        #region Public API — Registration

        public void RegisterTray(CubeTray tray)
        {
            if (tray == null) return;
            if (trays.Contains(tray)) return;
            trays.Add(tray);
            HookTray(tray);
            tray.SetOwningManager(this);
        }

        public void UnregisterTray(CubeTray tray)
        {
            if (tray == null) return;
            if (!trays.Remove(tray)) return;
            UnhookTray(tray);
            tray.SetOwningManager(null);
        }

        #endregion

        #region Public API — Configuration

        public void ConfigureAll(
            CubeShapeDefinition shapeDefinition = null,
            CubePalette palette = null,
            int stockCount = int.MinValue,
            float respawnDelay = -1f,
            bool reset = true)
        {
            var finalShape = shapeDefinition != null ? shapeDefinition : defaultShapeDefinition;
            var finalPalette = palette != null ? palette : defaultPalette;
            int finalStock = stockCount != int.MinValue ? stockCount : defaultStockCount;
            float finalDelay = respawnDelay >= 0f ? respawnDelay : defaultRespawnDelay;

            for (int i = 0; i < trays.Count; i++)
            {
                var tray = trays[i];
                if (tray == null) continue;
                tray.Configure(finalShape, finalPalette, finalStock, finalDelay, reset);
            }
        }

        /// <summary>Assign the shared sequence and reset the cursor to 0.</summary>
        public void SetSharedSequence(TraySequence sequence)
        {
            sharedSequence = sequence;
            _sharedCursor.SetSequence(sequence);

            if (logSequenceDraws)
            {
                if (sequence == null)
                    Debug.Log("[CubeTrayManager] Shared sequence cleared.");
                else
                    Debug.Log($"[CubeTrayManager] Shared sequence set to '{sequence.sequenceName}' " +
                              $"({sequence.Count} entries, loop={sequence.loop}). Cursor reset to 0.");
            }
        }

        public void ResetSharedCursor()
        {
            _sharedCursor.Reset();

            if (logSequenceDraws)
                Debug.Log($"[CubeTrayManager] Shared cursor reset to 0 " +
                          $"(remaining={SharedSequenceRemaining}).");
        }

        /// <summary>Called by trays to draw the next cube from the shared sequence.</summary>
        public bool TryDrawNextSequenceCube(out TraySequence.CubeSpec spec)
        {
            spec = default;

            if (_sharedCursor.Sequence == null)
                return false;

            if (!_sharedCursor.HasNext())
            {
                if (logSequenceDraws)
                    Debug.Log($"[CubeTrayManager] Shared sequence exhausted " +
                              $"(drawIndex={_sharedCursor.Index}, count={_sharedCursor.Sequence.Count}, loop={_sharedCursor.Sequence.loop}).");
                return false;
            }

            int drawIndex = _sharedCursor.Index;
            if (!_sharedCursor.TryTake(out spec))
                return false;

            if (logSequenceDraws)
            {
                string colorsText = "none";
                if (spec.colors != null)
                {
                    colorsText = spec.colors.Length == 0
                        ? "EMPTY"
                        : string.Join(",", spec.colors);
                }

                Debug.Log($"[CubeTrayManager] Drew sequence[{drawIndex}]: " +
                          $"shape={spec.shapeType}, colors=[{colorsText}]");
            }

            return true;
        }

        public bool HasNextSequenceCube() => _sharedCursor.HasNext();

        #endregion

        #region Public API — Reset / Query

        /// <summary>
        /// Resets the shared cursor AND every tray. After this, tray #1 will
        /// spawn entry 0 of the shared sequence, tray #2 will spawn entry 1, etc.
        /// </summary>
        public void ResetAll(int newStockCount = -1)
        {
            ResetSharedCursor();

            int stock = newStockCount >= 0 ? newStockCount : defaultStockCount;
            for (int i = 0; i < trays.Count; i++)
                trays[i]?.ResetTray(stock);
        }

        public CubeTray GetActiveTray()
        {
            for (int i = 0; i < trays.Count; i++)
                if (trays[i] != null && trays[i].HasCube) return trays[i];
            return null;
        }

        public List<CubeTray> GetActiveTrays()
        {
            var result = new List<CubeTray>();
            for (int i = 0; i < trays.Count; i++)
                if (trays[i] != null && trays[i].HasCube) result.Add(trays[i]);
            return result;
        }

        #endregion

        #region Helpers

        private void DiscoverTrays()
        {
            if (trays == null) trays = new List<CubeTray>();

            if (autoDiscoverInChildren)
            {
                var found = GetComponentsInChildren<CubeTray>(includeInactive: true);
                for (int i = 0; i < found.Length; i++)
                    if (!trays.Contains(found[i])) trays.Add(found[i]);
            }

            if (autoDiscoverInScene)
            {
                var found = Object.FindObjectsByType<CubeTray>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                    if (!trays.Contains(found[i])) trays.Add(found[i]);
            }

            for (int i = 0; i < trays.Count; i++)
                trays[i]?.SetOwningManager(this);
        }

        #endregion

        #region Hook Management

        private void HookTrays() { for (int i = 0; i < trays.Count; i++) HookTray(trays[i]); }
        private void UnhookTrays() { for (int i = 0; i < trays.Count; i++) UnhookTray(trays[i]); }

        private void HookTray(CubeTray tray)
        {
            if (tray == null) return;
            tray.OnCubeSpawned += HandleTraySpawned;
            tray.OnCubePlaced += HandleTrayPlaced;
        }

        private void UnhookTray(CubeTray tray)
        {
            if (tray == null) return;
            tray.OnCubeSpawned -= HandleTraySpawned;
            tray.OnCubePlaced -= HandleTrayPlaced;
        }

        private void HandleTraySpawned(CubeTray tray, GameObject cube)
            => OnAnyCubeSpawned?.Invoke(tray, cube);

        private void HandleTrayPlaced(CubeTray tray, DraggableCube cube)
        {
            OnAnyCubePlaced?.Invoke(tray, cube);
            StartCoroutine(CheckAllEmptyEndOfFrame());
        }

        private IEnumerator CheckAllEmptyEndOfFrame()
        {
            yield return null;
            if (AllTraysEmpty) OnAllTraysEmpty?.Invoke();
        }

        #endregion
    }
}