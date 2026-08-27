using System;
using UnityEngine;

[Serializable]
public class EngineSceneEntry
{
    public EngineData engineData;
    public GameObject sceneRoot;

    [Tooltip("(Optional) The same engine with parts manually placed in their dismantled/exploded positions. " +
             "Place it in the scene (inactive is fine). When assigned, the Explode button animates each part " +
             "to its matching position here instead of auto-calculating. Parts matched by GameObject name.")]
    public GameObject dismantledSceneRoot;

    [Tooltip("(Optional) Dedicated model for the Show Working tour. When assigned, this model is activated " +
             "during Show Working/Explorer mode while the standard model is disabled.")]
    public GameObject swapModelOnSW;
}

/// <summary>
/// Place this in the Main Scene.
/// Maps each EngineData to its pre-placed scene root via EngineSceneEntry list.
/// On Start, activates only the selected engine and deactivates all others.
/// </summary>
public class EngineSceneLoader : MonoBehaviour
{
    [Header("Session")]
    public EngineSessionData sessionData;

    [Header("Engine Scene Roots")]
    [Tooltip("Pair each EngineData asset with its pre-placed root GameObject in this scene.")]
    public EngineSceneEntry[] engineEntries;

    public EngineData ActiveEngineData { get; private set; }
    public GameObject ActiveEngineRoot { get; set; }

    [Header("Scene References")]
    public EngineViewManager engineViewManager;
    public EngineInteractor engineInteractor;
    public PartInfoPanel infoPanel;
    public TabletUIController tabletUIController;

    [Header("Fallback")]
    [Tooltip("Used when entering the scene directly in Editor without a selection.")]
    public EngineData fallbackEngine;

    [Header("Back Button")]
    public string homeSceneName = "EngineButtons HomeScene";

    void Awake()
    {
        if (tabletUIController == null)
            tabletUIController = FindFirstObjectByType<TabletUIController>();
    }

    void Start()
    {
        EngineData toLoad = (sessionData != null && sessionData.HasSelection)
            ? sessionData.selectedEngine
            : fallbackEngine;

        if (toLoad == null)
        {
            Debug.LogError("[EngineSceneLoader] No engine selected and no fallback assigned!");
            return;
        }

        ActivateEngine(toLoad);
        sessionData?.Clear();
    }

    void ActivateEngine(EngineData selected)
    {
        ActiveEngineData = selected;
        GameObject activeRoot      = null;
        GameObject dismantledRoot  = null;

        foreach (var entry in engineEntries)
        {
            if (entry.sceneRoot == null) continue;
            bool isSelected = entry.engineData == selected;
            entry.sceneRoot.SetActive(isSelected);

            // Always keep dismantled root inactive — EngineViewManager reads positions from it
            if (entry.dismantledSceneRoot != null)
                entry.dismantledSceneRoot.SetActive(false);

            // Always keep show working root inactive by default at load time
            if (entry.swapModelOnSW != null)
                entry.swapModelOnSW.SetActive(false);

            if (isSelected)
            {
                activeRoot     = entry.sceneRoot;
                dismantledRoot = entry.dismantledSceneRoot; // may be null — that's fine
            }
        }

        ActiveEngineRoot = activeRoot;

        if (activeRoot == null)
        {
            Debug.LogError($"[EngineSceneLoader] No sceneRoot found for '{selected.engineName}'. Check engineEntries list.");
            return;
        }

        if (infoPanel != null)
            infoPanel.SetDefault(selected.engineName, selected.engineDescription);

        if (engineViewManager != null)
        {
            // Dynamically assign engineRoot to match the currently active engine model
            engineViewManager.engineRoot = activeRoot.transform;

            // Dynamically assign XRayVisionController's targetRoot to collect the active engine's renderers
            if (engineViewManager.xRayController != null)
            {
                engineViewManager.xRayController.targetRoot = activeRoot.transform;
            }

            // Pass the dismantled scene root (or null) before refreshing
            engineViewManager.dismantledSceneRoot = dismantledRoot;
            engineViewManager.RefreshAfterLoad();
        }

        if (tabletUIController != null)
            tabletUIController.SetEngineData(selected);

        // Dynamically update the Tutorial Player display
        TutorialPlayerDisplay tutorialDisplay = FindFirstObjectByType<TutorialPlayerDisplay>();
        if (tutorialDisplay != null)
            tutorialDisplay.UpdateDisplay(selected);

        Debug.Log($"[EngineSceneLoader] Activated: {selected.engineName}" +
                  (dismantledRoot != null ? $" | Dismantled root: {dismantledRoot.name}" : " | No dismantled root"));
    }

    /// <summary>Called by the Back button in the engine scene.</summary>
    public void GoHome()
    {
        HomeSceneUIController.ReturnToScroll = true;

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(homeSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(homeSceneName);
    }

    /// <summary>Gets the active engine entry mapping.</summary>
    public EngineSceneEntry GetActiveEntry()
    {
        if (ActiveEngineData == null) return null;
        foreach (var entry in engineEntries)
        {
            if (entry.engineData == ActiveEngineData)
                return entry;
        }
        return null;
    }
}
