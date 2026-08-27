using UnityEngine;
using System;

/// <summary>
/// Main controller for the step-by-step engine tutorial.
/// Manages tutorial state, progression, and synchronization with UI and visuals.
/// </summary>
public class TutorialController : MonoBehaviour
{
    [SerializeField] private TutorialData tutorialData;
    [SerializeField] private EngineFlowVisualizer flowVisualizer;
    
    [Header("Auto-Play Settings")]
    [SerializeField] private bool enableAutoPlay = false;
    [SerializeField] private float autoPlayStepDuration = 5f;
    
    private int _currentStepIndex = -1;
    private bool _isTutorialActive = false;
    private float _autoPlayTimer = 0f;
    
    // Events for UI synchronization
    public event Action<int, TutorialStep> OnStepChanged;
    public event Action OnTutorialStarted;
    public event Action OnTutorialEnded;

    private void Start()
    {
        if (tutorialData == null)
        {
            Debug.LogWarning("TutorialController: No TutorialData assigned!");
            return;
        }
        
        if (flowVisualizer == null)
        {
            flowVisualizer = GetComponent<EngineFlowVisualizer>();
            if (flowVisualizer == null)
            {
                Debug.LogWarning("TutorialController: No EngineFlowVisualizer found!");
            }
        }
    }

    private void Update()
    {
        if (!_isTutorialActive || !enableAutoPlay) return;
        
        _autoPlayTimer += Time.deltaTime;
        if (_autoPlayTimer >= autoPlayStepDuration)
        {
            _autoPlayTimer = 0f;
            NextStep();
        }
    }

    /// <summary>
    /// Starts the tutorial from the first step.
    /// </summary>
    public void StartTutorial()
    {
        if (tutorialData == null || tutorialData.GetStepCount() == 0)
        {
            Debug.LogWarning("TutorialController: No tutorial steps available!");
            return;
        }
        
        _isTutorialActive = true;
        _currentStepIndex = -1;
        _autoPlayTimer = 0f;
        
        OnTutorialStarted?.Invoke();
        
        // Move to first step
        NextStep();
    }

    /// <summary>
    /// Ends the tutorial and stops all effects.
    /// </summary>
    public void EndTutorial()
    {
        _isTutorialActive = false;
        _currentStepIndex = -1;
        
        if (flowVisualizer != null)
            flowVisualizer.StopAllFlows();
        
        OnTutorialEnded?.Invoke();
    }

    /// <summary>
    /// Moves to the next tutorial step.
    /// </summary>
    public void NextStep()
    {
        if (!_isTutorialActive || tutorialData == null) return;
        
        int nextIndex = _currentStepIndex + 1;
        
        if (nextIndex >= tutorialData.GetStepCount())
        {
            // Tutorial complete
            EndTutorial();
            return;
        }
        
        GoToStep(nextIndex);
    }

    /// <summary>
    /// Moves to the previous tutorial step.
    /// </summary>
    public void PreviousStep()
    {
        if (!_isTutorialActive || tutorialData == null) return;
        
        int prevIndex = _currentStepIndex - 1;
        
        if (prevIndex < 0)
        {
            Debug.LogWarning("TutorialController: Already at first step!");
            return;
        }
        
        GoToStep(prevIndex);
    }

    /// <summary>
    /// Jumps directly to a specific step.
    /// </summary>
    public void GoToStep(int stepIndex)
    {
        if (!_isTutorialActive || tutorialData == null) return;
        
        if (stepIndex < 0 || stepIndex >= tutorialData.GetStepCount())
        {
            Debug.LogWarning($"TutorialController: Invalid step index {stepIndex}");
            return;
        }
        
        _currentStepIndex = stepIndex;
        _autoPlayTimer = 0f;
        
        TutorialStep step = tutorialData.GetStep(stepIndex);
        
        // Update visuals
        if (flowVisualizer != null)
        {
            flowVisualizer.TransitionToStep(step, step.transitionDuration);
        }
        
        // Notify listeners (UI, etc.)
        OnStepChanged?.Invoke(stepIndex, step);
    }

    /// <summary>
    /// Gets the current step index.
    /// </summary>
    public int GetCurrentStepIndex() => _currentStepIndex;

    /// <summary>
    /// Gets the current tutorial step.
    /// </summary>
    public TutorialStep GetCurrentStep()
    {
        if (_currentStepIndex >= 0 && tutorialData != null)
            return tutorialData.GetStep(_currentStepIndex);
        return null;
    }

    /// <summary>
    /// Checks if tutorial is currently active.
    /// </summary>
    public bool IsTutorialActive() => _isTutorialActive;

    /// <summary>
    /// Gets total number of steps.
    /// </summary>
    public int GetTotalSteps() => tutorialData != null ? tutorialData.GetStepCount() : 0;

    /// <summary>
    /// Checks if we can move to next step.
    /// </summary>
    public bool CanGoNext() => _isTutorialActive && _currentStepIndex < tutorialData.GetStepCount() - 1;

    /// <summary>
    /// Checks if we can move to previous step.
    /// </summary>
    public bool CanGoPrevious() => _isTutorialActive && _currentStepIndex > 0;
}
