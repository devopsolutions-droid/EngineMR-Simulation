using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a single step in the engine tutorial.
/// Each step shows specific visual effects and educational content.
/// </summary>
[System.Serializable]
public class TutorialStep
{
    [SerializeField] public string stepTitle;
    [SerializeField] public string stepDescription;
    
    [SerializeField] public bool showAirflow = false;
    [SerializeField] public float airflowIntensity = 0f;
    
    [SerializeField] public bool showCombustion = false;
    [SerializeField] public float combustionIntensity = 0f;
    
    [SerializeField] public bool showExhaust = false;
    [SerializeField] public float exhaustIntensity = 0f;
    
    [SerializeField] public List<string> highlightedParts = new List<string>();
    
    [SerializeField] public float transitionDuration = 1f;
}

/// <summary>
/// ScriptableObject containing all tutorial steps for an engine.
/// Create one per engine type and assign in the scene.
/// </summary>
[CreateAssetMenu(fileName = "New Engine Tutorial", menuName = "Engine VR/Tutorial/Engine Tutorial Data")]
public class TutorialData : ScriptableObject
{
    [SerializeField] public List<TutorialStep> steps = new List<TutorialStep>();
    
    [SerializeField] public bool autoPlaySteps = false;
    [SerializeField] public float stepDuration = 5f;
    
    public int GetStepCount() => steps.Count;
    
    public TutorialStep GetStep(int index)
    {
        if (index >= 0 && index < steps.Count)
            return steps[index];
        return null;
    }
}
