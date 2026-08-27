using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the full tablet UI flow in the Main Scene.
///
/// Inspector wiring for each button's On Clicked ():
///   START Button            → OnStartClicked
///   X-Ray Button            → OnXRayClicked
///   X-Ray Button Reset      → OnXRayResetClicked
///   Explode                 → OnExplodeClicked
///   Disassemble Button      → OnExplodeClicked
///   Default View            → OnDefaultViewClicked
///   Assemble Button         → OnDefaultViewClicked
///   Exit Button             → OnExitClicked
///   BACK Button             → OnBackClicked
/// </summary>
public class TabletUIController : MonoBehaviour
{
    // ── Scene Systems ─────────────────────────────────────────────────────────
    [Header("Scene Systems")]
    public EngineViewManager engineViewManager;
    public EngineInteractor  engineInteractor;
    public EngineData        currentEngineData;

    // ── Panels ────────────────────────────────────────────────────────────────
    [Header("Panels")]
    public GameObject loadingScreenPanel;
    public GameObject mainMenuPanel;
    public GameObject featuresPanel;
    public GameObject separatedViewModesPanel;

    // ── Loading Screen ────────────────────────────────────────────────────────
    [Header("Loading Screen")]
    public GameObject startButtonGO;
    public GameObject progressBarGO;
    [Tooltip("How long the loading bar runs before the main menu appears and interactions unlock.")]
    public float      loadingDuration = 6f;

    // ── Engine Display Area ───────────────────────────────────────────────────
    [Header("Engine Display Area")]
    public Image           engineDisplayImage;

    [Tooltip("Header on the tablet; set from EngineData.engineName when the scene loads.")]
    public TextMeshProUGUI engineNameText;

    [Tooltip("Engine overview from EngineData (what it is, role, typical use). Switches to part description on hover/select.")]
    public TextMeshProUGUI engineDescriptionText;

    [Tooltip("Shows selected part name. Empty by default.")]
    public TextMeshProUGUI partNameText;

    [Header("Assembly Progress")]
    [Tooltip("Drag a TMP text here to show '3 of 8 parts assembled'.")]
    public TextMeshProUGUI assemblyProgressText;

    [Header("Free Grab")]
    public FreeGrabController freeGrabController;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Coroutine _loadingCoroutine;
    private ShowWorkingInteractiveController _cachedInteractive;
    private SimpleShowWorkingController _cachedSimpleShowWorking;
    private SimplePartExplorer _cachedSimpleExplorer;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(false);
        if (progressBarGO != null)      progressBarGO.SetActive(false);

        if (engineInteractor != null)
        {
            engineInteractor.OnPartSelected += HandlePartSelected;
            engineInteractor.OnPartHovered  += HandlePartHovered;
        }

        // Cache FindFirstObjectByType results once to avoid per-click scene scans
        _cachedSimpleShowWorking = FindFirstObjectByType<SimpleShowWorkingController>();
        _cachedInteractive = FindFirstObjectByType<ShowWorkingInteractiveController>();
        _cachedSimpleExplorer = FindFirstObjectByType<SimplePartExplorer>();

        // Subscribe to assembly step events
        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null)
            grabManager.OnStepCompleted += HandleAssemblyStepCompleted;

        // Wait one frame for EngineSceneLoader to call SetEngineData()
        // before populating the display
        StartCoroutine(PopulateAfterLoad());
    }

    System.Collections.IEnumerator PopulateAfterLoad()
    {
        yield return null; // wait one frame
        PopulateEngineDisplay();
    }

    void OnDestroy()
    {
        if (engineInteractor != null)
        {
            engineInteractor.OnPartSelected -= HandlePartSelected;
            engineInteractor.OnPartHovered  -= HandlePartHovered;
        }
        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null)
            grabManager.OnStepCompleted -= HandleAssemblyStepCompleted;
    }

    // ── Button Methods ────────────────────────────────────────────────────────

    public void OnStartClicked()
    {
        if (startButtonGO != null) startButtonGO.SetActive(false);

        // Disable then re-enable forces SliderAnimation.Awake() to run again → resets to 0
        if (progressBarGO != null)
        {
            progressBarGO.SetActive(false);
            progressBarGO.SetActive(true);
        }

        if (_loadingCoroutine != null) StopCoroutine(_loadingCoroutine);
        _loadingCoroutine = StartCoroutine(RunLoadingBar());
    }

    public void OnXRayClicked()
    {
        engineViewManager?.ActivateXRayView();
    }

    public void OnXRayResetClicked()
    {
        engineViewManager?.ActivateDefaultView();
    }

    public void OnExplodeClicked()
    {
        engineViewManager?.ActivateExplodedView();
    }

    public void OnDefaultViewClicked()
    {
        engineViewManager?.ActivateDefaultView();
    }

    public void OnGrabClicked()
    {
        engineViewManager?.ActivateGrabMode();
    }

    public void OnReassembleClicked()
    {
        engineViewManager?.DeactivateGrabMode();
    }

    public void OnShowWorkingClicked()
    {
        Debug.Log("[TabletUIController] OnShowWorkingClicked() called");
        if (engineViewManager == null)
            Debug.LogError("[TabletUIController] engineViewManager reference is missing (NULL)!");
        engineViewManager?.ActivateShowWorkingView();
    }

    public void OnNextClicked()
    {
        Debug.Log("[TabletUIController] OnNextClicked() called");

        // Find active controller polymorphically
        IShowWorkingController activeController = null;
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader != null && loader.ActiveEngineRoot != null)
        {
            activeController = loader.ActiveEngineRoot.GetComponentInChildren<IShowWorkingController>();
        }

        if (activeController == null)
        {
            // Fallback to scene-level cached references
            if (_cachedSimpleShowWorking != null && _cachedSimpleShowWorking.IsRunning)
                activeController = _cachedSimpleShowWorking;
            else if (_cachedInteractive != null && _cachedInteractive.IsRunning)
                activeController = _cachedInteractive;
        }

        if (activeController != null && activeController.IsRunning)
        {
            Debug.Log($"[TabletUIController] Routing OnNextClicked to active controller: {activeController.GetType().Name}");
            activeController.OnNextPressed();
            return;
        }

        // Legacy fallback: navigate parts via SimplePartExplorer
        if (_cachedSimpleExplorer == null)
            Debug.LogError("[TabletUIController] SimplePartExplorer not found in scene!");
        _cachedSimpleExplorer?.NextPart();
    }

    public void OnPreviousClicked()
    {
        Debug.Log("[TabletUIController] OnPreviousClicked() called");

        // Find active controller polymorphically
        IShowWorkingController activeController = null;
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader != null && loader.ActiveEngineRoot != null)
        {
            activeController = loader.ActiveEngineRoot.GetComponentInChildren<IShowWorkingController>();
        }

        if (activeController == null)
        {
            // Fallback to scene-level cached references
            if (_cachedSimpleShowWorking != null && _cachedSimpleShowWorking.IsRunning)
                activeController = _cachedSimpleShowWorking;
            else if (_cachedInteractive != null && _cachedInteractive.IsRunning)
                activeController = _cachedInteractive;
        }

        if (activeController != null && activeController.IsRunning)
        {
            Debug.Log($"[TabletUIController] Routing OnPreviousClicked to active controller: {activeController.GetType().Name}");
            activeController.OnPreviousPressed();
            return;
        }

        // Legacy fallback: navigate parts via SimplePartExplorer
        if (_cachedSimpleExplorer == null)
            Debug.LogError("[TabletUIController] SimplePartExplorer not found in scene!");
        _cachedSimpleExplorer?.PreviousPart();
    }

    /// <summary>
    /// Called by Start Turbine and Ignite buttons in the Unity Inspector.
    /// Routes to ShowWorkingInteractiveController.OnSpecialButtonPressed().
    /// </summary>
    public void OnSpecialButtonPressed()
    {
        Debug.Log("[TabletUIController] OnSpecialButtonPressed() called");
        if (_cachedInteractive != null && _cachedInteractive.IsRunning)
        {
            _cachedInteractive.OnSpecialButtonPressed();
        }
        else
        {
            Debug.LogWarning("[TabletUIController] OnSpecialButtonPressed called but interactive controller is not running.");
        }
    }

    public void OnStopShowWorkingClicked()
    {
        Debug.Log("[TabletUIController] OnStopShowWorkingClicked() called");
        if (engineViewManager == null)
            Debug.LogError("[TabletUIController] engineViewManager reference is missing (NULL)!");
        engineViewManager?.ActivateDefaultView();
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnBackClicked()
    {
        var loader = FindFirstObjectByType<EngineSceneLoader>();
        loader?.GoHome();
    }

    public void OnOpenSeparatedViewModes()
    {
        if (featuresPanel != null)           featuresPanel.SetActive(false);
        if (separatedViewModesPanel != null) separatedViewModesPanel.SetActive(true);
    }

    public void OnManualSeparateClicked()
    {
        engineViewManager?.ActivateManualGrabMode();
    }

    public void OnBackFromSeparatedViewModes()
    {
        if (separatedViewModesPanel != null) separatedViewModesPanel.SetActive(false);
        if (featuresPanel != null)           featuresPanel.SetActive(true);
    }

    /// <summary>Wire to the Back button inside Main Menu to return to Loading Screen.</summary>
    public void OnBackToLoadingScreen()
    {
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);

        // Reset loading screen state
        if (startButtonGO != null)  startButtonGO.SetActive(true);

        // Fully reset the progress bar by disabling then re-enabling
        // SliderAnimation resets to 0 on Awake, so toggling forces a clean restart
        if (progressBarGO != null)
        {
            progressBarGO.SetActive(false);
            // Keep it hidden — it will show again when START is clicked
        }

        // Stop any running loading coroutine
        if (_loadingCoroutine != null)
        {
            StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = null;
        }
    }

    // ── Loading Bar ───────────────────────────────────────────────────────────

    IEnumerator RunLoadingBar()
    {
        yield return new WaitForSeconds(loadingDuration);

        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
        if (mainMenuPanel != null)      mainMenuPanel.SetActive(true);

        // Tablet was hidden during loading — refresh engine copy so TMP layout is correct
        PopulateEngineDisplay();

        // Unlock all engine interactions now that loading is complete
        engineInteractor?.EnableInteraction();
        engineViewManager?.EnableViewButtons();

        _loadingCoroutine = null;
    }

    // ── Button State Sync ─────────────────────────────────────────────────────
    // Button visibility is now controlled entirely by EngineViewManager.cs
    // No button state management here.

    // ── Engine Display ────────────────────────────────────────────────────────

    public void PopulateEngineDisplay()
    {
        if (currentEngineData == null) return;

        if (engineNameText != null)
        {
            engineNameText.gameObject.SetActive(true);
            engineNameText.text = currentEngineData.engineName;
        }

        if (engineDescriptionText != null)
        {
            engineDescriptionText.gameObject.SetActive(true);
            engineDescriptionText.text = currentEngineData.engineDescription ?? "";
            engineDescriptionText.ForceMeshUpdate(true);
        }

        if (partNameText != null)
        {
            partNameText.gameObject.SetActive(true);
            partNameText.text = "";
        }

        if (engineDisplayImage != null && currentEngineData.thumbnail != null)
            engineDisplayImage.sprite = currentEngineData.thumbnail;
    }

    /// <summary>Called by EngineSceneLoader after engine activates.</summary>
    public void SetEngineData(EngineData data)
    {
        currentEngineData = data;
        PopulateEngineDisplay();
    }

    // ── Part Info Handlers ────────────────────────────────────────────────────

    void HandleAssemblyStepCompleted(int stepsCompleted, int total)
    {
        if (assemblyProgressText != null)
            assemblyProgressText.text = $"{stepsCompleted} of {total} parts assembled";
    }

    void HandlePartSelected(EnginePart part)
    {
        Debug.Log($"[TabletUIController] HandlePartSelected: {(part != null ? part.PartName : "null")}");

        // In step-by-step assembly grab mode, do not override step UI
        if (EngineViewManager.IsGrabModeActive && !EngineViewManager.IsManualGrabModeActive) return;

        if (part == null)
        {
            // Deselected — reset to engine defaults
            if (partNameText != null)
            {
                partNameText.gameObject.SetActive(true);
                partNameText.text = "";
            }
            if (engineDescriptionText != null)
            {
                engineDescriptionText.gameObject.SetActive(true);
                if (EngineViewManager.IsManualGrabModeActive)
                {
                    engineDescriptionText.text = "Grab and move any part of the engine freely to inspect its design.";
                }
                else if (currentEngineData != null)
                {
                    engineDescriptionText.text = currentEngineData.engineDescription;
                }
            }
        }
        else
        {
            // Part selected — lock name and description to this part
            if (partNameText != null)
            {
                partNameText.gameObject.SetActive(true);
                partNameText.text = part.PartName;
            }
            if (engineDescriptionText != null)
            {
                engineDescriptionText.gameObject.SetActive(true);
                engineDescriptionText.text = part.Description;
            }
        }
    }

    void HandlePartHovered(EnginePart part)
    {
        // In step-by-step assembly grab mode, strictly display step info — do not override
        if (EngineViewManager.IsGrabModeActive && !EngineViewManager.IsManualGrabModeActive) return;

        // NEVER override display when a part is already selected
        if (engineInteractor != null && engineInteractor.HasActivePart)
        {
            Debug.Log($"[TabletUIController] HandlePartHovered blocked for: {(part != null ? part.PartName : "null")} because HasActivePart is true.");
            return;
        }

        Debug.Log($"[TabletUIController] HandlePartHovered processed for: {(part != null ? part.PartName : "null")}");

        if (EngineViewManager.IsManualGrabModeActive)
        {
            if (part == null)
            {
                if (partNameText != null)
                {
                    partNameText.gameObject.SetActive(true);
                    partNameText.text = "";
                }
                if (engineDescriptionText != null)
                {
                    engineDescriptionText.gameObject.SetActive(true);
                    engineDescriptionText.text = "Grab and move any part of the engine freely to inspect its design.";
                }
            }
            else
            {
                if (partNameText != null)
                {
                    partNameText.gameObject.SetActive(true);
                    partNameText.text = part.PartName;
                }
                if (engineDescriptionText != null)
                {
                    engineDescriptionText.gameObject.SetActive(true);
                    engineDescriptionText.text = part.Description;
                }
            }
            return;
        }

        if (part == null)
        {
            if (partNameText != null)
            {
                partNameText.gameObject.SetActive(true);
                partNameText.text = "";
            }

            // In Show Working mode, keep engineDescriptionText showing the step
            // description — don't overwrite with the generic engine description.
            if (!EngineViewManager.IsShowWorkingActive)
            {
                if (engineDescriptionText != null && currentEngineData != null)
                {
                    engineDescriptionText.gameObject.SetActive(true);
                    engineDescriptionText.text = currentEngineData.engineDescription;
                }
            }
        }
        else
        {
            if (partNameText != null)
            {
                partNameText.gameObject.SetActive(true);
                partNameText.text = part.PartName;
            }

            // In Show Working mode, leave engineDescriptionText showing the step
            // description so users can see both the part name (partNameText) and
            // the current step description (engineDescriptionText) simultaneously.
            if (!EngineViewManager.IsShowWorkingActive && engineDescriptionText != null)
            {
                engineDescriptionText.gameObject.SetActive(true);
                engineDescriptionText.text = part.Description;
            }
        }
    }
}
