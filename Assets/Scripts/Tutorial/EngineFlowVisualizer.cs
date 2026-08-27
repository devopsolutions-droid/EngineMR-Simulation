using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages visual effects for engine flows: airflow, combustion, and exhaust.
/// Uses particle systems and material animations to show engine processes step-by-step.
/// </summary>
public class EngineFlowVisualizer : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem airflowParticles;
    [SerializeField] private ParticleSystem combustionParticles;
    [SerializeField] private ParticleSystem exhaustParticles;
    
    [Header("Materials")]
    [SerializeField] private Material airflowMaterial;
    [SerializeField] private Material combustionMaterial;
    [SerializeField] private Material exhaustMaterial;
    
    [Header("Colors")]
    [SerializeField] private Color airflowColor = new Color(0.2f, 0.6f, 1f, 0.7f); // Blue
    [SerializeField] private Color combustionColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange
    [SerializeField] private Color exhaustColor = new Color(1f, 0.2f, 0.2f, 0.7f); // Red
    
    private Coroutine _transitionCoroutine;
    
    private float _currentAirflowIntensity = 0f;
    private float _currentCombustionIntensity = 0f;
    private float _currentExhaustIntensity = 0f;

    private void Start()
    {
        InitializeParticleSystems();
    }

    private void InitializeParticleSystems()
    {
        // Disable all particle systems initially
        if (airflowParticles != null) airflowParticles.Stop();
        if (combustionParticles != null) combustionParticles.Stop();
        if (exhaustParticles != null) exhaustParticles.Stop();
    }

    /// <summary>
    /// Smoothly transitions to a new flow state over the specified duration.
    /// </summary>
    public void TransitionToStep(TutorialStep step, float duration)
    {
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
        
        _transitionCoroutine = StartCoroutine(TransitionCoroutine(step, duration));
    }

    private IEnumerator TransitionCoroutine(TutorialStep step, float duration)
    {
        float elapsed = 0f;
        
        float startAirflow = _currentAirflowIntensity;
        float startCombustion = _currentCombustionIntensity;
        float startExhaust = _currentExhaustIntensity;
        
        float targetAirflow = step.showAirflow ? step.airflowIntensity : 0f;
        float targetCombustion = step.showCombustion ? step.combustionIntensity : 0f;
        float targetExhaust = step.showExhaust ? step.exhaustIntensity : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Smooth interpolation using ease-out curve
            t = Mathf.SmoothStep(0f, 1f, t);
            
            _currentAirflowIntensity = Mathf.Lerp(startAirflow, targetAirflow, t);
            _currentCombustionIntensity = Mathf.Lerp(startCombustion, targetCombustion, t);
            _currentExhaustIntensity = Mathf.Lerp(startExhaust, targetExhaust, t);
            
            UpdateFlowVisuals();
            
            yield return null;
        }
        
        // Ensure final values are exact
        _currentAirflowIntensity = targetAirflow;
        _currentCombustionIntensity = targetCombustion;
        _currentExhaustIntensity = targetExhaust;
        UpdateFlowVisuals();
    }

    private void UpdateFlowVisuals()
    {
        UpdateAirflow();
        UpdateCombustion();
        UpdateExhaust();
    }

    private void UpdateAirflow()
    {
        if (airflowParticles == null) return;
        
        if (_currentAirflowIntensity > 0.01f)
        {
            if (!airflowParticles.isPlaying)
                airflowParticles.Play();
            
            var emission = airflowParticles.emission;
            emission.rateOverTime = 50f * _currentAirflowIntensity;
            
            var main = airflowParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(airflowColor, Color.white, 0.3f)
            );
        }
        else
        {
            if (airflowParticles.isPlaying)
                airflowParticles.Stop();
        }
    }

    private void UpdateCombustion()
    {
        if (combustionParticles == null) return;
        
        if (_currentCombustionIntensity > 0.01f)
        {
            if (!combustionParticles.isPlaying)
                combustionParticles.Play();
            
            var emission = combustionParticles.emission;
            emission.rateOverTime = 80f * _currentCombustionIntensity;
            
            var main = combustionParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(combustionColor);
        }
        else
        {
            if (combustionParticles.isPlaying)
                combustionParticles.Stop();
        }
    }

    private void UpdateExhaust()
    {
        if (exhaustParticles == null) return;
        
        if (_currentExhaustIntensity > 0.01f)
        {
            if (!exhaustParticles.isPlaying)
                exhaustParticles.Play();
            
            var emission = exhaustParticles.emission;
            emission.rateOverTime = 60f * _currentExhaustIntensity;
            
            var main = exhaustParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(exhaustColor);
        }
        else
        {
            if (exhaustParticles.isPlaying)
                exhaustParticles.Stop();
        }
    }

    /// <summary>
    /// Immediately stops all flow effects.
    /// </summary>
    public void StopAllFlows()
    {
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
        
        _currentAirflowIntensity = 0f;
        _currentCombustionIntensity = 0f;
        _currentExhaustIntensity = 0f;
        
        if (airflowParticles != null) airflowParticles.Stop();
        if (combustionParticles != null) combustionParticles.Stop();
        if (exhaustParticles != null) exhaustParticles.Stop();
    }

    /// <summary>
    /// Get current intensity values for debugging.
    /// </summary>
    public void GetCurrentIntensities(out float airflow, out float combustion, out float exhaust)
    {
        airflow = _currentAirflowIntensity;
        combustion = _currentCombustionIntensity;
        exhaust = _currentExhaustIntensity;
    }
}
