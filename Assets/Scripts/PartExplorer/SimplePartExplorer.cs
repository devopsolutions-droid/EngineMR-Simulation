using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Simple Part Explorer - Shows one engine part at a time during Show Working mode.
/// The active part lifts up for clear viewing while all other parts become X-Ray
/// transparent so the airflow tube visuals glow through. A white outline marks the
/// currently active part without hiding its surface.
/// </summary>
public class SimplePartExplorer : MonoBehaviour, IPointerClickHandler
{
    [Header("UI - Parent Panel")]
    [Tooltip("Optional parent panel for the explorer UI. Activated when explorer starts, deactivated when stops.")]
    [SerializeField] private GameObject explorerPanel;

    [Header("UI - Tablet")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image previousButtonImage;  // Assign if using raw Image buttons instead of Button components
    [SerializeField] private Image nextButtonImage;      // Assign if using raw Image buttons instead of Button components
    [SerializeField] private TextMeshProUGUI tabletPartName;
    [SerializeField] private TextMeshProUGUI tabletPartDescription;
    [SerializeField] private TextMeshProUGUI stepCounter;
    
    [Header("UI - Wall Monitor")]
    [SerializeField] private TextMeshProUGUI monitorPartName;
    [SerializeField] private TextMeshProUGUI monitorPartDescription;
    [SerializeField] private AudioSource audioSource;

    [Header("Show Working - Part Lift")]
    [Tooltip("How far the active part lifts up (local-space Y units).")]
    [SerializeField] private float liftAmount = 0.4f;
    [Tooltip("Duration of the lift/lower animation in seconds.")]
    [SerializeField] private float liftDuration = 0.35f;

    private EnginePartManifest enginePartManifest;
    private Transform engineRoot;
    private List<PartData> partsList = new List<PartData>();
    private List<EnginePart> enginePartsList = new List<EnginePart>();
    private int currentPartIndex = -1;
    private bool isExplorerActive = false;
    private JetEngineShowWorking _showWorking;
    
    private CanvasGroup previousButtonCanvasGroup;
    private CanvasGroup nextButtonCanvasGroup;

    private void Start()
    {
        // ── Set up CanvasGroups for fading out disabled buttons ──
        Image prevImg = previousButtonImage != null ? previousButtonImage : (previousButton != null ? previousButton.GetComponent<Image>() : null);
        if (prevImg != null)
        {
            previousButtonCanvasGroup = prevImg.GetComponent<CanvasGroup>();
            if (previousButtonCanvasGroup == null)
                previousButtonCanvasGroup = prevImg.gameObject.AddComponent<CanvasGroup>();
        }

        Image nextImg = nextButtonImage != null ? nextButtonImage : (nextButton != null ? nextButton.GetComponent<Image>() : null);
        if (nextImg != null)
        {
            nextButtonCanvasGroup = nextImg.GetComponent<CanvasGroup>();
            if (nextButtonCanvasGroup == null)
                nextButtonCanvasGroup = nextImg.gameObject.AddComponent<CanvasGroup>();
        }

        // Hide the explorer panel by default at start if it's assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
    }

    /// <summary>
    /// Keep IPointerClickHandler as secondary fallback for legacy image clicking
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isExplorerActive) return;

        if (previousButtonImage != null && eventData.pointerCurrentRaycast.gameObject == previousButtonImage.gameObject)
        {
            PreviousPart();
        }
        else if (nextButtonImage != null && eventData.pointerCurrentRaycast.gameObject == nextButtonImage.gameObject)
        {
            NextPart();
        }
    }

    /// <summary>
    /// Start the explorer - call this from "Show Working" button
    /// </summary>
    public void StartExplorer()
    {
        // If the interactive Show Working controller is running, do NOT delegate
        // to EngineViewManager — that would restart/kill the interactive flow.
        var interactive = FindFirstObjectByType<ShowWorkingInteractiveController>();
        if (interactive != null && interactive.IsRunning)
        {
            Debug.Log("[SimplePartExplorer] StartExplorer ignored — interactive controller is running.");
            return;
        }

        // Automatically sync with EngineViewManager if it is not already in show working mode
        EngineViewManager viewManager = FindFirstObjectByType<EngineViewManager>();
        if (viewManager != null && !EngineViewManager.IsShowWorkingActive)
        {
            Debug.Log("[SimplePartExplorer] Delegating StartExplorer to EngineViewManager to handle view transition and button states.");
            viewManager.ActivateShowWorkingView();
            return;
        }

        // Initialize audio source if null
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        if (!LoadPartsFromManifest())
        {
            Debug.LogError("SimplePartExplorer: Failed to load parts from manifest!");
            ResetShowWorkingStateOnFailure();
            return;
        }

        if (partsList.Count == 0)
        {
            Debug.LogError("SimplePartExplorer: No parts found in manifest!");
            ResetShowWorkingStateOnFailure();
            return;
        }

        isExplorerActive = true;
        currentPartIndex = -1;

        // Find show working controller
        _showWorking = FindFirstObjectByType<JetEngineShowWorking>();
        _showWorking?.OnShowWorkingStart();

        // Disable standard raycast/hover interactions while in step-by-step mode
        EngineInteractor interactor = FindFirstObjectByType<EngineInteractor>();
        if (interactor != null)
        {
            interactor.DisableInteraction();
        }

        // Show the parent panel if assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(true);
        }

        // Show first part
        NextPart();
    }

    /// <summary>
    /// Stop the explorer and restore all parts to be fully visible and original
    /// </summary>
    public void StopExplorer()
    {
        // If the interactive Show Working controller is running, do NOT delegate
        // to EngineViewManager — that would kill the entire interactive flow.
        var interactive = FindFirstObjectByType<ShowWorkingInteractiveController>();
        if (interactive != null && interactive.IsRunning)
        {
            Debug.Log("[SimplePartExplorer] StopExplorer ignored — interactive controller is running.");
            return;
        }

        // Automatically sync with EngineViewManager if it is in show working mode
        EngineViewManager viewManager = FindFirstObjectByType<EngineViewManager>();
        if (viewManager != null && EngineViewManager.IsShowWorkingActive)
        {
            Debug.Log("[SimplePartExplorer] Delegating StopExplorer to EngineViewManager to restore view states.");
            viewManager.ActivateDefaultView();
            return;
        }

        isExplorerActive = false;
        currentPartIndex = -1;

        // Reset the skipAutoCoverRemoval flag so future legacy Show Working sessions
        // correctly auto-remove covers (via HidePartsOnStart / StartAirflow).
        if (_showWorking != null)
            _showWorking.skipAutoCoverRemoval = false;

        _showWorking?.OnShowWorkingStop();

        // Restore all parts: lower back into place + restore original visuals
        foreach (var part in enginePartsList)
        {
            if (part != null)
            {
                part.LowerDown(liftDuration);
                part.SetVisible(true);
                part.RestoreOriginal();
            }
        }

        // Re-enable standard raycast/hover interactions
        EngineInteractor interactor = FindFirstObjectByType<EngineInteractor>();
        if (interactor != null)
        {
            interactor.EnableInteraction();
        }

        // Hide UI
        if (tabletPartName != null) tabletPartName.gameObject.SetActive(false);
        if (tabletPartDescription != null) tabletPartDescription.gameObject.SetActive(false);
        if (monitorPartName != null) monitorPartName.gameObject.SetActive(false);
        if (monitorPartDescription != null) monitorPartDescription.gameObject.SetActive(false);
        if (stepCounter != null) stepCounter.gameObject.SetActive(false);

        // Hide previous/next buttons if not using a parent panel
        if (previousButton != null && explorerPanel == null) previousButton.gameObject.SetActive(false);
        if (nextButton != null && explorerPanel == null) nextButton.gameObject.SetActive(false);
        if (previousButtonImage != null && explorerPanel == null) previousButtonImage.gameObject.SetActive(false);
        if (nextButtonImage != null && explorerPanel == null) nextButtonImage.gameObject.SetActive(false);

        // Hide the parent panel if assigned
        if (explorerPanel != null)
        {
            explorerPanel.SetActive(false);
        }
        
        // Initialize audio source if null
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        // Stop audio
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // Restore tablet display for standard view mode
        TabletUIController tabletUI = FindFirstObjectByType<TabletUIController>();
        if (tabletUI != null)
        {
            tabletUI.PopulateEngineDisplay();
        }
    }

    private void ResetShowWorkingStateOnFailure()
    {
        EngineViewManager viewManager = FindFirstObjectByType<EngineViewManager>();
        if (viewManager != null && EngineViewManager.IsShowWorkingActive)
        {
            viewManager.ActivateDefaultView();
        }
        else
        {
            TabletUIController tablet = FindFirstObjectByType<TabletUIController>();
            if (tablet != null) tablet.PopulateEngineDisplay();
        }
    }

    /// <summary>
    /// Move to next part
    /// </summary>
    public void NextPart()
    {
        if (!isExplorerActive) return;

        int nextIndex = currentPartIndex + 1;

        if (nextIndex >= partsList.Count)
        {
            // End of explorer, restore all
            StopExplorer();
            return;
        }

        ShowPart(nextIndex);
    }

    /// <summary>
    /// Move to previous part
    /// </summary>
    public void PreviousPart()
    {
        if (!isExplorerActive) return;

        int prevIndex = currentPartIndex - 1;

        if (prevIndex < 0)
        {
            Debug.LogWarning("SimplePartExplorer: Already at first part!");
            return;
        }

        ShowPart(prevIndex);
    }

    /// <summary>
    /// Show a specific part: makes only this part visible, others invisible.
    /// </summary>
    private void ShowPart(int index)
    {
        if (index < 0 || index >= partsList.Count) return;

        currentPartIndex = index;
        var partData = partsList[index];
        var enginePart = enginePartsList[index];

        if (enginePart == null)
        {
            Debug.LogWarning($"SimplePartExplorer: EnginePart at index {index} is null!");
            return;
        }

        // Active part — lift up + full opacity + white outline so airflow tubes stay visible
        enginePart.LowerDown(liftDuration * 0.5f);           // reset any previous lift first
        enginePart.SetVisible(true);
        enginePart.SetShowWorkingActive();
        enginePart.LiftUp(liftAmount, liftDuration);         // lift for clear viewing

        // All other parts — lower back down + X-Ray transparent so airflow glows through
        for (int i = 0; i < enginePartsList.Count; i++)
        {
            if (i == index || enginePartsList[i] == null) continue;
            enginePartsList[i].LowerDown(liftDuration * 0.5f);
            enginePartsList[i].SetVisible(true);
            enginePartsList[i].SetShowWorkingBackground();
        }

        // Update tablet UI
        if (tabletPartName != null)
        {
            tabletPartName.gameObject.SetActive(true);
            tabletPartName.text = partData.partName;
        }

        if (tabletPartDescription != null)
        {
            tabletPartDescription.gameObject.SetActive(true);
            tabletPartDescription.text = partData.description;
        }

        // Update monitor UI
        if (monitorPartName != null)
        {
            monitorPartName.gameObject.SetActive(true);
            monitorPartName.text = partData.partName;
        }

        if (monitorPartDescription != null)
        {
            monitorPartDescription.gameObject.SetActive(true);
            monitorPartDescription.text = partData.description;
        }

        // Update step counter
        if (stepCounter != null)
        {
            stepCounter.gameObject.SetActive(true);
            stepCounter.text = $"Part {index + 1} / {partsList.Count}";
        }

        // Play audio explanation
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        if (audioSource != null)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            // Find matching audio clip (prefer PartData, fallback to EnginePart)
            AudioClip clipToPlay = null;
            if (partData != null && partData.audioExplanation != null)
            {
                clipToPlay = partData.audioExplanation;
            }
            else if (enginePart != null)
            {
                clipToPlay = enginePart.AudioClip;
            }

            if (clipToPlay != null)
            {
                audioSource.clip = clipToPlay;
                audioSource.Play();
            }
        }

        // Update button states
        UpdateButtonStates();

        // Notify airflow controller
        _showWorking?.OnPartShown(enginePart.gameObject.name);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  PUBLIC METHODS — used by ShowWorkingInteractiveController
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Update the tablet + monitor TMP fields with custom text.
    /// Used by ShowWorkingInteractiveController to show step name + instruction.
    /// </summary>
    /// <param name="name">Step name (shown in part name fields).</param>
    /// <param name="description">Step instruction (shown in description fields).</param>
    /// <param name="stepNumber">Current step number (1-based).</param>
    /// <param name="totalSteps">Total number of steps.</param>
    public void SetInteractiveUIText(string name, string description, int stepNumber, int totalSteps)
    {
        // Tablet
        if (tabletPartName != null)
        {
            tabletPartName.gameObject.SetActive(true);
            tabletPartName.text = name;
        }

        if (tabletPartDescription != null)
        {
            tabletPartDescription.gameObject.SetActive(true);
            tabletPartDescription.text = description;
        }

        // Monitor
        if (monitorPartName != null)
        {
            monitorPartName.gameObject.SetActive(true);
            monitorPartName.text = name;
        }

        if (monitorPartDescription != null)
        {
            monitorPartDescription.gameObject.SetActive(true);
            monitorPartDescription.text = description;
        }

        // Step counter
        if (stepCounter != null)
        {
            stepCounter.gameObject.SetActive(true);
            stepCounter.text = $"Step {stepNumber} / {totalSteps}";
        }
    }

    /// <summary>
    /// Set a single part as the "active" visual: lift it up + white outline.
    /// Leaves all other parts as they are (call SetAllOtherPartsBackground separately).
    /// </summary>
    public void SetPartActiveVisual(EnginePart part)
    {
        if (part == null) return;

        part.LowerDown(liftDuration * 0.5f);    // reset any previous lift
        part.SetVisible(true);
        part.SetShowWorkingActive();
        part.LiftUp(liftAmount, liftDuration);
    }

    /// <summary>
    /// Set all engine parts EXCEPT the active one to background (X-Ray transparent)
    /// so the airflow tube visuals glow through.
    /// </summary>
    public void SetAllOtherPartsBackground(EnginePart activePart)
    {
        foreach (var part in enginePartsList)
        {
            if (part == null || part == activePart) continue;
            part.LowerDown(liftDuration * 0.5f);
            part.SetVisible(true);
            part.SetShowWorkingBackground();
        }
    }

    /// <summary>
    /// Hide the Next/Previous navigation buttons.
    /// Called when entering interactive mode so the user can't use buttons.
    /// </summary>
    public void HideNavigationButtons()
    {
        if (previousButton != null) previousButton.gameObject.SetActive(false);
        if (nextButton != null)     nextButton.gameObject.SetActive(false);
        if (previousButtonImage != null) previousButtonImage.gameObject.SetActive(false);
        if (nextButtonImage != null)     nextButtonImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the Next/Previous navigation buttons.
    /// Called when exiting interactive mode.
    /// </summary>
    public void ShowNavigationButtons()
    {
        if (previousButton != null) previousButton.gameObject.SetActive(true);
        if (nextButton != null)     nextButton.gameObject.SetActive(true);
        if (previousButtonImage != null) previousButtonImage.gameObject.SetActive(true);
        if (nextButtonImage != null)     nextButtonImage.gameObject.SetActive(true);

        UpdateButtonStates();
    }

    /// <summary>
    /// Load the part manifest but skip any GameObjects that were already removed
    /// (e.g. covers removed during interactive Show Working grab flow).
    /// Call BEFORE ResumeExplorerAt().
    /// </summary>
    /// <param name="skippedParts">Array of GameObjects to exclude from the explorer.</param>
    public void LoadAndSkipParts(GameObject[] skippedParts)
    {
        // Build a HashSet for fast lookup
        var skipSet = new HashSet<GameObject>();
        if (skippedParts != null)
        {
            foreach (var go in skippedParts)
                if (go != null) skipSet.Add(go);
        }

        // Find the EngineSceneLoader
        EngineSceneLoader sceneLoader = FindFirstObjectByType<EngineSceneLoader>();
        if (sceneLoader == null || sceneLoader.ActiveEngineRoot == null || sceneLoader.ActiveEngineData == null)
        {
            Debug.LogError("SimplePartExplorer: Cannot load parts — no active engine.");
            return;
        }

        engineRoot = sceneLoader.ActiveEngineRoot.transform;
        enginePartManifest = sceneLoader.ActiveEngineData.partManifest;
        if (enginePartManifest == null) return;

        partsList.Clear();
        enginePartsList.Clear();

        var partEntries = enginePartManifest.parts;
        if (partEntries == null) return;

        foreach (var entry in partEntries)
        {
            if (entry == null || entry.partData == null) continue;

            Transform partTransform = engineRoot.Find(entry.gameObjectName);
            if (partTransform == null)
                partTransform = FindChildRecursive(engineRoot, entry.gameObjectName);
            if (partTransform == null) continue;

            // Skip if this part was already removed by interactive steps
            if (skipSet.Contains(partTransform.gameObject)) continue;

            EnginePart enginePart = partTransform.GetComponent<EnginePart>();
            if (enginePart == null) continue;

            partsList.Add(entry.partData);
            enginePartsList.Add(enginePart);
        }

        Debug.Log($"[SimplePartExplorer] LoadAndSkipParts: loaded {partsList.Count} parts (skipped {skipSet.Count} removed).");
    }

    /// <summary>
    /// Start/resume the explorer at a specific part index without calling
    /// StartExplorer (which would scan for view managers etc.).
    /// Used by ShowWorkingInteractiveController after cover removal steps.
    /// </summary>
    /// <param name="startIndex">Index of the first part to show.</param>
    public void ResumeExplorerAt(int startIndex)
    {
        if (partsList == null || partsList.Count == 0)
        {
            Debug.LogError("SimplePartExplorer: No parts loaded — call LoadAndSkipParts first.");
            return;
        }

        isExplorerActive = true;
        currentPartIndex = -1;

        // Find show working controller for airflow tube updates
        _showWorking = FindFirstObjectByType<JetEngineShowWorking>();

        // Disable standard raycast/hover interactions
        EngineInteractor interactor = FindFirstObjectByType<EngineInteractor>();
        if (interactor != null) interactor.DisableInteraction();

        // Show the parent panel if assigned
        if (explorerPanel != null) explorerPanel.SetActive(true);

        // Show the UI text objects
        if (tabletPartName != null) tabletPartName.gameObject.SetActive(true);
        if (tabletPartDescription != null) tabletPartDescription.gameObject.SetActive(true);
        if (monitorPartName != null) monitorPartName.gameObject.SetActive(true);
        if (monitorPartDescription != null) monitorPartDescription.gameObject.SetActive(true);
        if (stepCounter != null) stepCounter.gameObject.SetActive(true);

        // Jump to the first part
        ShowPart(0);
    }

    /// <summary>
    /// Access the internal AudioSource reference (read-only for external callers).
    /// </summary>
    public AudioSource AudioSourceRef
    {
        get
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = FindFirstObjectByType<AudioSource>();
            }
            return audioSource;
        }
    }

    /// <summary>
    /// Update button enabled/disabled states (alpha control)
    /// </summary>
    private void UpdateButtonStates()
    {
        // Don't touch interactability when the interactive Show Working flow is running —
        // the controller manages Next/Previous itself and partsList is empty in that mode.
        var interactive = FindFirstObjectByType<ShowWorkingInteractiveController>();
        if (interactive != null && interactive.IsRunning)
        {
            if (previousButton != null) previousButton.interactable = true;
            if (nextButton != null)     nextButton.interactable = true;
            if (previousButtonCanvasGroup != null) { previousButtonCanvasGroup.alpha = 1f; previousButtonCanvasGroup.interactable = true; }
            if (nextButtonCanvasGroup != null)     { nextButtonCanvasGroup.alpha = 1f;     nextButtonCanvasGroup.interactable = true; }
            return;
        }

        bool canGoPrevious = (currentPartIndex > 0);
        bool canGoNext = (currentPartIndex < partsList.Count - 1);

        // Update Previous button
        if (previousButton != null)
        {
            previousButton.interactable = canGoPrevious;
        }
        if (previousButtonCanvasGroup != null)
        {
            previousButtonCanvasGroup.alpha = canGoPrevious ? 1f : 0.5f;
            previousButtonCanvasGroup.interactable = canGoPrevious;
        }

        // Update Next button
        if (nextButton != null)
        {
            nextButton.interactable = canGoNext;
        }
        if (nextButtonCanvasGroup != null)
        {
            nextButtonCanvasGroup.alpha = canGoNext ? 1f : 0.5f;
            nextButtonCanvasGroup.interactable = canGoNext;
        }
    }

    /// <summary>
    /// Load parts dynamically from the currently active engine's manifest
    /// </summary>
    private bool LoadPartsFromManifest()
    {
        // Find the EngineSceneLoader in the scene
        EngineSceneLoader sceneLoader = FindFirstObjectByType<EngineSceneLoader>();
        if (sceneLoader == null)
        {
            Debug.LogError("SimplePartExplorer: Could not find EngineSceneLoader in scene!");
            return false;
        }

        // Get the active root and active engine data
        if (sceneLoader.ActiveEngineRoot == null || sceneLoader.ActiveEngineData == null)
        {
            Debug.LogError("SimplePartExplorer: EngineSceneLoader has no active engine loaded!");
            return false;
        }

        engineRoot = sceneLoader.ActiveEngineRoot.transform;
        enginePartManifest = sceneLoader.ActiveEngineData.partManifest;

        if (enginePartManifest == null)
        {
            Debug.LogError($"SimplePartExplorer: Engine '{sceneLoader.ActiveEngineData.engineName}' has no part manifest assigned!");
            return false;
        }

        Debug.Log($"SimplePartExplorer: Hooked into active engine '{sceneLoader.ActiveEngineData.engineName}' using manifest '{enginePartManifest.name}'");

        partsList.Clear();
        enginePartsList.Clear();

        var partEntries = enginePartManifest.parts;
        if (partEntries == null || partEntries.Count == 0)
        {
            Debug.LogError("SimplePartExplorer: No parts defined in the active engine's manifest!");
            return false;
        }

        // Load each part in the exact index order specified in the manifest ScriptableObject
        foreach (var entry in partEntries)
        {
            if (entry == null || entry.partData == null) continue;

            // Find EnginePart child under the active engine root
            Transform partTransform = engineRoot.Find(entry.gameObjectName);
            if (partTransform == null)
            {
                // Fallback: search recursively if not a direct child
                partTransform = FindChildRecursive(engineRoot, entry.gameObjectName);
            }

            if (partTransform == null)
            {
                Debug.LogWarning($"SimplePartExplorer: Could not find part GameObject '{entry.gameObjectName}' under root '{engineRoot.name}'");
                continue;
            }

            EnginePart enginePart = partTransform.GetComponent<EnginePart>();
            if (enginePart == null)
            {
                Debug.LogWarning($"SimplePartExplorer: Part GameObject '{entry.gameObjectName}' is missing the EnginePart component!");
                continue;
            }

            partsList.Add(entry.partData);
            enginePartsList.Add(enginePart);
        }

        Debug.Log($"SimplePartExplorer: Loaded {partsList.Count} parts successfully.");
        return partsList.Count > 0;
    }

    /// <summary>
    /// Recursive child search helper
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;
            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Context menu to automatically find and wire up UI elements in the scene.
    /// Right-click the SimplePartExplorer component in the Inspector to run.
    /// </summary>
    [ContextMenu("Auto Wire UI References")]
    private void AutoWireUI()
    {
        if (audioSource == null)
            audioSource = FindFirstObjectByType<AudioSource>();

        var allText = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in allText)
        {
            string lowerName = txt.gameObject.name.ToLower();
            if (tabletPartName == null && lowerName.Contains("tablet") && lowerName.Contains("name"))
                tabletPartName = txt;
            else if (tabletPartDescription == null && lowerName.Contains("tablet") && (lowerName.Contains("desc") || lowerName.Contains("info") || lowerName.Contains("body")))
                tabletPartDescription = txt;
            else if (monitorPartName == null && (lowerName.Contains("monitor") || lowerName.Contains("wall") || lowerName.Contains("screen")) && lowerName.Contains("name"))
                monitorPartName = txt;
            else if (monitorPartDescription == null && (lowerName.Contains("monitor") || lowerName.Contains("wall") || lowerName.Contains("screen")) && (lowerName.Contains("desc") || lowerName.Contains("info") || lowerName.Contains("body")))
                monitorPartDescription = txt;
            else if (stepCounter == null && (lowerName.Contains("counter") || lowerName.Contains("step") || lowerName.Contains("index") || lowerName.Contains("number")))
                stepCounter = txt;
        }

        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            string lowerName = btn.gameObject.name.ToLower();
            if (previousButton == null && (lowerName.Contains("prev") || lowerName.Contains("back") || lowerName.Contains("left")))
                previousButton = btn;
            else if (nextButton == null && (lowerName.Contains("next") || lowerName.Contains("forward") || lowerName.Contains("right")))
                nextButton = btn;
        }

        var allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in allImages)
        {
            string lowerName = img.gameObject.name.ToLower();
            if (previousButton == null && previousButtonImage == null && (lowerName.Contains("prev") || lowerName.Contains("back") || lowerName.Contains("left")))
                previousButtonImage = img;
            else if (nextButton == null && nextButtonImage == null && (lowerName.Contains("next") || lowerName.Contains("forward") || lowerName.Contains("right")))
                nextButtonImage = img;
        }

        // Try to find the parent panel
        if (explorerPanel == null)
        {
            var allGo = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                string lowerName = go.name.ToLower();
                if (lowerName.Contains("explorerpanel") || lowerName.Contains("workingpanel") || lowerName.Contains("showworking"))
                {
                    explorerPanel = go;
                    break;
                }
            }
        }

        Debug.Log("SimplePartExplorer: Finished auto-wiring UI references!");
    }
}
