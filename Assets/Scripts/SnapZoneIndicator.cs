using UnityEngine;
using System.Collections;

/// <summary>
/// Snap Zone Indicator — visual guide showing the user where to place a part.
///
/// Renders a coloured ring/glow at the part's snap target position.
/// The indicator has three visual states:
///   1. IDLE      — part not held: subtle pulsing ring, guides the user to the target area
///   2. APPROACH  — part held and within indicator range: bright, animated ring getting
///                  more intense as the part gets closer
///   3. SNAPPED   — part snapped: flash green and fade out over 0.5s
///
/// The indicator is rendered using a simple LineRenderer circle projected in world space
/// at the snap target position. It only shows during Grab Mode.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SnapZoneIndicator : MonoBehaviour
{
    [Header("Indicator Settings")]
    [Tooltip("Distance from the snap target at which the approach glow starts.")]
    [Range(0.1f, 1.5f)]
    public float indicatorRange = 0.5f;

    [Tooltip("Radius of the indicator circle in world units.")]
    [Range(0.01f, 0.5f)]
    public float circleRadius = 0.08f;

    [Range(8, 64)]
    public int circleSegments = 24;

    [Header("Colors")]
    public Color idleColor     = new Color(0.2f, 1.0f, 0.2f, 0.25f);  // subtle green
    public Color approachColor = new Color(0.0f, 1.0f, 0.3f, 0.8f);   // bright green
    public Color snappedColor  = new Color(0.0f, 1.0f, 0.0f, 1.0f);   // solid green flash

    [Header("Animation")]
    [Range(0.1f, 4f)]
    public float pulseSpeed = 1.8f;
    [Range(0f, 1f)]
    public float pulseAmount = 0.3f;
    [Tooltip("How long the snapped flash lasts before fading out completely.")]
    [Range(0.1f, 1.5f)]
    public float snappedFadeDuration = 0.5f;

    [Header("Line Style")]
    [Range(0.001f, 0.02f)]
    public float lineWidth = 0.005f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private LineRenderer _line;
    private EnginePartSnapController _snapController;
    private Vector3 _targetPosition;

    // Visual state tracking
    private enum IndicatorState { Idle, Approach, Snapped, Hidden }
    private IndicatorState _state = IndicatorState.Hidden;
    private float _stateTime;
    private Coroutine _snappedFadeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = circleSegments + 1;
        _line.loop = true;
        _line.useWorldSpace = true;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth;
        _line.material = new Material(Shader.Find("Sprites/Default"));
        _line.enabled = false; // hidden by default

        _snapController = GetComponentInParent<EnginePartSnapController>();
        if (_snapController == null)
            _snapController = GetComponent<EnginePartSnapController>();
    }

    void Start()
    {
        // Build the circle geometry once (it moves as a whole)
        BuildCircle();
    }

    void OnDestroy()
    {
        if (_snappedFadeCoroutine != null)
            StopCoroutine(_snappedFadeCoroutine);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show the indicator at the given world-space target position.</summary>
    public void Show(Vector3 targetPos)
    {
        _targetPosition = targetPos;
        _line.enabled = true;
        _state = IndicatorState.Idle;
        _stateTime = 0f;
    }

    /// <summary>Hide the indicator entirely.</summary>
    public void Hide()
    {
        _line.enabled = false;
        _state = IndicatorState.Hidden;
        if (_snappedFadeCoroutine != null)
        {
            StopCoroutine(_snappedFadeCoroutine);
            _snappedFadeCoroutine = null;
        }
    }

    /// <summary>
    /// Update indicator state based on current distance from the held part to the snap target.
    /// Should be called every frame while the indicator is active.
    /// </summary>
    /// <param name="heldPartPosition">Current world position of the part being held.</param>
    public void UpdateApproach(Vector3 heldPartPosition)
    {
        if (_state == IndicatorState.Snapped || _state == IndicatorState.Hidden)
            return;

        float dist = Vector3.Distance(heldPartPosition, _targetPosition);

        if (dist <= _snapController.snapDistance)
        {
            // Part is within snap range — will snap imminently, show snapped state
            SetSnappedState();
        }
        else if (dist <= indicatorRange)
        {
            // Part is approaching — brighten ring proportional to closeness
            _state = IndicatorState.Approach;
            _stateTime += Time.deltaTime;
            float intensity = 1f - Mathf.Clamp01((dist - _snapController.snapDistance) / (indicatorRange - _snapController.snapDistance));
            UpdateAppearance(intensity);
        }
        else
        {
            // Part is far — idle pulse
            _state = IndicatorState.Idle;
            _stateTime += Time.deltaTime;
            UpdateAppearance(0f);
        }
    }

    /// <summary>
    /// Called by EnginePartSnapController when snap actually happens, for a flash-and-fade effect.
    /// </summary>
    public void OnSnapped()
    {
        if (_snappedFadeCoroutine != null)
            StopCoroutine(_snappedFadeCoroutine);
        _snappedFadeCoroutine = StartCoroutine(SnappedFadeOut());
    }

    // ── Visual Updates ────────────────────────────────────────────────────────

    private void SetSnappedState()
    {
        _state = IndicatorState.Snapped;
        _stateTime = 0f;

        Color c = snappedColor;
        c.a = 1f;
        _line.startColor = c;
        _line.endColor = c;
    }

    private void UpdateAppearance(float approachIntensity)
    {
        // Interpolate color between idle and approach based on intensity
        Color targetColor = Color.Lerp(idleColor, approachColor, approachIntensity);

        // Pulse alpha
        float pulse = 1f + Mathf.Sin(_stateTime * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        float alpha = targetColor.a * Mathf.Lerp(0.6f, 1f, pulse);
        Color finalColor = new Color(targetColor.r, targetColor.g, targetColor.b, alpha);

        // Pulse radius slightly on approach
        float radiusScale = 1f + approachIntensity * 1.5f;

        _line.startColor = finalColor;
        _line.endColor = finalColor;
        _line.startWidth = lineWidth * radiusScale;
        _line.endWidth = lineWidth * radiusScale;
    }

    private IEnumerator SnappedFadeOut()
    {
        float elapsed = 0f;
        Color startColor = snappedColor;
        float startWidth = lineWidth * 2.5f; // flare up

        while (elapsed < snappedFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snappedFadeDuration;

            // Fade alpha to 0, shrink width
            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            _line.startColor = c;
            _line.endColor = c;

            float w = Mathf.Lerp(startWidth, 0f, t);
            _line.startWidth = w;
            _line.endWidth = w;

            yield return null;
        }

        _line.enabled = false;
        _state = IndicatorState.Hidden;
        _snappedFadeCoroutine = null;
    }

    // ── Circle Geometry ───────────────────────────────────────────────────────

    private void BuildCircle()
    {
        Vector3[] positions = new Vector3[circleSegments + 1];
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            positions[i] = new Vector3(
                Mathf.Cos(angle) * circleRadius,
                Mathf.Sin(angle) * circleRadius,
                0f
            );
        }
        _line.SetPositions(positions);
    }

    // ── Position Sync ─────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_line.enabled && _state != IndicatorState.Hidden)
        {
            // Position the indicator at the snap target
            transform.position = _targetPosition;

            // Orient the indicator to face the camera (or stay flat on XY plane)
            // Keep it world-aligned so it's visible from any angle
        }
    }
}