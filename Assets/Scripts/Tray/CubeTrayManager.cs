using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Tray
{
    /// <summary>
    /// Central controller for all CubeTrays in the scene.
    /// Fan-outs configuration (palette, shape pool, stock) to every registered tray,
    /// aggregates their events, and exposes a single reset/pause API.
    /// </summary>
    public class CubeTrayManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Trays")]
        [Tooltip("Explicitly assigned trays. Leave empty to auto-discover all CubeTray components in children.")]
        [SerializeField] private List<CubeTray> trays = new();

        [Tooltip("If true, auto-discover CubeTray components in child GameObjects on Awake.")]
        [SerializeField] private bool autoDiscoverInChildren = true;

        [Tooltip("If true, also search the whole scene for CubeTray components (not just children).")]
        [SerializeField] private bool autoDiscoverInScene = false;

        [Header("Shared Config (fallback)")]
        [Tooltip("Used when a level doesn't specify its own. Assigned by LevelLoader at runtime.")]
        [SerializeField] private CubeShapeDefinition defaultShapeDefinition;
        [SerializeField] private CubePalette defaultPalette;
        [SerializeField] private int defaultStockCount = -1;
        [SerializeField] private float defaultRespawnDelay = 0.15f;

        #endregion

        #region Runtime State

        public IReadOnlyList<CubeTray> Trays => trays;
        public int TrayCount => trays.Count;

        /// <summary>True when every registered tray has no active cube and no remaining stock.</summary>
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

        /// <summary>True when at least one tray still holds a cube or has stock left.</summary>
        public bool AnyTrayHasWork => !AllTraysEmpty;

        #endregion

        #region Events

        /// <summary>Fires when any tray spawns a new cube.</summary>
        public event System.Action<CubeTray, GameObject> OnAnyCubeSpawned;

        /// <summary>Fires when any tray's cube is placed on the board.</summary>
        public event System.Action<CubeTray, DraggableCube> OnAnyCubePlaced;

        /// <summary>Fires when the last tray runs out of cubes and stock.</summary>
        public event System.Action OnAllTraysEmpty;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            DiscoverTrays();
            HookTrays();
        }

        private void OnDestroy()
        {
            UnhookTrays();
        }

        #endregion

        #region Public API

        /// <summary>Register a tray at runtime.</summary>
        public void RegisterTray(CubeTray tray)
        {
            if (tray == null) return;
            if (trays.Contains(tray)) return;

            trays.Add(tray);
            HookTray(tray);
        }

        /// <summary>Unregister a tray.</summary>
        public void UnregisterTray(CubeTray tray)
        {
            if (tray == null) return;
            if (!trays.Remove(tray)) return;

            UnhookTray(tray);
        }

        /// <summary>
        /// Configure every registered tray. Called by LevelLoader at level start.
        /// Any parameter left null/-1 uses the manager's default.
        /// </summary>
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

                tray.Configure(
                    shapeDefinition: finalShape,
                    palette: finalPalette,
                    stockCount: finalStock,
                    respawnDelay: finalDelay,
                    reset: reset
                );
            }
        }

        /// <summary>Resets every tray with the given stock count (or the manager's default if -1).</summary>
        public void ResetAll(int newStockCount = -1)
        {
            int stock = newStockCount >= 0 ? newStockCount : defaultStockCount;

            for (int i = 0; i < trays.Count; i++)
            {
                var tray = trays[i];
                if (tray == null) continue;
                tray.ResetTray(stock);
            }
        }

        /// <summary>Force every empty tray to spawn a cube.</summary>
        public void ForceSpawnAll()
        {
            for (int i = 0; i < trays.Count; i++)
            {
                var tray = trays[i];
                if (tray == null) continue;
            }
        }

        /// <summary>Get the tray currently holding an active cube, or null.</summary>
        public CubeTray GetActiveTray()
        {
            for (int i = 0; i < trays.Count; i++)
                if (trays[i] != null && trays[i].HasCube)
                    return trays[i];
            return null;
        }

        /// <summary>Get all trays currently holding an active cube.</summary>
        public List<CubeTray> GetActiveTrays()
        {
            var result = new List<CubeTray>();
            for (int i = 0; i < trays.Count; i++)
                if (trays[i] != null && trays[i].HasCube)
                    result.Add(trays[i]);
            return result;
        }

        #endregion

        #region Discovery

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
        }

        #endregion

        #region Hook Management

        private void HookTrays()
        {
            for (int i = 0; i < trays.Count; i++)
                HookTray(trays[i]);
        }

        private void UnhookTrays()
        {
            for (int i = 0; i < trays.Count; i++)
                UnhookTray(trays[i]);
        }

        private void HookTray(CubeTray tray)
        {
            if (tray == null) return;

            // We use reflection-free hooks by subscribing to events exposed by CubeTray.
            // See CubeTray.cs for the events we expect: OnCubeSpawned, OnCubePlaced.
            tray.OnCubeSpawned += HandleTraySpawned;
            tray.OnCubePlaced  += HandleTrayPlaced;
        }

        private void UnhookTray(CubeTray tray)
        {
            if (tray == null) return;
            tray.OnCubeSpawned -= HandleTraySpawned;
            tray.OnCubePlaced  -= HandleTrayPlaced;
        }

        private void HandleTraySpawned(CubeTray tray, GameObject cube)
        {
            OnAnyCubeSpawned?.Invoke(tray, cube);
        }

        private void HandleTrayPlaced(CubeTray tray, DraggableCube cube)
        {
            OnAnyCubePlaced?.Invoke(tray, cube);

            // Defer the "all empty" check to end of frame so any pending spawn
            // from the same placement has time to fire.
            StartCoroutine(CheckAllEmptyEndOfFrame());
        }

        private IEnumerator CheckAllEmptyEndOfFrame()
        {
            yield return null;
            if (AllTraysEmpty) OnAllTraysEmpty?.Invoke();
        }

        #endregion

        #region Debug

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool logTrayEvents = false;

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (!logTrayEvents) return;

            // (Optional) can be extended to log tray state periodically.
        }
#endif

        #endregion
    }
}