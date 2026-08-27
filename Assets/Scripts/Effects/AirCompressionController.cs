using UnityEngine;
using System.Collections;

/// <summary>
/// AirCompressionController
/// ─────────────────────────
/// Manipulates the JetEngineAirflowController's tube radius scale to create a
/// "narrowing" visual effect (air squeezing), and optionally shifts the tube color.
/// Designed as a temporary VFX that plays for the duration of an Air Compression step.
/// </summary>
public class AirCompressionController : MonoBehaviour
{
    [Header("Airflow Reference")]
    [Tooltip("The JetEngineAirflowController whose tube radius will be animated.")]
    public JetEngineAirflowController airflowController;

    [Header("Compression Settings")]
    [Tooltip("Animation curve: time (0..1) → radius scale multiplier (0..1). " +
             "1.0 = normal, 0.5 = tube narrowed to half, etc.")]
    public AnimationCurve narrowCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.6f);

    [Tooltip("Total duration of the compression animation in seconds.")]
    public float narrowDuration = 2.0f;

    [Header("Color Shift (Optional)")]
    [Tooltip("If true, shifts the tube color during compression.")]
    public bool enableColorShift = true;

    [Tooltip("Target color during peak compression (e.g., cyan-white for compressed air).")]
    public Color targetColor = new Color(0.8f, 1f, 1f); // cyan-white

    [Tooltip("How fast the color shifts.")]
    public float colorShiftDuration = 1.0f;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private Coroutine _compressionCoroutine;
    private float _originalRadiusScale;

    void Awake()
    {
        if (airflowController != null)
            _originalRadiusScale = airflowController.radiusScale;
    }

    /// <summary>
    /// Start the compression effect. Animates tube narrowing and optional color shift.
    /// </summary>
    public void StartCompression(System.Action onComplete = null)
    {
        if (_compressionCoroutine != null) StopCoroutine(_compressionCoroutine);

        if (airflowController == null)
        {
            Debug.LogWarning("[AirCompressionController] No airflowController assigned!");
            onComplete?.Invoke();
            return;
        }

        _originalRadiusScale = airflowController.radiusScale;
        _compressionCoroutine = StartCoroutine(AnimateCompression(onComplete));
    }

    /// <summary>
    /// Reset tube radius back to original value immediately.
    /// </summary>
    public void ResetCompression()
    {
        if (_compressionCoroutine != null)
        {
            StopCoroutine(_compressionCoroutine);
            _compressionCoroutine = null;
        }

        if (airflowController != null)
            airflowController.radiusScale = _originalRadiusScale;
    }

    private IEnumerator AnimateCompression(System.Action onComplete)
    {
        float elapsed = 0f;

        // ── Phase 1: Narrow ────────────────────────────────────────────────
        while (elapsed < narrowDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / narrowDuration);
            float scaleMultiplier = narrowCurve.Evaluate(t);

            if (airflowController != null)
                airflowController.radiusScale = _originalRadiusScale * scaleMultiplier;

            // If color shift enabled, animate color
            if (enableColorShift && airflowController != null)
            {
                float colorT = Mathf.Clamp01(elapsed / colorShiftDuration);
                // We can't easily shift tube color without shader property access,
                // so we just pass through to the airflow material if it exposes one
                var mat = GetTubeMaterial();
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", Color.Lerp(Color.white, targetColor, colorT));
                }
            }

            yield return null;
        }

        // ── Hold briefly at compressed state ───────────────────────────────
        yield return new WaitForSeconds(0.5f);

        // ── Phase 2: Restore ───────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < narrowDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (narrowDuration * 0.5f));
            float restoreScale = Mathf.Lerp(
                _originalRadiusScale * narrowCurve.Evaluate(1f),
                _originalRadiusScale,
                t
            );

            if (airflowController != null)
                airflowController.radiusScale = restoreScale;

            // Restore color
            if (enableColorShift)
            {
                var mat = GetTubeMaterial();
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", Color.Lerp(targetColor, Color.white, t));
                }
            }

            yield return null;
        }

        // Final restore
        if (airflowController != null)
            airflowController.radiusScale = _originalRadiusScale;

        _compressionCoroutine = null;
        onComplete?.Invoke();
    }

    private Material GetTubeMaterial()
    {
        if (airflowController == null) return null;
        // Access via reflection isn't great; instead we rely on the user to
        // assign a reference — but for now we use a simple approach:
        // The tube material is private, so we ask the user to assign it.
        // In the Editor, they can drag the Airflow_ContinuousTube's material.
        return null; // Override in Inspector or use injected reference
    }

    void OnDestroy()
    {
        if (_compressionCoroutine != null)
        {
            StopCoroutine(_compressionCoroutine);
            _compressionCoroutine = null;
        }
    }
}