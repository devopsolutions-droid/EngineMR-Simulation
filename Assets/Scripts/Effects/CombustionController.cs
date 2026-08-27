using UnityEngine;
using System.Collections;

/// <summary>
/// CombustionController
/// ─────────────────────
/// Orchestrates the combustion/ignition visual effects:
///   1. Chamber glow (PartGlowController) — brief intense flash
///   2. Flame particle system spawned at the combustion zone
///   3. Audio crossfade from normal engine loop to intensified loop
///   4. Camera shake for impact
/// Runs all effects in parallel, then fires onComplete.
/// </summary>
public class CombustionController : MonoBehaviour
{
    [Header("Chamber Glow")]
    [Tooltip("Glow controller for the combustion chamber area. Activated for a burst.")]
    public PartGlowController chamberGlow;

    [Header("Flame Particles")]
    [Tooltip("Particle system prefab for the ignition flame burst.")]
    public ParticleSystem flamePrefab;

    [Tooltip("Transform where the flame should appear (combustion zone).")]
    public Transform flameOrigin;

    [Header("Audio")]
    [Tooltip("Audio source currently playing the normal engine loop.")]
    public AudioSource engineAudioSource;

    [Tooltip("The intensified engine loop clip to crossfade to.")]
    public AudioClip intensifiedEngineLoop;

    [Tooltip("Duration of the audio crossfade.")]
    public float crossfadeDuration = 1.5f;

    [Header("Camera Shake")]
    [Tooltip("How long the camera shake lasts.")]
    public float shakeDuration = 0.6f;

    [Tooltip("Magnitude of the camera shake.")]
    public float shakeMagnitude = 0.15f;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private Coroutine _combustionCoroutine;

    /// <summary>
    /// Start the combustion effect sequence. All effects run in parallel.
    /// </summary>
    public void StartCombustion(System.Action onComplete = null)
    {
        if (_combustionCoroutine != null) StopCoroutine(_combustionCoroutine);
        _combustionCoroutine = StartCoroutine(RunCombustion(onComplete));
    }

    /// <summary>
    /// Stop all combustion effects immediately.
    /// </summary>
    public void StopCombustion()
    {
        if (_combustionCoroutine != null)
        {
            StopCoroutine(_combustionCoroutine);
            _combustionCoroutine = null;
        }
    }

    private IEnumerator RunCombustion(System.Action onComplete)
    {
        // ── 1. Chamber glow burst ───────────────────────────────────────────
        if (chamberGlow != null)
        {
            chamberGlow.glowColor = new Color(1f, 0.6f, 0.1f); // orange-yellow
            chamberGlow.maxGlowIntensity = 3f;
            chamberGlow.glowDuration = 0.8f;
            chamberGlow.ActivateGlow(null);
        }

        // ── 2. Spawn flame particles ────────────────────────────────────────
        ParticleSystem flameInstance = null;
        if (flamePrefab != null)
        {
            Vector3 pos = flameOrigin != null ? flameOrigin.position : transform.position;
            Quaternion rot = flameOrigin != null ? flameOrigin.rotation : Quaternion.identity;
            flameInstance = Instantiate(flamePrefab, pos, rot);
            flameInstance.Play();
        }

        // ── 3. Audio crossfade to intensified loop ─────────────────────────
        Coroutine audioCoroutine = null;
        if (engineAudioSource != null && intensifiedEngineLoop != null)
        {
            audioCoroutine = StartCoroutine(CrossfadeAudio());
        }

        // ── 4. Camera shake ─────────────────────────────────────────────────
        Coroutine shakeCoroutine = StartCoroutine(CameraShake());

        // Wait for shake (shortest effect) to settle, then wait for audio
        yield return shakeCoroutine;

        if (audioCoroutine != null)
            yield return audioCoroutine;

        // Clean up flame particles after they finish
        if (flameInstance != null)
        {
            yield return new WaitForSeconds(flameInstance.main.startLifetime.constantMax);
            Destroy(flameInstance.gameObject);
        }

        _combustionCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator CrossfadeAudio()
    {
        if (engineAudioSource == null || intensifiedEngineLoop == null) yield break;

        // Store original clip
        AudioClip originalClip = engineAudioSource.clip;
        float originalVolume = engineAudioSource.volume;

        // Crossfade: reduce current volume, switch clip, raise volume
        float elapsed = 0f;
        while (elapsed < crossfadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            engineAudioSource.volume = Mathf.Lerp(originalVolume, 0f, elapsed / (crossfadeDuration * 0.5f));
            yield return null;
        }

        engineAudioSource.volume = 0f;
        engineAudioSource.clip = intensifiedEngineLoop;
        engineAudioSource.Play();

        elapsed = 0f;
        while (elapsed < crossfadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            engineAudioSource.volume = Mathf.Lerp(0f, originalVolume, elapsed / (crossfadeDuration * 0.5f));
            yield return null;
        }

        engineAudioSource.volume = originalVolume;
    }

    private IEnumerator CameraShake()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float intensity = 1f - (elapsed / shakeDuration); // fade out
            float offsetX = Random.Range(-1f, 1f) * shakeMagnitude * intensity;
            float offsetY = Random.Range(-1f, 1f) * shakeMagnitude * intensity;
            cam.transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        cam.transform.localPosition = originalPos;
    }

    void OnDestroy()
    {
        if (_combustionCoroutine != null)
        {
            StopCoroutine(_combustionCoroutine);
            _combustionCoroutine = null;
        }
    }
}