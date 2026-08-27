using UnityEngine;
using System.Collections;

/// <summary>
/// SlowMotionController
/// ─────────────────────
/// Modulates Time.timeScale for a cinematic slow-motion effect.
/// Fades in the time scale, holds for a duration, then fades back to normal.
/// Fires onComplete when the full sequence finishes.
/// </summary>
public class SlowMotionController : MonoBehaviour
{
    [Header("Slow Motion Settings")]
    [Tooltip("Target Time.timeScale during slow-motion (e.g., 0.2 = 20% speed).")]
    [Range(0.05f, 0.5f)]
    public float timeScale = 0.2f;

    [Tooltip("Total duration of the slow-motion effect in seconds (real-time).")]
    public float duration = 3.0f;

    [Header("Fade")]
    [Tooltip("How long (real-time seconds) it takes to fade INTO slow motion.")]
    public float fadeIn = 0.3f;

    [Tooltip("How long (real-time seconds) it takes to fade BACK to normal speed.")]
    public float fadeOut = 0.5f;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private Coroutine _slowMoCoroutine;
    private float _originalTimeScale;
    private float _originalFixedDeltaTime;

    void Awake()
    {
        _originalTimeScale = Time.timeScale;
        _originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    /// <summary>
    /// Trigger the slow-motion effect. Fades in, holds, fades out.
    /// </summary>
    public void TriggerSlowMotion(System.Action onComplete = null)
    {
        if (_slowMoCoroutine != null) StopCoroutine(_slowMoCoroutine);
        _slowMoCoroutine = StartCoroutine(RunSlowMotion(onComplete));
    }

    /// <summary>
    /// Immediately resume normal time. Cancels any running slow-motion.
    /// </summary>
    public void ResumeNormalTime()
    {
        if (_slowMoCoroutine != null)
        {
            StopCoroutine(_slowMoCoroutine);
            _slowMoCoroutine = null;
        }

        Time.timeScale = _originalTimeScale;
        Time.fixedDeltaTime = _originalFixedDeltaTime;
    }

    private IEnumerator RunSlowMotion(System.Action onComplete)
    {
        // ── Fade in ─────────────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime; // use unscaled so fade itself isn't slowed
            float t = Mathf.Clamp01(elapsed / fadeIn);
            Time.timeScale = Mathf.Lerp(_originalTimeScale, timeScale, t);
            Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;
            yield return null;
        }

        Time.timeScale = timeScale;
        Time.fixedDeltaTime = _originalFixedDeltaTime * timeScale;

        // ── Hold ────────────────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(duration);

        // ── Fade out ────────────────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOut);
            Time.timeScale = Mathf.Lerp(timeScale, _originalTimeScale, t);
            Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;
            yield return null;
        }

        // Restore
        Time.timeScale = _originalTimeScale;
        Time.fixedDeltaTime = _originalFixedDeltaTime;

        _slowMoCoroutine = null;
        onComplete?.Invoke();
    }

    void OnDestroy()
    {
        // Safety: restore time if destroyed during slow-motion
        if (Mathf.Abs(Time.timeScale - _originalTimeScale) > 0.01f)
        {
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }
    }
}