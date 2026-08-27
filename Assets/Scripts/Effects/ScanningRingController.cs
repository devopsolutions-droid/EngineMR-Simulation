using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns a flat horizontal ring that sweeps up and down a part's bounding volume,
/// creating a "scanning" visual effect. Uses the Custom/ScanningRing shader.
/// Spawned and managed by XRayVisionController on hover.
/// </summary>
public class ScanningRingController : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Speed of the up/down sweep oscillation.")]
    public float sweepSpeed = 1.2f;

    [Tooltip("How much the ring expands beyond the part's XZ bounds (1.0 = exact fit).")]
    [Range(1.0f, 2.0f)]
    public float ringScaleFactor = 1.3f;

    [Tooltip("Sweep range as a fraction of the part's height (1.0 = full height).")]
    [Range(0.2f, 1.5f)]
    public float sweepHeightFactor = 0.7f;

    [Tooltip("Duration of the fade-out on hover exit.")]
    public float fadeOutDuration = 0.3f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private MeshRenderer _renderer;
    private Material     _ringMaterial;
    private Bounds       _targetBounds;
    private Coroutine    _sweepCoroutine;
    private Coroutine    _fadeCoroutine;
    private float        _currentFadeAlpha = 1f;

    /// <summary>
    /// Call once after instantiation to set up the ring mesh + material.
    /// </summary>
    public void Initialize(Material material)
    {
        // Generate a flat ring mesh
        Mesh ringMesh = CreateRingMesh(innerRadius: 0.4f, outerRadius: 0.6f, segments: 48);

        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = ringMesh;

        _renderer = gameObject.AddComponent<MeshRenderer>();
        _ringMaterial = new Material(material);
        _renderer.material = _ringMaterial;
    }

    /// <summary>
    /// Start scanning the given renderer's bounding volume.
    /// </summary>
    public void StartScan(Renderer targetRenderer)
    {
        if (targetRenderer == null) return;

        _targetBounds = targetRenderer.bounds;

        // Position ring at centre, scale X/Z to wrap around the part
        transform.position = _targetBounds.center;

        float diameter = Mathf.Max(_targetBounds.size.x, _targetBounds.size.z) * ringScaleFactor;
        transform.localScale = Vector3.one * diameter;

        // Ensure full visibility
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        _currentFadeAlpha = 1f;
        if (_ringMaterial != null)
            _ringMaterial.SetFloat("_FadeAlpha", 1f);

        // Start sweep loop
        if (_sweepCoroutine != null) StopCoroutine(_sweepCoroutine);
        _sweepCoroutine = StartCoroutine(SweepLoop());
    }

    /// <summary>
    /// Stop scanning and fade out, then destroy self.
    /// </summary>
    public void StopScan()
    {
        if (_sweepCoroutine != null) { StopCoroutine(_sweepCoroutine); _sweepCoroutine = null; }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutAndDestroy());
    }

    // ── Sweep animation ───────────────────────────────────────────────────────

    private IEnumerator SweepLoop()
    {
        float halfHeight = _targetBounds.size.y * 0.5f * sweepHeightFactor;
        Vector3 basePos = _targetBounds.center;

        while (true)
        {
            // Oscillate: sin ranges from -1 to 1, remap to 0..1 range
            float t = Mathf.Sin(Time.time * sweepSpeed) * 0.5f + 0.5f;
            float yOffset = Mathf.Lerp(-halfHeight, halfHeight, t);

            Vector3 pos = basePos;
            pos.y = basePos.y + yOffset;
            transform.position = pos;

            yield return null;
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _currentFadeAlpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            if (_ringMaterial != null)
                _ringMaterial.SetFloat("_FadeAlpha", _currentFadeAlpha);
            yield return null;
        }

        if (_ringMaterial != null) Destroy(_ringMaterial);
        Destroy(gameObject);
    }

    // ── Ring mesh generation ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a flat horizontal ring mesh (torus-like disc) with UV.x = radial
    /// coordinate (0 = inner edge, 1 = outer edge) and UV.y = circumferential.
    /// </summary>
    private static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ScanningRing_Generated";

        int vertexCount = segments * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv       = new Vector2[vertexCount];
        int[]     tris     = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos   = Mathf.Cos(angle);
            float sin   = Mathf.Sin(angle);

            // Inner ring vertex  (UV.x = 0)
            vertices[i * 2]     = new Vector3(innerRadius * cos, 0f, innerRadius * sin);
            uv[i * 2]           = new Vector2(0f, (float)i / segments);

            // Outer ring vertex  (UV.x = 1)
            vertices[i * 2 + 1] = new Vector3(outerRadius * cos, 0f, outerRadius * sin);
            uv[i * 2 + 1]       = new Vector2(1f, (float)i / segments);

            int next = (i + 1) % segments;
            int t    = i * 6;

            // Two triangles per segment forming a quad strip
            tris[t + 0] = i * 2;
            tris[t + 1] = next * 2;
            tris[t + 2] = i * 2 + 1;

            tris[t + 3] = i * 2 + 1;
            tris[t + 4] = next * 2;
            tris[t + 5] = next * 2 + 1;
        }

        mesh.vertices  = vertices;
        mesh.uv        = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}