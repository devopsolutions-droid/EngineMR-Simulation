using UnityEngine;
using TMPro;

/// <summary>
/// Debug utility for the tutorial system.
/// Shows real-time information about tutorial state and particle effects.
/// </summary>
public class TutorialDebugger : MonoBehaviour
{
    [SerializeField] private TutorialController tutorialController;
    [SerializeField] private EngineFlowVisualizer flowVisualizer;
    [SerializeField] private TextMeshProUGUI debugText;
    
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showIntensities = true;

    private void Start()
    {
        if (tutorialController == null)
            tutorialController = FindFirstObjectByType<TutorialController>();
        
        if (flowVisualizer == null)
            flowVisualizer = FindFirstObjectByType<EngineFlowVisualizer>();
        
        if (debugText == null)
            debugText = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (!showDebugInfo || debugText == null) return;
        
        UpdateDebugDisplay();
    }

    private void UpdateDebugDisplay()
    {
        string info = "=== TUTORIAL DEBUG ===\n";
        
        if (tutorialController != null)
        {
            info += $"Active: {tutorialController.IsTutorialActive()}\n";
            info += $"Step: {tutorialController.GetCurrentStepIndex() + 1} / {tutorialController.GetTotalSteps()}\n";
            
            var step = tutorialController.GetCurrentStep();
            if (step != null)
            {
                info += $"Title: {step.stepTitle}\n";
                info += $"Airflow: {step.showAirflow} ({step.airflowIntensity})\n";
                info += $"Combustion: {step.showCombustion} ({step.combustionIntensity})\n";
                info += $"Exhaust: {step.showExhaust} ({step.exhaustIntensity})\n";
            }
            
            info += $"Can Next: {tutorialController.CanGoNext()}\n";
            info += $"Can Prev: {tutorialController.CanGoPrevious()}\n";
        }
        
        if (showIntensities && flowVisualizer != null)
        {
            flowVisualizer.GetCurrentIntensities(out float airflow, out float combustion, out float exhaust);
            info += $"\n=== CURRENT INTENSITIES ===\n";
            info += $"Airflow: {airflow:F2}\n";
            info += $"Combustion: {combustion:F2}\n";
            info += $"Exhaust: {exhaust:F2}\n";
        }
        
        debugText.text = info;
    }

    /// <summary>
    /// Toggle debug display on/off.
    /// </summary>
    public void ToggleDebugDisplay()
    {
        showDebugInfo = !showDebugInfo;
        if (!showDebugInfo && debugText != null)
            debugText.text = "";
    }

    /// <summary>
    /// Log current tutorial state to console.
    /// </summary>
    public void LogTutorialState()
    {
        if (tutorialController == null) return;
        
        Debug.Log($"=== TUTORIAL STATE ===");
        Debug.Log($"Active: {tutorialController.IsTutorialActive()}");
        Debug.Log($"Current Step: {tutorialController.GetCurrentStepIndex()}");
        Debug.Log($"Total Steps: {tutorialController.GetTotalSteps()}");
        
        var step = tutorialController.GetCurrentStep();
        if (step != null)
        {
            Debug.Log($"Step Title: {step.stepTitle}");
            Debug.Log($"Step Description: {step.stepDescription}");
        }
    }

    /// <summary>
    /// Test navigation by going through all steps.
    /// </summary>
    public void TestAllSteps()
    {
        if (tutorialController == null) return;
        
        Debug.Log("Starting tutorial test...");
        tutorialController.StartTutorial();
        
        // Will need to be called repeatedly or use coroutine
        StartCoroutine(TestStepsCoroutine());
    }

    private System.Collections.IEnumerator TestStepsCoroutine()
    {
        while (tutorialController.CanGoNext())
        {
            yield return new WaitForSeconds(2f);
            tutorialController.NextStep();
            LogTutorialState();
        }
        
        Debug.Log("Tutorial test complete!");
    }
}
