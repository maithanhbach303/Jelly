using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;
using MyGame.Tray;
using MyGame.Match;

namespace MyGame.Levels
{
    public class LevelLoader : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Systems")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CubeTrayManager trayManager;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private LevelGoalSystem goalSystem;

        [Header("Level Source")]
        [SerializeField] private LevelDefinition[] levels;
        [SerializeField] private int startIndex = 0;

        #endregion

        #region Runtime State

        private int _currentIndex = -1;

        public LevelDefinition CurrentLevel =>
            (_currentIndex >= 0 && _currentIndex < levels.Length)
                ? levels[_currentIndex]
                : null;

        public int CurrentIndex => _currentIndex;
        public int LevelCount => levels != null ? levels.Length : 0;

        public event System.Action<LevelDefinition> OnLevelLoaded;

        #endregion

        #region Lifecycle

        private void Start()
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
            int next = (_currentIndex + 1) % LevelCount;
            LoadIndex(next);
        }

        public void ReloadCurrent()
        {
            if (_currentIndex >= 0) LoadIndex(_currentIndex);
        }

        #endregion

        #region Apply

        private void ApplyLevel(LevelDefinition level)
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (trayManager == null) trayManager = FindFirstObjectByType<CubeTrayManager>();
            if (matchManager == null) matchManager = FindFirstObjectByType<MatchManager>();
            if (goalSystem == null) goalSystem = FindFirstObjectByType<LevelGoalSystem>();

            if (gridManager != null)
                gridManager.Build(level.board);

            if (trayManager != null)
            {
                trayManager.ConfigureAll(
                    shapeDefinition: level.shapePool,
                    palette: level.palette,
                    stockCount: level.stockCount,
                    respawnDelay: level.respawnDelay,
                    reset: true
                );
            }

            if (matchManager != null)
                matchManager.SetMinMatchSize(level.minMatchSize);

            if (goalSystem != null)
                goalSystem.LoadGoals(level.goals);

            Debug.Log($"[LevelLoader] Loaded level '{level.levelName}' " +
                      $"(board={level.board.boardName}, minMatch={level.minMatchSize})");
        }

        #endregion
    }
}