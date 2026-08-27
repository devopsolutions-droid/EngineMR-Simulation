using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace EngineMR.UI
{
    /// <summary>
    /// Master UI Controller for EngineButtons HomeScene.
    /// Manages the 4-phase home workflow:
    ///   Phase 1: Launch the App (Welcome & Start)
    ///   Phase 2: Environment Detection (Scanning indicator)
    ///   Phase 3: Choose a Model (Grid Catalog)
    ///   Phase 4: Preview Model (Turntable 3D Preview & Selection)
    /// </summary>
    public class MRHomeFlowController : MonoBehaviour
    {
        [Header("Data Registry & Session")]
        [SerializeField] private EngineRegistry engineRegistry;
        [SerializeField] private EngineSessionData sessionData;
        [SerializeField] private string targetMRSceneName = "Main Scene";

        [Header("Panels")]
        [SerializeField] private GameObject launchPanel;
        [SerializeField] private GameObject scanningPanel;
        [SerializeField] private GameObject catalogPanel;
        [SerializeField] private GameObject previewPanel;

        [Header("Launch Panel UI")]
        [SerializeField] private Button startButton;

        [Header("Scanning Panel UI")]
        [SerializeField] private float scanDurationSeconds = 2.5f;
        [SerializeField] private TextMeshProUGUI scanStatusText;

        [Header("Catalog Panel UI")]
        [SerializeField] private Transform cardGridParent;
        [SerializeField] private GameObject engineCardPrefab;
        [SerializeField] private Button catalogBackButton;
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;
        [SerializeField] private int cardsPerPage = 6; // 2x3 grid as shown in reference design

        [Header("Preview Panel UI")]
        [SerializeField] private MRModelPreviewPanel previewController;

        [Header("Transition Settings")]
        [SerializeField] private GameObject sceneTransitionPrefab;

        private int _currentPage = 0;
        private int _totalPages = 1;
        private readonly List<GameObject> _activeCardObjects = new();

        public static bool ReturnDirectlyToCatalog = false;

        private void Start()
        {
            // Initialize scene transition manager if needed
            if (SceneTransitionManager.Instance == null && sceneTransitionPrefab != null)
            {
                Instantiate(sceneTransitionPrefab);
            }

            // Wire up Buttons & Events
            if (startButton != null)
                startButton.onClick.AddListener(OnStartButtonClicked);

            if (catalogBackButton != null)
                catalogBackButton.onClick.AddListener(OnCatalogBackClicked);

            if (prevPageButton != null)
                prevPageButton.onClick.AddListener(OnPrevPageClicked);

            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(OnNextPageClicked);

            if (previewController != null)
            {
                previewController.BindRegistry(engineRegistry);
                previewController.OnCancelClicked += OnPreviewCancelClicked;
                previewController.OnSelectConfirmed += OnModelSelectedForMR;
            }

            // Calculate total pages
            if (engineRegistry != null && engineRegistry.Count > 0)
            {
                _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)engineRegistry.Count / cardsPerPage));
            }

            // Decide start state
            if (ReturnDirectlyToCatalog)
            {
                ReturnDirectlyToCatalog = false;
                ShowCatalogPanel();
            }
            else
            {
                ShowLaunchPanel();
            }
        }

        // ── Phase 1: Launch App ───────────────────────────────────────────────

        public void ShowLaunchPanel()
        {
            SetPanelStates(launch: true, scan: false, catalog: false, preview: false);
        }

        private void OnStartButtonClicked()
        {
            StartCoroutine(RunEnvironmentScanRoutine());
        }

        // ── Phase 2: Environment Detection ───────────────────────────────────

        private IEnumerator RunEnvironmentScanRoutine()
        {
            SetPanelStates(launch: false, scan: true, catalog: false, preview: false);

            if (scanStatusText != null)
                scanStatusText.text = "Scanning your environment...";

            yield return new WaitForSeconds(scanDurationSeconds);

            ShowCatalogPanel();
        }

        // ── Phase 3: Choose a Model ──────────────────────────────────────────

        public void ShowCatalogPanel()
        {
            SetPanelStates(launch: false, scan: false, catalog: true, preview: false);
            RenderCatalogPage(_currentPage);
        }

        private void RenderCatalogPage(int page)
        {
            // Clear existing spawned cards
            foreach (var card in _activeCardObjects)
            {
                if (card != null) Destroy(card);
            }
            _activeCardObjects.Clear();

            if (engineRegistry == null || engineRegistry.Count == 0)
            {
                Debug.LogWarning("[MRHomeFlowController] EngineRegistry has no engines assigned!");
                return;
            }

            int startIndex = page * cardsPerPage;
            int endIndex = Mathf.Min(startIndex + cardsPerPage, engineRegistry.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var data = engineRegistry.Get(i);
                if (data == null) continue;

                int capturedIndex = i;
                var cardGO = Instantiate(engineCardPrefab, cardGridParent);
                var cardUI = cardGO.GetComponent<MREngineCardUI>();

                if (cardUI != null)
                {
                    cardUI.BindData(data, (selectedData) =>
                    {
                        OpenPreviewForEngine(selectedData, capturedIndex);
                    });
                }

                _activeCardObjects.Add(cardGO);
            }

            UpdatePaginationUI();
        }

        private void UpdatePaginationUI()
        {
            if (pageIndicatorText != null)
                pageIndicatorText.text = $"{_currentPage + 1} / {_totalPages}";

            if (prevPageButton != null)
                prevPageButton.interactable = _currentPage > 0;

            if (nextPageButton != null)
                nextPageButton.interactable = _currentPage < _totalPages - 1;
        }

        private void OnPrevPageClicked()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RenderCatalogPage(_currentPage);
            }
        }

        private void OnNextPageClicked()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RenderCatalogPage(_currentPage);
            }
        }

        private void OnCatalogBackClicked()
        {
            ShowLaunchPanel();
        }

        // ── Phase 4: Preview Model ───────────────────────────────────────────

        private void OpenPreviewForEngine(EngineData engineData, int index)
        {
            SetPanelStates(launch: false, scan: false, catalog: false, preview: true);

            if (previewController != null)
            {
                previewController.ShowPreview(engineData, index);
            }
        }

        private void OnPreviewCancelClicked()
        {
            ShowCatalogPanel();
        }

        private void OnModelSelectedForMR(EngineData selectedData)
        {
            if (selectedData == null) return;

            Debug.Log($"[MRHomeFlowController] Selected engine for MR: '{selectedData.engineName}'");

            if (sessionData != null)
            {
                sessionData.Select(selectedData);
            }

            // Transition to MR Surface Placement Scene (Main Scene)
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadScene(targetMRSceneName);
            }
            else
            {
                SceneManager.LoadScene(targetMRSceneName);
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────

        private void SetPanelStates(bool launch, bool scan, bool catalog, bool preview)
        {
            if (launchPanel != null) launchPanel.SetActive(launch);
            if (scanningPanel != null) scanningPanel.SetActive(scan);
            if (catalogPanel != null) catalogPanel.SetActive(catalog);
            if (previewPanel != null) previewPanel.SetActive(preview);
        }
    }
}
