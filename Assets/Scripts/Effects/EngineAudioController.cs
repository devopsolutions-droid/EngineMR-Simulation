using UnityEngine;
using System.Collections;

/// <summary>
/// EngineAudioController
/// ──────────────────────
/// Provides audio crossfade functionality between two engine audio loops.
/// Used by CombustionController and other VFX systems to transition between
/// normal engine hum and intensified post-ignition roar.
/// Also supports a one-shot ignition "boom" sound at the moment of combustion.
/// </summary>
public class EngineAudioController : MonoBehaviour
{
    [Header("Audio Source")]
    [Tooltip("The primary AudioSource playing engine sounds.")]
    public AudioSource engineAudioSource;

    [Header("Engine Loops")]
    [Tooltip("Normal (idle/running) engine loop.")]
    public AudioClip normalEngineLoop;

    [Tooltip("Intensified (post-ignition) engine loop.")]
    public AudioClip intensifiedEngineLoop;

    [Header("Ignition SFX")]
    [Tooltip("One-shot sound played at the moment of ignition (e.g., a 'boom' or 'whoosh').")]
    public AudioClip ignitionBoom;

    [Header("Crossfade")]
    [Tooltip("Duration of the audio crossfade in seconds.")]
    public float crossfadeDuration = 1.5f;

    [Tooltip("Target volume for the engine loop (0..1).")]
    [Range(0f, 1f)]
    public float engineVolume = 0.8f;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private Coroutine _crossfadeCoroutine;
    private bool _isIntensified = false;

    void Start()
    {
        if (engineAudioSource != null && normalEngineLoop != null)
        {
            engineAudioSource.clip = normalEngineLoop;
            engineAudioSource.volume = engineVolume;
            engineAudioSource.loop = true;
            engineAudioSource.Play();
        }
    }

    /// <summary>
    /// Play the ignition boom sound (one-shot) without stopping the engine loop.
    /// </summary>
    public void PlayIgnitionBoom()
    {
        if (engineAudioSource != null && ignitionBoom != null)
        {
            engineAudioSource.PlayOneShot(ignitionBoom, 1.0f);
        }
    }

    /// <summary>
    /// Crossfade from current engine loop to the intensified loop.
    /// </summary>
    public void CrossfadeToIntensified(System.Action onComplete = null)
    {
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        if (engineAudioSource == null || intensifiedEngineLoop == null)
        {
            onComplete?.Invoke();
            return;
        }

        _isIntensified = true;
        _crossfadeCoroutine = StartCoroutine(Crossfade(intensifiedEngineLoop, onComplete));
    }

    /// <summary>
    /// Crossfade back from intensified loop to the normal engine loop.
    /// </summary>
    public void CrossfadeToNormal(System.Action onComplete = null)
    {
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        if (engineAudioSource == null || normalEngineLoop == null)
        {
            onComplete?.Invoke();
            return;
        }

        _isIntensified = false;
        _crossfadeCoroutine = StartCoroutine(Crossfade(normalEngineLoop, onComplete));
    }

    /// <summary>
    /// True if the audio is currently playing the intensified loop.
    /// </summary>
    public bool IsIntensified => _isIntensified;

    private IEnumerator Crossfade(AudioClip targetClip, System.Action onComplete)
    {
        float halfDuration = crossfadeDuration * 0.5f;

        // Fade out current
        float elapsed = 0f;
        float startVolume = engineAudioSource.volume;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            engineAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        // Switch clip
        engineAudioSource.volume = 0f;
        engineAudioSource.clip = targetClip;
        engineAudioSource.Play();

        // Fade in new clip
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            engineAudioSource.volume = Mathf.Lerp(0f, engineVolume, elapsed / halfDuration);
            yield return null;
        }

        engineAudioSource.volume = engineVolume;
        _crossfadeCoroutine = null;
        onComplete?.Invoke();
    }

    void OnDestroy()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }
    }
}