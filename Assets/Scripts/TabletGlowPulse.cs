using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Adds a pulsing golden edge glow to the tablet.
/// Attach this to the same GameObject as GrabbableTablet.
///
/// At runtime it spawns a slightly oversized quad behind the tablet
/// using the TabletGlow shader. The glow pulses until the user grabs
/// the tablet, then fades out and is destroyed.
///
/// No prefab or material asset needed — everything is created in code.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TabletGlowPulse : MonoBehaviour
{
    [Header("Glow Appearance")]
    [Tooltip("Golden glow colour. Alpha is ignored — the shader controls opacity.")]
    public Color glowColor = new Color(1f, 0.78f, 0.1f, 1f);

    [Tooltip("How far the glow quad extends beyond the tablet edges (world units).")]
    public float glowBorder = 0.015f;

    [Tooltip("How far behind the tablet the glow quad sits (world units). " +
             "Increase if the glow clips through the tablet mesh.")]
    public float glowOffset = 0.002f;

    [Tooltip("Width of the glowing edge band in UV space. " +
             "Smaller = thinner edge line, larger = wider glow.")]
    [Range(0.01f, 0.5f)]
    public float edgeSoftness = 0.12f;

    [Header("Pulse")]
    [Tooltip("Minimum brightness of the pulse (0 = fully off at the bottom of the pulse).")]
    [Range(0f, 1f)]
    public float pulseMin = 0.3f;

    [Tooltip("Maximum brightness of the pulse.")]
    [Range(0f, 1f)]
    public float pulseMax = 1f;

    [Tooltip("Pulses per second.")]
    [Range(0.1f, 4f)]
    public float pulseSpeed = 1.2f;

    [Header("Fade Out")]
    [Tooltip("How long the glow takes to fade out after the tablet is grabbed (seconds).")]
    [Range(0.1f, 2f)]
    public float fadeOutDuration = 0.4f;

    // ── Internals ─────────────────────────────────────────────────────────────
    private XRGrabInteractable _grab;
    private GameObject         _glowQuad;
    private Material           _glowMat;
    private bool               _grabbed;
    private Coroutine          _fadeCoroutine;

    // ── Tablet size detection ─────────────────────────────────────────────────
    // We try to read the size from a MeshFilter or Renderer bounds.
    // Falls back to a sensible default if neither is found.
    private Vector2 _tabletSize = new Vector2(0.22f, 0.14f); // width × height in local units

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnGrabbed);

        DetectTabletSize();
        BuildGlowQuad();
    }

    void OnDestroy()
    {
        if (_grab != null)
            _grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void Update()
    {
        if (_grabbed || _glowMat == null) return;

        // Smooth sine pulse between pulseMin and pulseMax
        float t     = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float power = Mathf.Lerp(pulseMin, pulseMax, t);
        _glowMat.SetFloat("_GlowPower", power);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void DetectTabletSize()
    {
        // Try MeshFilter first (most accurate — uses actual mesh bounds)
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            // bounds.size is in local space of the mesh object
            // We want the XY face size (assuming tablet lies in XY plane)
            _tabletSize = new Vector2(b.size.x, b.size.y);

            // If the mesh is oriented differently (e.g. XZ plane), swap axes
            if (b.size.y < b.size.z * 0.5f)
                _tabletSize = new Vector2(b.size.x, b.size.z);

            return;
        }

        // Fallback: use renderer bounds
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // Convert world-space bounds to local scale
            Vector3 localSize = transform.InverseTransformVector(rend.bounds.size);
            _tabletSize = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
            if (Mathf.Abs(localSize.y) < Mathf.Abs(localSize.z) * 0.5f)
                _tabletSize = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.z));
        }
    }

    void BuildGlowQuad()
    {
        var shader = Shader.Find("Custom/TabletGlow");
        if (shader == null)
        {
            Debug.LogError("[TabletGlowPulse] Custom/TabletGlow shader not found! " +
                           "Make sure TabletGlow.shader is in Assets/Shaders/.");
            return;
        }

        // ── Material ──────────────────────────────────────────────────────────
        _glowMat = new Material(shader);
        _glowMat.SetColor("_GlowColor",  glowColor);
        _glowMat.SetFloat("_GlowPower",  pulseMin);
        _glowMat.SetFloat("_EdgeSoft",   edgeSoftness);
        _glowMat.hideFlags = HideFlags.HideAndDontSave;

        // ── Quad mesh ─────────────────────────────────────────────────────────
        float hw = (_tabletSize.x * 0.5f) + glowBorder; // half-width  + border
        float hh = (_tabletSize.y * 0.5f) + glowBorder; // half-height + border

        Mesh quad = new Mesh { name = "TabletGlowQuad" };
        quad.vertices = new Vector3[]
        {
            new Vector3(-hw, -hh, 0),
            new Vector3( hw, -hh, 0),
            new Vector3( hw,  hh, 0),
            new Vector3(-hw,  hh, 0),
        };
        quad.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
        };
        quad.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        quad.RecalculateNormals();
        quad.RecalculateBounds();

        // ── GameObject ────────────────────────────────────────────────────────
        _glowQuad = new GameObject("TabletGlowQuad");
        _glowQuad.transform.SetParent(transform, false);

        // Push slightly behind the tablet face so it doesn't z-fight
        _glowQuad.transform.localPosition = new Vector3(0f, 0f, glowOffset);
        _glowQuad.transform.localRotation = Quaternion.identity;
        _glowQuad.transform.localScale    = Vector3.one;

        var mf = _glowQuad.AddComponent<MeshFilter>();
        mf.mesh = quad;

        var mr = _glowQuad.AddComponent<MeshRenderer>();
        mr.material           = _glowMat;
        mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows     = false;
    }

    // ── Grab handler ──────────────────────────────────────────────────────────

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_grabbed) return;
        _grabbed = true;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeOutAndDestroy()
    {
        float startPower = _glowMat != null ? _glowMat.GetFloat("_GlowPower") : pulseMax;
        float elapsed    = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float power = Mathf.Lerp(startPower, 0f, elapsed / fadeOutDuration);
            if (_glowMat != null) _glowMat.SetFloat("_GlowPower", power);
            yield return null;
        }

        if (_glowQuad != null) Destroy(_glowQuad);
        if (_glowMat  != null) Destroy(_glowMat);
    }
}
