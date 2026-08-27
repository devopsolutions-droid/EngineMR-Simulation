using UnityEngine;

/// <summary>
/// SIMPLE driver for Airflow_v6.1 blue animation.
/// Uses the EXISTING Custom/AirflowEffect shader with all-blue colours — no new shader needed.
/// Polls JetEngineShowWorking.CurrentAirflowProgress directly and drives _Progress per-piece.
/// 
/// Design: "copy-paste" simplicity — uses exactly the same shader as the legacy tube,
/// just with all gradient colours set to blue. No bridge script required.
/// </summary>
public class AirflowV61Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JetEngineShowWorking showWorkingRef;

    [Header("Animation")]
    [SerializeField, Range(0.5f, 10f)] private float smoothingSpeed = 3f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private MeshRenderer[] meshRenderers;
    private MaterialPropertyBlock mpb;
    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private bool isFlowing = false;

    private static readonly int ProgressID = Shader.PropertyToID("_Progress");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        mpb = new MaterialPropertyBlock();

        // Auto-discover JetEngineShowWorking if not wired in Inspector
        if (showWorkingRef == null)
            showWorkingRef = FindObjectOfType<JetEngineShowWorking>();

        if (showWorkingRef == null)
        {
            Debug.LogError("[AirflowV61Controller] No JetEngineShowWorking found in scene. " +
                "Assign in Inspector or ensure one exists.", this);
            enabled = false;
            return;
        }

        // Check if show working is already running (progress > 0)
        float initialProgress = showWorkingRef.CurrentAirflowProgress;
        if (initialProgress > 0.01f)
        {
            isFlowing = true;
            currentProgress = initialProgress;
            targetProgress = initialProgress;
            ApplyProgress(currentProgress);
        }
    }

    void Update()
    {
        if (showWorkingRef == null) return;

        targetProgress = showWorkingRef.CurrentAirflowProgress;

        // ── Auto-detect flow state from progress ──────────────────────────
        if (targetProgress > 0.01f && !isFlowing)
        {
            isFlowing = true;
            currentProgress = 0f;
        }
        else if (targetProgress < 0.001f && isFlowing)
        {
            // Flow ended — snap to zero
            isFlowing = false;
            currentProgress = 0f;
            ApplyProgress(0f);
            return;
        }

        if (!isFlowing) return;

        // Smooth interpolation for visual continuity
        currentProgress = Mathf.Lerp(currentProgress, targetProgress, Time.deltaTime * smoothingSpeed);

        // Snap to exact value when close (prevents float drift)
        if (Mathf.Abs(currentProgress - targetProgress) < 0.001f)
            currentProgress = targetProgress;

        ApplyProgress(currentProgress);
    }

    // ── Progress application ──────────────────────────────────────────────────

    /// <summary>
    /// Apply _Progress to all child mesh renderers via MaterialPropertyBlock.
    /// Each piece uses the EXISTING Custom/AirflowEffect shader's UV.y mapping.
    /// </summary>
    private void ApplyProgress(float progress)
    {
        if (meshRenderers == null || mpb == null) return;

        mpb.SetFloat(ProgressID, progress);

        foreach (var renderer in meshRenderers)
        {
            if (renderer != null)
                renderer.SetPropertyBlock(mpb);
        }
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Get the current smoothed progress value (0–1).
    /// </summary>
    public float CurrentProgress => currentProgress;

    /// <summary>
    /// Manually override the progress value (bypasses showWorkingRef polling).
    /// </summary>
    public void SetProgress(float value)
    {
        targetProgress = Mathf.Clamp01(value);
        currentProgress = targetProgress;
        ApplyProgress(currentProgress);
    }
}