using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// JetEngineAirflowController
/// ──────────────────────────
/// Generates a single continuous airflow tube along the engine axis.
/// The tube uses a position-based gradient (blue → cyan → white → orange → red)
/// and fills from left to right based on _progress (0→1).
/// </summary>
public class JetEngineAirflowController : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("Material using Custom/AirflowEffect shader.")]
    public Material airflowMaterial;

    [Header("Engine Axis")]
    [Tooltip("Direction the engine faces. Usually Vector3.right for this jet engine model.")]
    public Vector3 engineAxis = Vector3.right;

    [Tooltip("Center of the engine in world space. Leave at zero to auto-use this GameObject's position.")]
    public Vector3 engineCenter = Vector3.zero;

    [Tooltip("Total length of the engine along its axis (world units). Use Auto-Setup tool to measure.")]
    public float engineLength = 2.0f;

    [Tooltip("Base radius of the airflow tube. Use Auto-Setup tool to measure.")]
    public float baseRadius = 0.18f;

    [Tooltip("Scale multiplier applied on top of baseRadius. Reduce this if tube is too large (try 0.1 - 0.3).")]
    [Range(0.01f, 1f)]
    public float radiusScale = 0.15f;

    [Header("Outer Covers to Hide")]
    [Tooltip("Assign Left Outer Cover and Right Outer Cover GameObjects here.")]
    public GameObject[] outerCovers;

    [Header("Debug")]
    [Tooltip("Draw gizmos in Scene view showing the tube position and size.")]
    public bool showGizmos = true;

    [Header("Cover Slide Off")]
    [Tooltip("World-space direction the covers slide when Show Working starts.")]
    public Vector3 coverSlideDirection = Vector3.up;
    [Tooltip("How far the covers slide (world units).")]
    public float coverSlideDistance = 1.5f;
    [Range(0.3f, 2f)] public float coverSlideDuration = 0.6f;

    [Header("Progress Fill")]
    [Range(0.3f, 3f)] public float progressFillDuration = 0.8f;

    [Header("Tube Transform Override")]
    [Tooltip("If true, uses the localPosition/localRotation/localScale below instead of procedural positioning.")]
    public bool overrideTubeTransform = true;
    [Tooltip("Local position of Airflow_ContinuousTube.")]
    public Vector3 tubeLocalPosition = new Vector3(-0.0011f, 0.0087f, 0.0047f);
    [Tooltip("Local rotation (Euler) of Airflow_ContinuousTube.")]
    public Vector3 tubeLocalEulerAngles = new Vector3(0f, -180f, -90f);
    [Tooltip("Local scale of Airflow_ContinuousTube.")]
    public Vector3 tubeLocalScale = new Vector3(4.25f, 0.82f, 2.11f);

    // ── Internal ──────────────────────────────────────────────────────────────
    private GameObject _airflowTube;
    private Material _tubeMaterial;

    private bool _coversHidden;

    // Track the SlideAndFadeCovers coroutine so we can cancel it on StopAirflow()
    private Coroutine _slideCoroutine;

    // Progress animation
    private Coroutine _progressCoroutine;
    private float _currentProgress;    // 0..1, the actual visual progress
    private float _targetProgress;     // 0..1, where we're animating to

    // VFX coroutines
    private Coroutine _radiusCoroutine;
    private Coroutine _colorCoroutine;

    // ── VFX: Tube Radius Animation ──────────────────────────────────────────────

    /// <summary>
    /// Animate the tube's radiusScale over time using an AnimationCurve.
    /// targetMultiplier: peak multiplier relative to current radiusScale (e.g., 0.6 = narrow to 60%).
    /// </summary>
    public void AnimateRadiusScale(float targetMultiplier, float duration, System.Action onComplete = null)
    {
        if (_radiusCoroutine != null) StopCoroutine(_radiusCoroutine);
        _radiusCoroutine = StartCoroutine(AnimateRadiusScaleImpl(targetMultiplier, duration, onComplete));
    }

    /// <summary>
    /// Stop radius scale animation and keep current value.
    /// </summary>
    public void StopRadiusAnimation()
    {
        if (_radiusCoroutine != null)
        {
            StopCoroutine(_radiusCoroutine);
            _radiusCoroutine = null;
        }
    }

    private IEnumerator AnimateRadiusScaleImpl(float targetMultiplier, float duration, System.Action onComplete)
    {
        float startScale = radiusScale;
        float targetScale = startScale * targetMultiplier;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            radiusScale = Mathf.Lerp(startScale, targetScale, t);
            yield return null;
        }

        radiusScale = targetScale;
        _radiusCoroutine = null;
        onComplete?.Invoke();
    }

    // ── VFX: Tube Color Animation ────────────────────────────────────────────────

    /// <summary>
    /// Animate the tube material's _Color (or _BaseColor) to a target color and back.
    /// If the material doesn't have a color property, does nothing.
    /// </summary>
    public void AnimateTubeColor(Color targetColor, float duration, System.Action onComplete = null)
    {
        if (_colorCoroutine != null) StopCoroutine(_colorCoroutine);
        _colorCoroutine = StartCoroutine(AnimateTubeColorImpl(targetColor, duration, onComplete));
    }

    /// <summary>
    /// Stop tube color animation and reset to white.
    /// </summary>
    public void StopTubeColorAnimation()
    {
        if (_colorCoroutine != null)
        {
            StopCoroutine(_colorCoroutine);
            _colorCoroutine = null;
        }
        if (_tubeMaterial != null)
        {
            if (_tubeMaterial.HasProperty("_BaseColor"))
                _tubeMaterial.SetColor("_BaseColor", Color.white);
            else if (_tubeMaterial.HasProperty("_Color"))
                _tubeMaterial.SetColor("_Color", Color.white);
        }
    }

    private IEnumerator AnimateTubeColorImpl(Color targetColor, float duration, System.Action onComplete)
    {
        if (_tubeMaterial == null) yield break;

        // Determine which color property the material uses
        bool useBaseColor = _tubeMaterial.HasProperty("_BaseColor");
        bool useColor = _tubeMaterial.HasProperty("_Color");
        if (!useBaseColor && !useColor) yield break;

        Color startColor = Color.white;
        if (useBaseColor) startColor = _tubeMaterial.GetColor("_BaseColor");
        else if (useColor) startColor = _tubeMaterial.GetColor("_Color");

        float halfDur = duration * 0.5f;
        float elapsed = 0f;

        // Phase 1: fade TO target color
        while (elapsed < halfDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDur);
            Color c = Color.Lerp(startColor, targetColor, t);
            if (useBaseColor) _tubeMaterial.SetColor("_BaseColor", c);
            else _tubeMaterial.SetColor("_Color", c);
            yield return null;
        }

        // Hold briefly
        yield return new WaitForSeconds(0.3f);

        // Phase 2: fade BACK to white
        elapsed = 0f;
        while (elapsed < halfDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDur);
            Color c = Color.Lerp(targetColor, Color.white, t);
            if (useBaseColor) _tubeMaterial.SetColor("_BaseColor", c);
            else _tubeMaterial.SetColor("_Color", c);
            yield return null;
        }

        // Final restore
        if (useBaseColor) _tubeMaterial.SetColor("_BaseColor", Color.white);
        else if (useColor) _tubeMaterial.SetColor("_Color", Color.white);

        _colorCoroutine = null;
        onComplete?.Invoke();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public float CurrentProgress => _targetProgress;

    /// <summary>
    /// Start the airflow — slide covers off, show the overlay, fade in the tube.
    /// </summary>
    public void StartAirflow()
    {
        // Slide covers off — store coroutine reference so StopAirflow can cancel it
        if (!_coversHidden)
            _slideCoroutine = StartCoroutine(SlideAndFadeCovers());

        // Start with Progress=0 so the tube is visible (just empty/gone at the start)
        if (_airflowTube != null)
        {
            _airflowTube.SetActive(true);
            SetProgressImmediate(0f);
        }
    }

    /// <summary>
    /// Set how far the airflow gradient fills from intake (0) to exhaust (1).
    /// Animates smoothly. Previous zones stay visible (cumulative fill).
    /// </summary>
    public void SetProgress(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
        if (_progressCoroutine != null) StopCoroutine(_progressCoroutine);
        _progressCoroutine = StartCoroutine(AnimateProgress(_targetProgress));
    }

    /// <summary>
    /// Stop all airflow and restore covers.
    /// </summary>
    /// <summary>
    /// Show the airflow tube without sliding/fading covers.
    /// Used by interactive Show Working mode where the user removes covers manually.
    /// </summary>
    public void ShowTubeImmediate()
    {
        if (_airflowTube != null)
        {
            bool wasHidden = !_airflowTube.activeSelf;
            _airflowTube.SetActive(true);

            // Only reset progress to 0 on the FIRST reveal of the tube.
            // If it was already visible, preserve the current progress so
            // subsequent calls don't wipe out progress set by AdvanceAirflowTo().
            if (wasHidden)
                SetProgressImmediate(0f);
        }

        // Mark covers as "handled" so StartAirflow won't re-slide them if called later
        _coversHidden = true;
    }

    public void StopAirflow()
    {
        // Stop progress animation
        if (_progressCoroutine != null) { StopCoroutine(_progressCoroutine); _progressCoroutine = null; }

        // CRITICAL: Stop the orphan SlideAndFadeCovers coroutine before restoring covers.
        // Without this, the coroutine can complete later and call cover.SetActive(false),
        // hiding the cover even though we just restored it via RestoreCovers().
        if (_slideCoroutine != null) { StopCoroutine(_slideCoroutine); _slideCoroutine = null; }

        // Hide tube
        if (_airflowTube != null) _airflowTube.SetActive(false);

        RestoreCovers();

        _currentProgress = 0f;
        _targetProgress = 0f;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (airflowMaterial == null)
        {
            var shader = Shader.Find("Custom/AirflowEffect");
            if (shader != null) airflowMaterial = new Material(shader);
        }
        BuildSingleTube();
    }

    void OnDestroy()
    {
        if (_tubeMaterial != null) Destroy(_tubeMaterial);
    }

    // ── Tube construction ─────────────────────────────────────────────────────

    void BuildSingleTube()
    {
        // engineLength and baseRadius are in LOCAL units (set by AutoMeasure).
        float localLength = engineLength;
        float localRadius = baseRadius * radiusScale;

        // Build a single tapered tube spanning the full engine length, centered at origin.
        // Radius profile (fractions of localRadius):
        //   t=0.0 (intake)    : 1.4x  wide
        //   t=0.2 (compress)  : 0.9x  narrower
        //   t=0.4 (combust)   : 0.6x  narrowest
        //   t=0.6 (turbine)   : 0.7x
        //   t=0.8 (exhaust)   : 1.0x
        //   t=1.0 (exit)      : 1.6x  flared
        int radialSegments = 24;
        int heightSegments = 24;

        var mesh = new Mesh();
        mesh.name = "AirflowTube_Continuous";

        var verts   = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs     = new List<Vector2>();
        var tris    = new List<int>();

        for (int h = 0; h <= heightSegments; h++)
        {
            float t      = (float)h / heightSegments;
            float y      = -localLength * 0.5f + localLength * t;  // centered: -half to +half
            float radius = RadiusProfile(t) * localRadius;

            for (int r = 0; r <= radialSegments; r++)
            {
                float angle = (float)r / radialSegments * Mathf.PI * 2f;
                float x     = Mathf.Cos(angle) * radius;
                float z     = Mathf.Sin(angle) * radius;

                verts.Add(new Vector3(x, y, z));
                Vector3 n = new Vector3(x, 0, z).normalized;
                normals.Add(n);
                // UV.y = t runs 0→1 along the tube length
                uvs.Add(new Vector2((float)r / radialSegments, t));
            }
        }

        for (int h = 0; h < heightSegments; h++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int i0 = h       * (radialSegments + 1) + r;
                int i1 = i0 + 1;
                int i2 = (h + 1) * (radialSegments + 1) + r;
                int i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        // Create the tube GameObject
        _airflowTube = new GameObject("Airflow_ContinuousTube");
        _airflowTube.transform.SetParent(transform, false);

        // Use override transform values if enabled, otherwise fall back to procedural positioning
        if (overrideTubeTransform)
        {
            _airflowTube.transform.localPosition = tubeLocalPosition;
            _airflowTube.transform.localEulerAngles = tubeLocalEulerAngles;
            _airflowTube.transform.localScale = tubeLocalScale;
        }
        else
        {
            Vector3 worldCenter = (engineCenter == Vector3.zero) ? transform.position : engineCenter;
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            Vector3 localStart  = localCenter - engineAxis * (localLength * 0.5f);
            _airflowTube.transform.localPosition = localStart;
            _airflowTube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, engineAxis);
            _airflowTube.transform.localScale = Vector3.one;
        }

        var mf = _airflowTube.AddComponent<MeshFilter>();
        var mr = _airflowTube.AddComponent<MeshRenderer>();
        mf.mesh = mesh;

        if (airflowMaterial != null)
        {
            _tubeMaterial = new Material(airflowMaterial);
            _tubeMaterial.SetFloat("_Progress", 0f);
            _tubeMaterial.SetFloat("_Opacity", 0.105f);
            mr.material = _tubeMaterial;
        }

        _airflowTube.SetActive(false);
    }

    /// <summary>
    /// Smooth radius profile along the engine axis (t = 0 intake → 1 exhaust).
    /// </summary>
    static float RadiusProfile(float t)
    {
        // Key points along the tube
        float[] keyT = { 0.00f, 0.20f, 0.40f, 0.60f, 0.80f, 1.00f };
        float[] keyR = { 1.40f, 0.90f, 0.60f, 0.70f, 1.00f, 1.60f };

        // Find which segment we're in
        for (int i = 0; i < keyT.Length - 1; i++)
        {
            if (t >= keyT[i] && t <= keyT[i + 1])
            {
                float seg = (t - keyT[i]) / (keyT[i + 1] - keyT[i]);
                return Mathf.Lerp(keyR[i], keyR[i + 1], seg);
            }
        }
        return keyR[^1];
    }

    // ── Progress animation ────────────────────────────────────────────────────

    void SetProgressImmediate(float value)
    {
        _currentProgress = value;
        _targetProgress = value;
        if (_tubeMaterial != null)
            _tubeMaterial.SetFloat("_Progress", value);
    }

    IEnumerator AnimateProgress(float target)
    {
        float start = _currentProgress;
        float elapsed = 0f;

        while (elapsed < progressFillDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / progressFillDuration);
            float value = Mathf.Lerp(start, target, t);
            _currentProgress = value;
            if (_tubeMaterial != null)
                _tubeMaterial.SetFloat("_Progress", value);
            yield return null;
        }

        _currentProgress = target;
        if (_tubeMaterial != null)
            _tubeMaterial.SetFloat("_Progress", target);
        _progressCoroutine = null;
    }

    // ── Cover slide + fade ────────────────────────────────────────────────────

    IEnumerator SlideAndFadeCovers()
    {
        if (outerCovers == null || outerCovers.Length == 0) { _coversHidden = true; yield break; }

        var coverData = new List<(Material[] fadeMats, Transform tr, Vector3 startPos)>();

        foreach (var cover in outerCovers)
        {
            if (cover == null) continue;
            foreach (var rend in cover.GetComponentsInChildren<Renderer>())
            {
                var origMats = rend.materials;
                var fadeMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    fadeMats[i] = new Material(origMats[i]);
                    SetTransparent(fadeMats[i]);
                }
                rend.materials = fadeMats;
                coverData.Add((fadeMats, rend.transform, rend.transform.position));
            }
        }

        Vector3 slideOffset = coverSlideDirection.normalized * coverSlideDistance;
        float elapsed = 0f;

        while (elapsed < coverSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.SmoothStep(0f, 1f, elapsed / coverSlideDuration);
            float alpha = Mathf.Lerp(1f, 0f, t);

            foreach (var (fadeMats, tr, startPos) in coverData)
            {
                tr.position = startPos + slideOffset * t;
                foreach (var mat in fadeMats) SetAlpha(mat, alpha);
            }
            yield return null;
        }

        foreach (var cover in outerCovers)
            if (cover != null) cover.SetActive(false);

        foreach (var (fadeMats, _, _) in coverData)
            foreach (var mat in fadeMats) Destroy(mat);

        _coversHidden = true;
        _slideCoroutine = null;
    }

    void RestoreCovers()
    {
        if (outerCovers == null) return;
        foreach (var cover in outerCovers)
            if (cover != null) cover.SetActive(true);
        _coversHidden = false;
    }

    // ── Editor helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Measures all child Renderers to compute engine center, length and radius.
    /// Called by the JetEngineShowWorkingSetup editor tool.
    /// </summary>
    public void AutoMeasureFromRenderers()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        float length = Mathf.Abs(Vector3.Dot(localSize, engineAxis.normalized));
        float radius = 0f;
        for (int i = 0; i < 3; i++)
        {
            if (i == 2) continue; // skip axis-aligned dimension
            float dim = Mathf.Abs(localSize[i]) * 0.5f;
            if (dim > radius) radius = dim;
        }
        engineLength = length;
        baseRadius = radius;
        if (engineCenter == Vector3.zero)
            engineCenter = bounds.center;

        Debug.Log($"[JetEngineAirflowController] AutoMeasured: Length={length:F3}, Radius={radius:F3}");
    }

    public void AutoFindOuterCovers()
    {
        outerCovers = null;
        var found = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name.IndexOf("Outer_cover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Outercover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Outer Cover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Left_Outer_Cover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Right_Outer_Cover", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found.Add(child.gameObject);
            }
        }
        outerCovers = found.ToArray();
        Debug.Log($"[JetEngineAirflowController] Found {outerCovers.Length} outer covers.");
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        float len = engineLength;
        float rad = baseRadius * radiusScale;
        Vector3 center = (engineCenter == Vector3.zero) ? transform.position : engineCenter;
        Vector3 start = center - engineAxis.normalized * len * 0.5f;
        Vector3 end   = center + engineAxis.normalized * len * 0.5f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(start, rad * RadiusProfile(0f));
        Gizmos.DrawWireSphere(end,   rad * RadiusProfile(1f));
        Gizmos.DrawLine(start, end);

        // Show progress point
        float progT = _targetProgress;
        Vector3 progPos = start + engineAxis.normalized * len * progT;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(progPos, 0.03f);

        // Draw a few radius profiles along the tube
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
        for (float t = 0; t <= 1f; t += 0.1f)
        {
            Vector3 pos = start + engineAxis.normalized * len * t;
            Gizmos.DrawWireSphere(pos, rad * RadiusProfile(t));
        }
    }

    // ── Material helpers ──────────────────────────────────────────────────────

    static void SetTransparent(Material mat)
    {
        if (mat.HasProperty("_AlphaMode"))
        {
            mat.SetFloat("_AlphaMode", 1);
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            return;
        }
        mat.SetFloat("_Mode", 2);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",   0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
    }

    static void SetAlpha(Material mat, float alpha)
    {
        if (mat.HasProperty("_BaseColorFactor")) { var c = mat.GetColor("_BaseColorFactor"); mat.SetColor("_BaseColorFactor", new Color(c.r, c.g, c.b, alpha)); }
        if (mat.HasProperty("_BaseColor"))       { var c = mat.GetColor("_BaseColor");       mat.SetColor("_BaseColor",       new Color(c.r, c.g, c.b, alpha)); }
        else if (mat.HasProperty("_Color"))      { var c = mat.GetColor("_Color");           mat.SetColor("_Color",           new Color(c.r, c.g, c.b, alpha)); }
        else                                     { var c = mat.color; mat.color = new Color(c.r, c.g, c.b, alpha); }
    }
}
