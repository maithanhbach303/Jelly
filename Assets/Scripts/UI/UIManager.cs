using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyGame.Board;
using MyGame.Interaction;
using MyGame.Levels;
using MyGame.Tray;

namespace MyGame.UI
{
    public class UIManager : MonoBehaviour
    {
        #region Inspector

        [Header("Systems")]
        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private LevelGoalSystem goalSystem;
        [SerializeField] private CubeTrayManager trayManager;
        [SerializeField] private GridManager gridManager;

        [Header("HUD")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private TMP_Text levelNameText;
        [SerializeField] private TMP_Text trayStockText;
        [SerializeField] private Button restartButton;

        [Header("Goal List")]
        [SerializeField] private Transform goalListParent;
        [SerializeField] private GoalRowUI goalRowPrefab;

        [Header("Win Panel")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TMP_Text winTitleText;
        [SerializeField] private Button winNextButton;
        [SerializeField] private Button winRetryButton;

        [Header("Lose Panel")]
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text loseTitleText;
        [SerializeField] private Button loseRetryButton;
        [SerializeField] private Button loseMenuButton;

        #endregion

        #region Runtime State

        private readonly List<GoalRowUI> _goalRows = new();
        private bool _subscribedLevel;
        private bool _subscribedGoals;
        private bool _subscribedTray;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            AutoFindSystems();
            HookButtons();
        }

        private void OnEnable()
        {
            SubscribeAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void Start()
        {
            // HideAll();
        }

        #endregion

        #region Setup

        private void AutoFindSystems()
        {
            if (levelLoader == null) levelLoader = FindFirstObjectByType<LevelLoader>();
            if (goalSystem == null)  goalSystem  = FindFirstObjectByType<LevelGoalSystem>();
            if (trayManager == null) trayManager = FindFirstObjectByType<CubeTrayManager>();
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        }

        private void HookButtons()
        {
            if (restartButton != null)   restartButton.onClick.AddListener(OnRestartClicked);
            if (winNextButton != null)   winNextButton.onClick.AddListener(OnNextClicked);
            if (winRetryButton != null)  winRetryButton.onClick.AddListener(OnRetryClicked);
            if (loseRetryButton != null) loseRetryButton.onClick.AddListener(OnRetryClicked);
            if (loseMenuButton != null)  loseMenuButton.onClick.AddListener(OnMenuClicked);
        }

        #endregion

        #region Subscriptions

        private void SubscribeAll()
        {
            if (!_subscribedLevel && levelLoader != null)
            {
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
                _subscribedLevel = true;
            }

            if (!_subscribedGoals && goalSystem != null)
            {
                goalSystem.OnGoalCompleted += HandleGoalCompleted;
                goalSystem.OnAllGoalsCompleted += HandleAllGoalsCompleted;
                _subscribedGoals = true;
            }

            if (!_subscribedTray && trayManager != null)
            {
                trayManager.OnAnyCubeSpawned += HandleTraySpawned;
                trayManager.OnAllTraysEmpty += HandleAllTraysEmpty;
                _subscribedTray = true;
            }
        }

        private void UnsubscribeAll()
        {
            if (_subscribedLevel && levelLoader != null)
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
            _subscribedLevel = false;

            if (_subscribedGoals && goalSystem != null)
            {
                goalSystem.OnGoalCompleted -= HandleGoalCompleted;
                goalSystem.OnAllGoalsCompleted -= HandleAllGoalsCompleted;
            }
            _subscribedGoals = false;

            if (_subscribedTray && trayManager != null)
            {
                trayManager.OnAnyCubeSpawned -= HandleTraySpawned;
                trayManager.OnAllTraysEmpty -= HandleAllTraysEmpty;
            }
            _subscribedTray = false;
        }

        #endregion

        #region Event Handlers

        private void HandleLevelLoaded(LevelDefinition level)
        {
            HideAll();
            ShowHUD();

            if (levelNameText != null)
                levelNameText.text = level.levelName;

            RebuildGoalRows(level);
            RefreshTrayStock();
        }

        private void HandleGoalCompleted(GoalTracker tracker)
        {
            Debug.Log($"[UIManager] Goal complete: {tracker.Goal.GetSummary()}");
        }

        private void HandleAllGoalsCompleted()
        {
            ShowWin();
        }

        private void HandleTraySpawned(CubeTray tray, GameObject cube)
        {
            RefreshTrayStock();
        }

        private void HandleAllTraysEmpty()
        {
            if (goalSystem != null && !goalSystem.AllGoalsComplete)
                ShowLose();
        }

        #endregion

        #region Panels

        public void HideAll()
        {
            if (hudPanel != null) hudPanel.SetActive(false);
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        public void ShowHUD()
        {
            if (hudPanel != null) hudPanel.SetActive(true);
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        public void ShowWin()
        {
            if (winPanel != null) winPanel.SetActive(true);
            if (losePanel != null) losePanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);

            if (winTitleText != null) winTitleText.text = "Level Complete!";
        }

        public void ShowLose()
        {
            if (losePanel != null) losePanel.SetActive(true);
            if (winPanel != null) winPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);

            if (loseTitleText != null) loseTitleText.text = "Out of Moves";
        }

        #endregion

        #region Goal Rows

        private void RebuildGoalRows(LevelDefinition level)
        {
            ClearGoalRows();

            if (goalRowPrefab == null || goalListParent == null) return;
            if (goalSystem == null || goalSystem.Trackers.Count == 0) return;

            var palette = level != null ? level.palette : null;

            for (int t = 0; t < goalSystem.Trackers.Count; t++)
            {
                var tracker = goalSystem.Trackers[t];
                if (tracker == null || tracker.Goal == null) continue;

                int reqCount = tracker.Goal.RequirementCount;
                for (int i = 0; i < reqCount; i++)
                {
                    var row = Instantiate(goalRowPrefab, goalListParent);
                    row.Bind(tracker, i, palette);
                    _goalRows.Add(row);
                }
            }
        }

        private void ClearGoalRows()
        {
            for (int i = 0; i < _goalRows.Count; i++)
                if (_goalRows[i] != null) Destroy(_goalRows[i].gameObject);
            _goalRows.Clear();
        }

        #endregion

        #region Tray Stock

        private void RefreshTrayStock()
        {
            if (trayStockText == null || trayManager == null) return;

            int total = 0;
            bool infinite = false;

            for (int i = 0; i < trayManager.TrayCount; i++)
            {
                var t = trayManager.Trays[i];
                if (t == null) continue;

                if (t.RemainingStock == int.MaxValue) infinite = true;
                else total += t.RemainingStock;
            }

            trayStockText.text = infinite ? "∞" : total.ToString();
        }

        #endregion

        #region Button Handlers

        private void OnRestartClicked() { if (levelLoader != null) levelLoader.ReloadCurrent(); }
        private void OnNextClicked()    { if (levelLoader != null) levelLoader.LoadNext(); }
        private void OnRetryClicked()   { if (levelLoader != null) levelLoader.ReloadCurrent(); }
        private void OnMenuClicked()    { Debug.Log("[UIManager] Menu clicked."); }

        #endregion
    }
}