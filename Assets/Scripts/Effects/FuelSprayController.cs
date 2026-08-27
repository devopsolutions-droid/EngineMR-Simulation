using UnityEngine;
using System.Collections;

/// <summary>
/// FuelSprayController
/// ────────────────────
/// Spawns and controls a fuel spray particle system attached to an injector location.
/// The spray runs for a configured duration, then fades out and fires onComplete.
/// </summary>
public class FuelSprayController : MonoBehaviour
{
    [Header("Particle Prefab")]
    [Tooltip("Particle System prefab for the fuel spray. Should use a fine mist-like texture.")]
    public ParticleSystem fuelSprayPrefab;

    [Header("Injector Location")]
    [Tooltip("Transform where the spray emitter should be placed (e.g., fuel injector nozzle). " +
             "Leave null to spawn at this GameObject's position.")]
    public Transform injectorLocation;

    [Header("Spray Settings")]
    [Tooltip("How long the spray should run in seconds.")]
    public float sprayDuration = 2.5f;

    [Tooltip("Emission rate during active spray (particles per second).")]
    public float sprayRate = 50f;

    [Tooltip("Max particles system-wide.")]
    public int maxParticles = 500;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private ParticleSystem _activeSpray;
    private Coroutine _sprayCoroutine;

    /// <summary>
    /// Start spraying fuel. Spawns the prefab, runs for sprayDuration, then stops.
    /// </summary>
    public void StartSpray(System.Action onComplete = null)
    {
        if (_sprayCoroutine != null) StopCoroutine(_sprayCoroutine);
        _sprayCoroutine = StartCoroutine(RunSpray(onComplete));
    }

    /// <summary>
    /// Stop the spray immediately and destroy the particle system.
    /// </summary>
    public void StopSpray()
    {
        if (_sprayCoroutine != null)
        {
            StopCoroutine(_sprayCoroutine);
            _sprayCoroutine = null;
        }

        if (_activeSpray != null)
        {
            _activeSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_activeSpray.gameObject, 2f);
            _activeSpray = null;
        }
    }

    private IEnumerator RunSpray(System.Action onComplete)
    {
        if (fuelSprayPrefab == null)
        {
            Debug.LogWarning("[FuelSprayController] No fuelSprayPrefab assigned!");
            onComplete?.Invoke();
            yield break;
        }

        // Spawn the particle system
        Vector3 spawnPos = injectorLocation != null ? injectorLocation.position : transform.position;
        Quaternion spawnRot = injectorLocation != null ? injectorLocation.rotation : transform.rotation;
        _activeSpray = Instantiate(fuelSprayPrefab, spawnPos, spawnRot);

        // Configure emission
        var emission = _activeSpray.emission;
        emission.enabled = true;
        emission.rateOverTime = sprayRate;

        var main = _activeSpray.main;
        main.maxParticles = maxParticles;
        main.loop = false;

        // Start playing
        _activeSpray.Play();

        // Wait for spray duration
        yield return new WaitForSeconds(sprayDuration);

        // Stop emitting but let particles finish naturally
        if (_activeSpray != null)
        {
            _activeSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(_activeSpray.main.startLifetime.constantMax);
            Destroy(_activeSpray.gameObject);
            _activeSpray = null;
        }

        _sprayCoroutine = null;
        onComplete?.Invoke();
    }

    void OnDestroy()
    {
        if (_sprayCoroutine != null)
        {
            StopCoroutine(_sprayCoroutine);
            _sprayCoroutine = null;
        }
        if (_activeSpray != null)
        {
            Destroy(_activeSpray.gameObject);
            _activeSpray = null;
        }
    }
}