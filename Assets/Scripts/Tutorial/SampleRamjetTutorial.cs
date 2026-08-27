using UnityEngine;

/// <summary>
/// Sample tutorial data for Ramjet engine.
/// This demonstrates how to set up tutorial steps programmatically.
/// You can also create TutorialData as a ScriptableObject in the Inspector.
/// </summary>
public class SampleRamjetTutorial : MonoBehaviour
{
    [SerializeField] private TutorialData tutorialData;

    private void Start()
    {
        if (tutorialData == null)
        {
            Debug.LogWarning("SampleRamjetTutorial: No TutorialData assigned!");
            return;
        }
        
        // Clear existing steps
        tutorialData.steps.Clear();
        
        // Step 1: Introduction
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Ramjet Engine",
            stepDescription = "Ramjet engine is an air-breathing engine that utilises the forward motion of the engine to generate thrust. This engine requires pre-compression action, due to the formation of a shock wave cone, in the diffuser.",
            showAirflow = false,
            showCombustion = false,
            showExhaust = false,
            transitionDuration = 0.5f
        });
        
        // Step 1: Intake
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 1: Intake",
            stepDescription = "Atmospheric air enters the ramjet engine through the supersonic diffuser. The temperature and pressure of the atmospheric air increase due to compression action, due to the formation of a shock wave cone, in the diffuser.",
            showAirflow = true,
            airflowIntensity = 1f,
            showCombustion = false,
            showExhaust = false,
            transitionDuration = 1f
        });
        
        // Step 2: Compression
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 2: Compression",
            stepDescription = "The pressure and temperature of the atmospheric air are increased further in the subsonic diffuser, due to the ramming action.",
            showAirflow = true,
            airflowIntensity = 0.8f,
            showCombustion = false,
            showExhaust = false,
            transitionDuration = 1f
        });
        
        // Step 3: Fuel Injection
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 3: Fuel Injection",
            stepDescription = "Now, the high-pressure, high-temperature air moves into the combustion chamber. Fuel is added to the combustion chamber and a combustible mixture is prepared. Hydrogen is the commonly used fuel in ramjet engines.",
            showAirflow = true,
            airflowIntensity = 0.7f,
            showCombustion = false,
            showExhaust = false,
            transitionDuration = 1f
        });
        
        // Step 4: Ignition
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 4: Ignition",
            stepDescription = "The mixture is ignited and the combustion starts. The pressure and temperature of the mixture increase rapidly due to the combustion process.",
            showAirflow = true,
            airflowIntensity = 0.6f,
            showCombustion = true,
            combustionIntensity = 0.5f,
            showExhaust = false,
            transitionDuration = 1f
        });
        
        // Step 5: Expansion
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 5: Expansion",
            stepDescription = "This high-pressure, high-temperature gas is now allowed to expand in a converging-diverging nozzle. Due to expansion in the nozzle, a high-velocity jet is produced in the nozzle which is allowed to be exited through the rear of the nozzle.",
            showAirflow = true,
            airflowIntensity = 0.5f,
            showCombustion = true,
            combustionIntensity = 0.8f,
            showExhaust = false,
            transitionDuration = 1f
        });
        
        // Step 6: Exhaust
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Step 6: Exhaust",
            stepDescription = "This high-velocity exhaust from the rear of the nozzle, by the 3rd law of motion, provides a forward thrust to the engine body and propels the engine forward.",
            showAirflow = true,
            airflowIntensity = 0.4f,
            showCombustion = true,
            combustionIntensity = 0.6f,
            showExhaust = true,
            exhaustIntensity = 1f,
            transitionDuration = 1f
        });
        
        // Conclusion
        tutorialData.steps.Add(new TutorialStep
        {
            stepTitle = "Conclusion",
            stepDescription = "• Initially, high-speed air enters the converging-diverging inlet of the ramjet engine.\n• The diffuser removes supersonic in speed, and the flow shock comes are removed.\n• Fuel is injected into the combustion chamber and is mixed with air to produce high pressure and high temperature.\n• The high-speed exhaust produced exits from the rear of the nozzle.\n• The reaction force from the exhaust propels the engine in the forward direction.",
            showAirflow = true,
            airflowIntensity = 0.3f,
            showCombustion = true,
            combustionIntensity = 0.5f,
            showExhaust = true,
            exhaustIntensity = 0.8f,
            transitionDuration = 1f
        });
    }
}
