using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Manages the tutorial UI panels (top panel and tablet panel).
/// Displays step information and handles navigation buttons.
/// </summary>
public class TutorialUIPanel : MonoBehaviour
{
    [Header("Tutorial Controller")]
    [SerializeField] private TutorialController tutorialController;
    
    [Header("Top Panel")]
    [SerializeField] private GameObject topPanel;
    [SerializeField] private TextMeshProUGUI stepTitleText;
    [SerializeField] private TextMeshProUGUI stepDescriptionText;
    
    [Header("Tablet Panel")]
    [SerializeField] private GameObject tabletPanel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI stepCounterText;
    
    [Header("Settings")]
    [SerializeField] private bool autoHidePanelsWhenInactive = true;

    private void Start()
    {
        if (tutorialController == null)
        {
            tutorialController = FindFirstObjectByType<TutorialController>();
        }
        
        if (tutorialController != null)
        {
            tutorialController.OnStepChanged += HandleStepChanged;
            tutorialController.OnTutorialStarted += HandleTutorialStarted;
            tutorialController.OnTutorialEnded += HandleTutorialEnded;
        }
        
        // Wire up buttons
        if (previousButton != null)
            previousButton.onClick.AddListener(OnPreviousClicked);
        
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);
        
        // Initially hide panels
        if (autoHidePanelsWhenInactive)
        {
            if (topPanel != null) topPanel.SetActive(false);
            if (tabletPanel != null) tabletPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (tutorialController != null)
        {
            tutorialController.OnStepChanged -= HandleStepChanged;
            tutorialController.OnTutorialStarted -= HandleTutorialStarted;
            tutorialController.OnTutorialEnded -= HandleTutorialEnded;
        }
        
        if (previousButton != null)
            previousButton.onClick.RemoveListener(OnPreviousClicked);
        
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextClicked);
    }

    private void HandleTutorialStarted()
    {
        // Show panels when tutorial starts
        if (topPanel != null) topPanel.SetActive(true);
        if (tabletPanel != null) tabletPanel.SetActive(true);
    }

    private void HandleTutorialEnded()
    {
        // Hide panels when tutorial ends
        if (autoHidePanelsWhenInactive)
        {
            if (topPanel != null) topPanel.SetActive(false);
            if (tabletPanel != null) tabletPanel.SetActive(false);
        }
    }

    private void HandleStepChanged(int stepIndex, TutorialStep step)
    {
        if (step == null) return;
        
        // Update top panel
        if (stepTitleText != null)
            stepTitleText.text = step.stepTitle;
        
        if (stepDescriptionText != null)
            stepDescriptionText.text = step.stepDescription;
        
        // Update step counter
        if (stepCounterText != null)
        {
            int totalSteps = tutorialController.GetTotalSteps();
            stepCounterText.text = $"Step {stepIndex + 1} / {totalSteps}";
        }
        
        // Update button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (previousButton != null)
            previousButton.interactable = tutorialController.CanGoPrevious();
        
        if (nextButton != null)
            nextButton.interactable = tutorialController.CanGoNext();
    }

    private void OnPreviousClicked()
    {
        if (tutorialController != null)
            tutorialController.PreviousStep();
    }

    private void OnNextClicked()
    {
        if (tutorialController != null)
            tutorialController.NextStep();
    }

    /// <summary>
    /// Manually show the tutorial panels.
    /// </summary>
    public void ShowPanels()
    {
        if (topPanel != null) topPanel.SetActive(true);
        if (tabletPanel != null) tabletPanel.SetActive(true);
    }

    /// <summary>
    /// Manually hide the tutorial panels.
    /// </summary>
    public void HidePanels()
    {
        if (topPanel != null) topPanel.SetActive(false);
        if (tabletPanel != null) tabletPanel.SetActive(false);
    }
}
