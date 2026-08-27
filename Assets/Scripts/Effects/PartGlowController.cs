using UnityEngine;
using System.Collections;

/// <summary>
/// PartGlowController
/// ──────────────────
/// Applies a MaterialPropertyBlock-based emission glow to a Renderer.
/// Glow fades in/out following an AnimationCurve, then fires an onComplete callback.
/// Non-allocating — uses MaterialPropertyBlock so it doesn't create material instances.
/// </summary>
public class PartGlowController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Renderer to apply glow to. Leave null to use this GameObject's Renderer.")]
    public Renderer targetRenderer;

    [Tooltip("Material index on the renderer to modify (usually 0).")]
    public int materialIndex = 0;

    [Header("Glow Settings")]
    [Tooltip("Color of the emission glow.")]
    public Color glowColor = Color.yellow;

    [Tooltip("Peak emission intensity.")]
    public float maxGlowIntensity = 2.0f;

    [Tooltip("Animation curve: time (0..1) → intensity (0..1). Typical: sharp rise, slow fall.")]
    public AnimationCurve glowCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("Total duration of the glow animation in seconds.")]
    public float glowDuration = 1.5f;

    // ── Runtime ─────────────────────────────────────────────────────────────────
    private MaterialPropertyBlock _mpb;
    private Renderer _renderer;
    private int _emissionColorId;
    private Coroutine _glowCoroutine;

    void Awake()
    {
        if (targetRenderer != null)
            _renderer = targetRenderer;
        else
            _renderer = GetComponent<Renderer>();

        if (_renderer == null)
        {
            Debug.LogError("[PartGlowController] No Renderer found on " + gameObject.name);
            enabled = false;
            return;
        }

        _mpb = new MaterialPropertyBlock();
        _emissionColorId = Shader.PropertyToID("_EmissionColor");

        // Ensure emission is enabled on the material (URP/HDRP: _EMISSION keyword)
        var mat = _renderer.sharedMaterials[materialIndex];
        if (mat != null && !mat.IsKeywordEnabled("_EMISSION"))
        {
            mat.EnableKeyword("_EMISSION");
        }
    }

    /// <summary>
    /// Start the glow animation. Calls onComplete when done.
    /// </summary>
    public void ActivateGlow(System.Action onComplete = null)
    {
        if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);
        _glowCoroutine = StartCoroutine(AnimateGlow(onComplete));
    }

    /// <summary>
    /// Stop the glow immediately and reset to black (no emission).
    /// </summary>
    public void StopGlow()
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        if (_renderer != null && _mpb != null)
        {
            _renderer.GetPropertyBlock(_mpb, materialIndex);
            _mpb.SetColor(_emissionColorId, Color.black);
            _renderer.SetPropertyBlock(_mpb, materialIndex);
        }
    }

    private IEnumerator AnimateGlow(System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < glowDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / glowDuration);
            float intensity = glowCurve.Evaluate(t) * maxGlowIntensity;

            Color emission = glowColor * intensity;

            _renderer.GetPropertyBlock(_mpb, materialIndex);
            _mpb.SetColor(_emissionColorId, emission);
            _renderer.SetPropertyBlock(_mpb, materialIndex);

            yield return null;
        }

        // End: reset to black
        _renderer.GetPropertyBlock(_mpb, materialIndex);
        _mpb.SetColor(_emissionColorId, Color.black);
        _renderer.SetPropertyBlock(_mpb, materialIndex);

        _glowCoroutine = null;
        onComplete?.Invoke();
    }

    void OnDestroy()
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }
    }
}