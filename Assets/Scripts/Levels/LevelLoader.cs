using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;
using MyGame.Tray;
using MyGame.Match;

namespace MyGame.Levels
{
    /// <summary>
    /// Loads a level. Runs before every other script (DefaultExecutionOrder -1000)
    /// so the board, prefills, and trays are in a deterministic state.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LevelLoader : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Systems")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CubeTrayManager trayManager;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private LevelGoalSystem goalSystem;
        [SerializeField] private PrefilledCubeSpawner prefillSpawner;

        [Header("Level Source")]
        [SerializeField] private LevelDefinition[] levels;
        [SerializeField] private int startIndex = 0;

        [Header("Debug")]
        [SerializeField] private bool logPrefillColors = false;
        [SerializeField] private bool logRetry = true;

        #endregion

        #region Runtime State

        private int _currentIndex = -1;
        public LevelDefinition CurrentLevel =>
            (_currentIndex >= 0 && _currentIndex < levels.Length) ? levels[_currentIndex] : null;

        public int CurrentIndex => _currentIndex;
        public int LevelCount => levels != null ? levels.Length : 0;

        public event System.Action<LevelDefinition> OnLevelLoaded;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (startIndex >= 0 && startIndex < LevelCount)
                LoadIndex(startIndex);
        }

        #endregion

        #region Public API

        public void LoadIndex(int index)
        {
            if (levels == null || levels.Length == 0)
            {
                Debug.LogWarning("[LevelLoader] No levels assigned.");
                return;
            }

            index = Mathf.Clamp(index, 0, levels.Length - 1);
            var level = levels[index];
            if (level == null) return;

            if (!level.IsValid(out string error))
            {
                Debug.LogError($"[LevelLoader] Level '{level.name}' invalid: {error}", level);
                return;
            }

            _currentIndex = index;
            ApplyLevel(level);
            OnLevelLoaded?.Invoke(level);
        }

        public void LoadNext()
        {
            if (LevelCount == 0) return;
            LoadIndex((_currentIndex + 1) % LevelCount);
        }

        /// <summary>
        /// Retry: remove every prefill, placed cube, and tray cube; reset the
        /// shared sequence; then reload the current level from scratch.
        /// </summary>
        public void ReloadCurrent()
        {
            if (_currentIndex < 0)
            {
                Debug.LogWarning("[LevelLoader] ReloadCurrent: no current level.");
                return;
            }

            if (logRetry)
                Debug.Log($"[LevelLoader] Retry — removing all and reloading level {_currentIndex}.");

            RemoveAll();
            LoadIndex(_currentIndex);
        }

        #endregion

        #region Remove All

        /// <summary>
        /// Tears down all board content: prefills, placed cubes, tray cubes,
        /// sequence cursor, match subscriptions, and goal trackers.
        /// After this, the scene is empty of board content.
        /// </summary>
        public void RemoveAll()
        {
            if (prefillSpawner != null)
                prefillSpawner.ClearAll();

            if (gridManager != null)
                gridManager.ClearAllCubes();

            if (trayManager != null)
                trayManager.ResetAll();

            if (matchManager != null)
                matchManager.ClearAllSubscriptions();

            if (goalSystem != null)
                goalSystem.ClearGoals();
        }

        #endregion

        #region Apply Level

        private void ApplyLevel(LevelDefinition level)
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (trayManager == null) trayManager = FindFirstObjectByType<CubeTrayManager>();
            if (matchManager == null) matchManager = FindFirstObjectByType<MatchManager>();
            if (goalSystem == null) goalSystem = FindFirstObjectByType<LevelGoalSystem>();
            if (prefillSpawner == null) prefillSpawner = FindFirstObjectByType<PrefilledCubeSpawner>();

            // 1. Build the board
            if (gridManager != null)
                gridManager.Build(level.board);

            // 2. Spawn prefills
            if (prefillSpawner != null && level.prefill != null)
            {
                for (int i = 0; i < level.prefill.Length; i++)
                    SpawnPrefill(level.prefill[i], level.palette);
            }

            // 3. Configure trays (no reset)
            if (trayManager != null)
            {
                trayManager.ConfigureAll(
                    shapeDefinition: level.shapePool,
                    palette: level.palette,
                    stockCount: level.stockCount,
                    respawnDelay: level.respawnDelay,
                    reset: false
                );

                trayManager.SetSharedSequence(level.traySequence);
            }

            // 4. Reset trays (cursor → 0, spawn first cubes)
            if (trayManager != null)
                trayManager.ResetAll(level.stockCount);

            // 5. Match config
            if (matchManager != null)
            {
                matchManager.SetMinMatchSize(level.minMatchSize);
                matchManager.ResolveNow();
            }

            // 6. Goals
            if (goalSystem != null)
                goalSystem.LoadGoals(level.goals);

            Debug.Log($"[LevelLoader] Loaded '{level.levelName}' " +
                      $"(prefills={level.prefill?.Length ?? 0}, " +
                      $"occupied={gridManager?.GetPlacedCubeCount() ?? 0})");
        }

        private void SpawnPrefill(PrefilledCube entry, CubePalette levelPalette)
        {
            if (prefillSpawner == null) return;

            var cube = prefillSpawner.SpawnAt(entry, levelPalette);
            if (cube == null)
            {
                Debug.LogWarning($"[LevelLoader] Prefill spawn failed at cell {entry.cell}.");
                return;
            }

            var shape = cube.GetComponentInChildren<CubeShape>();
            if (shape == null) return;

            var palette = entry.paletteOverride != null ? entry.paletteOverride : levelPalette;
            ApplyPrefillColors(shape, entry.colors, palette);

            if (logPrefillColors)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[LevelLoader] Prefill {entry.shapeType} at {entry.cell}: ");
                for (int i = 0; i < shape.Blocks.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var sub = shape.Blocks[i];
                    sb.Append(sub != null ? sub.CurrentColor.ToString() : "null");
                }
                Debug.Log(sb.ToString(), cube);
            }
        }

        private void ApplyPrefillColors(CubeShape shape, CubeColor[] colors, CubePalette palette)
        {
            if (colors == null || colors.Length == 0) return;

            var blocks = shape.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;

                int idx = Mathf.Min(i, colors.Length - 1);
                var color = colors[idx];

                if (palette != null && !palette.HasEntry(color))
                    Debug.LogWarning($"[LevelLoader] Color {color} not in palette '{palette.name}'.", sub);

                sub.SetColor(color);
            }
        }

        #endregion
    }
}