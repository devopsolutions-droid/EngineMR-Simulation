using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UltimateClean;

/// <summary>
/// Wires pre-placed buttons in the scroll panel to their EngineData from the registry.
/// You handle all layout/sizing in the Inspector — this script only wires the clicks and labels.
/// 
/// Setup:
///   1. Place your buttons manually under buttonContainer (Content)
///   2. Assign engineRegistry, sessionData, buttonContainer
///   3. Buttons are wired in order: first button = first engine in registry, etc.
/// </summary>
public class EngineButtonWirer : MonoBehaviour
{
    [Header("Data")]
    public EngineRegistry engineRegistry;
    public EngineSessionData sessionData;

    [Header("Scene")]
    [Tooltip("Exact name of the engine view scene in Build Settings.")]
    public string engineSceneName = "Main Scene";

    [Header("Scroll Panel")]
    [Tooltip("The Content transform that contains all your buttons.")]
    public Transform buttonContainer;

    [Header("Optional")]
    [Tooltip("Auto-set button thumbnail if button has a child Image named 'Thumbnail'.")]
    public bool setThumbnails = true;

    void Start()
    {
        if (engineRegistry == null) { Debug.LogError("[EngineButtonWirer] EngineRegistry not assigned!"); return; }
        if (sessionData    == null) { Debug.LogError("[EngineButtonWirer] SessionData not assigned!");    return; }
        if (buttonContainer == null){ Debug.LogError("[EngineButtonWirer] ButtonContainer not assigned!"); return; }

        WireButtons();
    }

    void WireButtons()
    {
        // Collect all direct children that have either Button or CleanButton
        var buttonObjects = new List<GameObject>();
        foreach (Transform child in buttonContainer)
        {
            if (child.GetComponent<Button>() != null ||
                child.GetComponent<CleanButton>() != null)
                buttonObjects.Add(child.gameObject);
        }

        // Filter out null/missing entries in the registry to prevent crashes
        var validEngines = new List<EngineData>();
        if (engineRegistry != null && engineRegistry.engines != null)
        {
            foreach (var engine in engineRegistry.engines)
            {
                if (engine != null)
                {
                    validEngines.Add(engine);
                }
            }
        }

        int engineCount = validEngines.Count;
        Debug.Log($"[EngineButtonWirer] {engineCount} valid engines, {buttonObjects.Count} buttons found.");

        for (int i = 0; i < buttonObjects.Count; i++)
        {
            if (i >= engineCount)
            {
                buttonObjects[i].SetActive(false);
                continue;
            }

            EngineData data = validEngines[i];
            GameObject btnGO = buttonObjects[i];

            // Set label
            var label = btnGO.transform.Find("Engine Name")?.GetComponent<TextMeshProUGUI>()
                     ?? btnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = data.engineName;

            // Set thumbnail
            if (setThumbnails && data.thumbnail != null)
            {
                var img = (btnGO.transform.Find("Engine Image")
                        ?? btnGO.transform.Find("Thumbnail"))
                        ?.GetComponent<Image>();
                if (img != null) img.sprite = data.thumbnail;
            }

            // Wire click — add an EngineButtonClickHandler component that listens via pointer
            EngineData captured = data;

            // Remove any existing handler first
            var existing = btnGO.GetComponent<EngineButtonClickHandler>();
            if (existing != null) Destroy(existing);

            var handler = btnGO.AddComponent<EngineButtonClickHandler>();
            handler.Init(captured, sessionData, engineSceneName);

            // Also wire standard Button if present
            var stdBtn = btnGO.GetComponent<Button>();
            if (stdBtn != null)
            {
                stdBtn.onClick.RemoveAllListeners();
                stdBtn.onClick.AddListener(() => OnEngineSelected(captured));
            }

            Debug.Log($"[EngineButtonWirer] Button[{i}] → '{data.engineName}'");
        }
    }

    void OnEngineSelected(EngineData data)
    {
        Debug.Log($"[EngineButtonWirer] Selected: {data.engineName}");
        sessionData.Select(data);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(engineSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(engineSceneName);
    }
}
