using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Dynamically updates the Tutorial Player's engine name and learning objectives UI
/// based on the active engine loaded in the scene.
/// </summary>
public class TutorialPlayerDisplay : MonoBehaviour
{
    [System.Serializable]
    public struct EngineTutorialContent
    {
        [Tooltip("The engine configuration asset.")]
        public EngineData engineData;

        [Tooltip("Custom engine name to display. If left empty, will fall back to engineData.engineName.")]
        public string customEngineName;

        [TextArea(5, 15)]
        [Tooltip("Learning objectives for this engine.")]
        public string learningObjectives;
    }

    [Header("UI Fields")]
    [Tooltip("TextMeshPro component for the engine name.")]
    public TextMeshProUGUI engineNameText;

    [Tooltip("TextMeshPro component for the learning objectives.")]
    public TextMeshProUGUI learningObjectivesText;

    [Header("Engine Contents")]
    [Tooltip("List of tutorial contents mapped to each engine.")]
    public List<EngineTutorialContent> engineContents = new List<EngineTutorialContent>();

    [Header("Fallback Defaults")]
    [Tooltip("Fallback name if no active engine is detected.")]
    public string defaultEngineName = "Engine Simulation";

    [TextArea(3, 10)]
    [Tooltip("Fallback learning objectives if no active engine is detected.")]
    public string defaultLearningObjectives = "1. Explore the engine parts in VR.\n2. Interact with the parts to learn their names.\n3. Understand how the engine assembly works.\n4. Examine the flow of energy and motion through the engine.\n5. Develop an understanding of the fundamental principles behind engine operation.";

    void Start()
    {
        UpdateDisplay();
    }

    /// <summary>
    /// Checks the current active engine and populates the text fields.
    /// </summary>
    public void UpdateDisplay(EngineData activeEngine = null)
    {
        // If no engine was passed, try to fetch it from the loader
        if (activeEngine == null)
        {
            EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
            if (loader != null)
            {
                activeEngine = loader.ActiveEngineData;
            }
        }

        if (activeEngine != null)
        {
            // Search for content matched to activeEngine
            EngineTutorialContent matchedContent = default;
            bool found = false;

            foreach (var content in engineContents)
            {
                if (content.engineData == activeEngine)
                {
                    matchedContent = content;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                // Set engine name
                string displayName = string.IsNullOrEmpty(matchedContent.customEngineName)
                    ? activeEngine.engineName
                    : matchedContent.customEngineName;

                if (engineNameText != null)
                    engineNameText.text = displayName;

                // Use custom objectives only if they have meaningful content (3+ words)
                // otherwise fall back to the default objectives
                string objectives = IsValidObjectives(matchedContent.learningObjectives)
                    ? matchedContent.learningObjectives
                    : defaultLearningObjectives;

                if (learningObjectivesText != null)
                    learningObjectivesText.text = objectives;
            }
            else
            {
                // Active engine exists but has no custom objectives in the list
                Debug.LogWarning($"[TutorialPlayerDisplay] Active engine '{activeEngine.engineName}' found but not mapped in engineContents list. Using defaults.");

                if (engineNameText != null)
                    engineNameText.text = activeEngine.engineName;

                if (learningObjectivesText != null)
                    learningObjectivesText.text = defaultLearningObjectives;
            }
        }
        else
        {
            // No active engine found in the scene (using scene defaults)
            Debug.LogWarning("[TutorialPlayerDisplay] EngineSceneLoader not found or no active engine. Using fallback defaults.");

            if (engineNameText != null)
                engineNameText.text = defaultEngineName;

            if (learningObjectivesText != null)
                learningObjectivesText.text = defaultLearningObjectives;
        }
    }

    /// <summary>
    /// Returns true if the objectives string is non-null, non-empty,
    /// and contains at least 3 words. Anything shorter is treated as
    /// a placeholder and the fallback is used instead.
    /// </summary>
    private bool IsValidObjectives(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] words = text.Split(new char[] { ' ', '\n', '\r', '\t' },
                                    System.StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 3;
    }
}
