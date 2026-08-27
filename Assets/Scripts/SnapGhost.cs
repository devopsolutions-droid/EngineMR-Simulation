using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Snap Ghost — visual ghost outline of an engine part at its snap target position.
///
/// When the user grabs a part, this component spawns semi-transparent mesh copies
/// of the part at the world-space snap target, giving the user a clear visual
/// hint: "place the part HERE".
///
/// The ghost uses a custom ghost material (with transparency + emission) that
/// pulses gently. On snap, the ghost flashes brightly and fades out.
///
/// Usage:
///   Place this as a child of the EnginePart that has EnginePartSnapController.
///   Call CreateGhost() when the part is grabbed, then Show().
///   Call UpdateTarget() each frame to keep the ghost synced.
///   Call ClearGhost() when released.
/// </summary>
public class SnapGhost : MonoBehaviour
{
    [Header("Ghost Material")]
    [Tooltip("Assign a transparent material for the ghost. Uses Sprites/Default as fallback.")]
    public Material ghostMaterial;

    [Tooltip("Base color of the ghost (ignored if ghostMaterial is assigned externally).")]
    public Color ghostColor = new Color(0.15f, 0.9f, 0.3f, 0.28f);

    [Header("Animation")]
    [Tooltip("Speed of the idle pulse.")]
    [Range(0.5f, 4f)]
    public float pulseSpeed = 1.5f;

    [Tooltip("How much the alpha oscillates during pulse (0=static).")]
    [Range(0f, 0.5f)]
    public float pulseAmount = 0.2f;

    [Tooltip("How long the snap flash-and-fade lasts.")]
    [Range(0.2f, 1.5f)]
    public float flashDuration = 0.6f;

    // ── Runtime ────────────────────────────────────────────────────────────────
    private GameObject _ghostRoot;
    private List<Renderer> _ghostRenderers = new();
    private Material _runtimeMat;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;

    private enum GhostState { Hidden, IdlePulse, SnapFlash }
    private GhostState _state = GhostState.Hidden;
    private float _stateTime;
    private float _baseAlpha;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (ghostMaterial == null)
        {
            // Create a default ghost material using Sprites/Default shader
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            ghostMaterial = new Material(shader);
        }

        // Create an instance for runtime tinting
        _runtimeMat = new Material(ghostMaterial);
        _runtimeMat.name = $"{gameObject.name}_SnapGhostMat";
    }

    void OnDestroy()
    {
        ClearGhost();
        if (_runtimeMat != null)
            Destroy(_runtimeMat);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the ghost mesh copies from the source part.
    /// Call once when the part is grabbed.
    /// </summary>
    public void CreateGhost(Transform sourcePart, Vector3 targetPosition, Quaternion targetRotation)
    {
        ClearGhost();

        _targetPosition = targetPosition;
        _targetRotation = targetRotation;

        // Create root for all ghost pieces
        _ghostRoot = new GameObject($"{sourcePart.name}_SnapGhost");
        _ghostRoot.transform.SetPositionAndRotation(targetPosition, targetRotation);

        // Copy every MeshFilter+MeshRenderer from the source part hierarchy
        MeshFilter[] filters = sourcePart.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;

            GameObject piece = new(mf.name);
            piece.transform.SetParent(_ghostRoot.transform);

            // Preserve the local transform relative to the source part root
            piece.transform.localPosition = mf.transform.localPosition;
            piece.transform.localRotation = mf.transform.localRotation;
            piece.transform.localScale = mf.transform.localScale;

            // Add mesh filter
            var filter = piece.AddComponent<MeshFilter>();
            filter.sharedMesh = mf.sharedMesh;

            // Add mesh renderer with ghost material
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.material = _runtimeMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _ghostRenderers.Add(renderer);
        }

        // Store the base alpha from ghostColor
        _baseAlpha = ghostColor.a;
    }

    /// <summary>
    /// Update the ghost's position and rotation (call every frame while visible).
    /// </summary>
    public void UpdateTarget(Vector3 position, Quaternion rotation)
    {
        if (_ghostRoot != null)
        {
            _ghostRoot.transform.SetPositionAndRotation(position, rotation);
        }
    }

    /// <summary>
    /// Show the ghost with idle pulsing animation.
    /// </summary>
    public void Show()
    {
        if (_ghostRoot == null) return;
        _ghostRoot.SetActive(true);
        _state = GhostState.IdlePulse;
        _stateTime = 0f;
        ApplyGhostColor(ghostColor);
    }

    /// <summary>
    /// Hide and destroy all ghost meshes.
    /// </summary>
    public void ClearGhost()
    {
        if (_ghostRoot != null)
        {
            Destroy(_ghostRoot);
            _ghostRoot = null;
        }
        _ghostRenderers.Clear();
        _state = GhostState.Hidden;
    }

    /// <summary>
    /// Called when the part snaps — plays a bright flash and fade-out.
    /// </summary>
    public void FlashSnap()
    {
        if (_state == GhostState.Hidden) return;
        _state = GhostState.SnapFlash;
        _stateTime = 0f;
    }

    /// <summary>
    /// Brighten the ghost as the part approaches the snap zone (t: 0=far, 1=at snap distance).
    /// </summary>
    public void SetProximity(float t)
    {
        if (_state != GhostState.IdlePulse || _ghostRoot == null) return;
        // Boost alpha and add green tint as part gets closer
        Color c = ghostColor;
        c.a = Mathf.Lerp(_baseAlpha, _baseAlpha * 3.5f, t);
        c.g = Mathf.Lerp(ghostColor.g, 1f, t);
        ApplyGhostColor(c);
    }

    // ── Animation Loop ─────────────────────────────────────────────────────────

    void Update()
    {
        if (_state == GhostState.Hidden || _ghostRoot == null) return;

        _stateTime += Time.deltaTime;

        switch (_state)
        {
            case GhostState.IdlePulse:
                // Gentle alpha pulse
                float pulse = 1f + Mathf.Sin(_stateTime * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
                Color idleColor = ghostColor;
                idleColor.a = _baseAlpha * pulse;
                ApplyGhostColor(idleColor);
                break;

            case GhostState.SnapFlash:
                // Bright flash that fades out
                float t = Mathf.Clamp01(_stateTime / flashDuration);
                Color flashColor = ghostColor;
                flashColor.a = Mathf.Lerp(_baseAlpha * 3f, 0f, t);
                flashColor.r = Mathf.Lerp(1f, ghostColor.r, t);
                flashColor.g = Mathf.Lerp(1f, ghostColor.g, t);
                flashColor.b = Mathf.Lerp(1f, ghostColor.b, t);
                ApplyGhostColor(flashColor);

                if (t >= 1f)
                {
                    ClearGhost();
                }
                break;
        }
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private void ApplyGhostColor(Color color)
    {
        // Set both color and main color properties for shader compatibility
        _runtimeMat.color = color;
        _runtimeMat.SetColor("_Color", color);
        _runtimeMat.SetColor("_TintColor", color);

        // Enable transparency
        _runtimeMat.SetFloat("_Mode", 3f); // Transparent mode for URP/Legacy
        _runtimeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _runtimeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _runtimeMat.SetInt("_ZWrite", 0);
        _runtimeMat.renderQueue = 3000; // Transparent queue
    }
}