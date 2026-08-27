using UnityEngine;

using System.Collections;

using System.Collections.Generic;
using System.Linq;



/// <summary>

/// Advanced XRay Vision Controller

/// ─────────────────────────────────

/// Drives the Custom/XRayVision shader with:

///   - CT scan sweep entry effect (top → bottom reveal)

///   - Glitch flicker on activation

///   - Fresnel rim + depth-based coloring (always on)

///   - Animated scan lines + circuit pulse lines (always on)

///   - Hover highlight: hovered part brightens white-hot, others stay dim

///   - Fix B: Per-part colored outlines (outline tint matches part xrayColor)

///   - Fix C: Depth-based alpha fade (deeper parts slightly more transparent)

///

/// HOW TO USE:

///   1. Create a Material using Custom/XRayVision shader

///   2. Assign it to XRayMaterial

///   3. Assign engine root to TargetRoot

///   4. Call ActivateXRay() / DeactivateXRay()

///   5. Call SetHoveredPart(part) from EngineInteractor when in xray mode

/// </summary>

public class XRayVisionController : MonoBehaviour

{

    [Header("Target")]

    public Transform targetRoot;



    [Header("XRay Material")]

    public Material xRayMaterial;

    [Header("Disabled Parts")]

    public List<GameObject> partsToDisable;



    [Header("Colors")]

    public Color rimColor       = new Color(0.0f, 0.85f, 1.0f, 1.0f);

    public Color depthNearColor = new Color(1.0f, 0.55f, 0.1f,  1.0f);

    public Color depthFarColor  = new Color(0.0f, 0.4f,  1.0f,  1.0f);

    public Color circuitColor   = new Color(0.2f, 1.0f,  0.8f,  1.0f);



    [Header("Rim & Body")]

    [Range(1f, 8f)]   public float rimPower       = 3.5f;

    [Range(0f, 3f)]   public float rimIntensity   = 1.8f;

    [Range(0f, 1.0f)] public float bodyAlpha      = 0.35f;



    [Header("Depth Coloring")]

    [Range(0.5f, 20f)] public float depthRange     = 6f;

    [Range(0f, 1f)]    public float depthInfluence = 0.7f;



    [Header("Scan Lines")]

    [Range(0f, 4f)]    public float scanSpeed      = 1.2f;

    [Range(5f, 120f)]  public float scanDensity    = 35f;

    [Range(0.01f, 0.95f)] public float scanLineWidth = 0.25f;

    [Range(0f, 1f)]    public float scanBrightness = 0.55f;

    [Range(0f, 0.3f)]  public float scanAlpha      = 0.12f;



    [Header("Circuit Pulses")]

    [Range(0f, 6f)]    public float circuitSpeed     = 2.5f;

    [Range(1f, 40f)]   public float circuitDensity   = 12f;

    [Range(0.01f, 0.3f)] public float circuitWidth   = 0.06f;

    [Range(0f, 2f)]    public float circuitIntensity = 1.2f;



    [Header("Sweep Entry")]

    [Range(0.3f, 4f)]  public float sweepDuration  = 1.8f;

    [Range(0.01f, 2f)] public float sweepBandWidth = 0.4f;

    [Range(0f, 5f)]    public float sweepIntensity  = 3.5f;



    [Header("Glitch")]

    [Range(0f, 1f)]    public float glitchDuration  = 0.5f;



    [Header("Outline")]

    [Range(1f, 8f)]    public float outlineWidth    = 1.2f;



    [Header("Scanner Mode")]

    public Material scanningRingMaterial;

    [Range(0.5f, 3f)] public float hoverBrightness = 1.2f;

    [Range(0f, 2f)]   public float hoverOpacity    = 1.0f;



    // ── Internal ──────────────────────────────────────────────────────────────

    private struct RendererData

    {

        public Renderer   renderer;

        public Material[] originalMaterials;

    }



    private List<RendererData> _renderers        = new List<RendererData>();

    private Material           _sharedMat;           // template material, holds runtime sweep/glitch state

    private List<Material>     _instanceMaterials    = new List<Material>(); // one per renderer (Fix B: per-part tint)

    private Dictionary<Renderer, Material> _hoverMat = new Dictionary<Renderer, Material>(); // per-part hover override

    private List<GameObject>   _hiddenExcludedObjects = new List<GameObject>();

    private bool               _active;

    private Coroutine          _entryCoroutine;

    private Bounds             _engineBounds;

    private ScanningRingController _currentScanningRing;

    private Renderer           _lastHoveredRenderer;

    private EnginePart         _lockedPart;



    public EnginePart LockedPart

    {

        get => _lockedPart;

        set

        {

            _lockedPart = value;

            SetHoveredPart(_lastHoveredRenderer);

        }

    }



    // ── Public API ────────────────────────────────────────────────────────────



    public void ActivateXRay()

    {

        Debug.Log($"[XrayMeshissue] ActivateXRay() called. _active was {_active}");

        if (_active) return;

        HandleExclusions(true);

        CollectRenderers();

        ComputeEngineBounds();

        CreateSharedMaterial();

        SwapToXRay();

        DisableConfiguredParts();

        _active = true;



        if (_entryCoroutine != null) StopCoroutine(_entryCoroutine);

        _entryCoroutine = StartCoroutine(EntrySequence());

    }



    public void DeactivateXRay()

    {

        Debug.Log($"[XrayMeshissue] DeactivateXRay() called. _active was {_active}");

        if (!_active) return;

        if (_entryCoroutine != null) { StopCoroutine(_entryCoroutine); _entryCoroutine = null; }

        DestroyScanningRing();

        RestoreOriginals();

        EnableConfiguredParts();

        HandleExclusions(false);

        _active = false;

        _lockedPart = null;

    }



    public void ToggleXRay()

    {

        Debug.Log($"[XrayMeshissue] ToggleXRay() called. _active is currently {_active}");

        if (_active) DeactivateXRay();

        else         ActivateXRay();

    }



    /// <summary>

    /// Call from EngineInteractor when hovering a part in XRay mode.

    /// Pass null to clear hover and restore X-Ray view of all parts.

    /// Shows ONLY the hovered part with original mesh, hides all other parts.

    /// </summary>

    public void SetHoveredPart(Renderer hoveredRenderer)

    {

        if (!_active || _sharedMat == null) return;



        // ── Scanning ring lifecycle ───────────────────────────────────────────

        if (hoveredRenderer != null && hoveredRenderer != _lastHoveredRenderer)

        {

            DestroyScanningRing();

            SpawnScanningRing(hoveredRenderer);

        }

        else if (hoveredRenderer == null && _lastHoveredRenderer != null)

        {

            DestroyScanningRing();

        }

        _lastHoveredRenderer = hoveredRenderer;



        // Find the parent EnginePart component of the hovered renderer

        EnginePart hoveredPart = hoveredRenderer != null ? hoveredRenderer.GetComponentInParent<EnginePart>() : null;



        // ── Hover & Lock rendering logic ───────────────────────────────────────

        for (int i = 0; i < _renderers.Count; i++)

        {

            var rd = _renderers[i];

            if (rd.renderer == null) continue;



            rd.renderer.enabled = true;

            EnginePart parentPart = rd.renderer.GetComponentInParent<EnginePart>();



            if (parentPart != null && (parentPart == hoveredPart || parentPart == _lockedPart))

            {

                // Restore original materials (opaque/textured) for hovered or locked parts

                rd.renderer.sharedMaterials = rd.originalMaterials;

                Debug.Log($"[XrayMeshissue] SetHoveredPart (REVEAL ORIGINAL): GameObject='{rd.renderer.gameObject.name}' | Restored Saved Count={rd.originalMaterials.Length} | Current sharedMaterials.Length={rd.renderer.sharedMaterials.Length}");

            }

            else

            {

                // Keep other parts in X-Ray mode

                var instMat = _instanceMaterials[i];

                var mats = new Material[rd.originalMaterials.Length];

                for (int m = 0; m < mats.Length; m++) mats[m] = instMat;



                rd.renderer.sharedMaterials = mats;

                Debug.Log($"[XrayMeshissue] SetHoveredPart (XRAY MODE): GameObject='{rd.renderer.gameObject.name}' | Saved Count={rd.originalMaterials.Length} | Assigned Array Len={mats.Length} | Current sharedMaterials.Length={rd.renderer.sharedMaterials.Length}");

            }

        }

    }

    



    // ── Lifecycle ─────────────────────────────────────────────────────────────



    void Update()

    {

        if (!_active || _sharedMat == null) return;



        // Push inspector properties to the shared template

        PushProperties(_sharedMat, hoverIntensity: 0f);



        // Copy runtime sweep/glitch properties from shared template to all instance materials

        SyncToInstances();



        // Keep hover mat in sync too

        foreach (var kv in _hoverMat)

            if (kv.Value != null) PushProperties(kv.Value, hoverIntensity: 1f);

    }



    void OnDisable() => DeactivateXRay();



    // ── Entry sequence ────────────────────────────────────────────────────────



    IEnumerator EntrySequence()

    {

        float top    = _engineBounds.max.y + sweepBandWidth;

        float bottom = _engineBounds.min.y - sweepBandWidth;



        // ── Phase 1: Glitch flicker ───────────────────────────────────────────

        float glitchElapsed = 0f;

        while (glitchElapsed < glitchDuration)

        {

            glitchElapsed += Time.deltaTime;

            float t = glitchElapsed / glitchDuration;

            float glitch = Mathf.Lerp(1f, 0f, t);

            _sharedMat.SetFloat("_GlitchIntensity", glitch);

            _sharedMat.SetFloat("_SweepY", top + 10f);

            _sharedMat.SetFloat("_SweepProgress", 0f);

            SyncInstanceFloat("_GlitchIntensity", glitch);

            SyncInstanceFloat("_SweepY",           top + 10f);

            SyncInstanceFloat("_SweepProgress",    0f);

            yield return null;

        }

        _sharedMat.SetFloat("_GlitchIntensity", 0f);

        SyncInstanceFloat("_GlitchIntensity", 0f);



        // ── Phase 2: CT scan sweep top → bottom ───────────────────────────────

        float sweepElapsed = 0f;

        while (sweepElapsed < sweepDuration)

        {

            sweepElapsed += Time.deltaTime;

            float t      = Mathf.SmoothStep(0f, 1f, sweepElapsed / sweepDuration);

            float sweepY = Mathf.Lerp(top, bottom, t);



            _sharedMat.SetFloat("_SweepY",         sweepY);

            _sharedMat.SetFloat("_SweepProgress",  t);

            _sharedMat.SetFloat("_SweepIntensity", sweepIntensity * (1f - t * 0.5f));

            SyncInstanceFloat("_SweepY",           sweepY);

            SyncInstanceFloat("_SweepProgress",    t);

            SyncInstanceFloat("_SweepIntensity",   sweepIntensity * (1f - t * 0.5f));

            yield return null;

        }



        // ── Phase 3: Settle — sweep done, full engine visible ─────────────────

        _sharedMat.SetFloat("_SweepY",         bottom - 100f);

        _sharedMat.SetFloat("_SweepIntensity", 0f);

        _sharedMat.SetFloat("_SweepProgress",  1f);

        SyncInstanceFloat("_SweepY",           bottom - 100f);

        SyncInstanceFloat("_SweepIntensity",   0f);

        SyncInstanceFloat("_SweepProgress",    1f);

        _entryCoroutine = null;

    }



    // ── Internal helpers ──────────────────────────────────────────────────────



    void CollectRenderers()

    {

        _renderers.Clear();

        if (targetRoot == null)

        {

            Debug.LogWarning("[XrayMeshissue] CollectRenderers: targetRoot is NULL!");

            return;

        }



        Debug.Log($"[XrayMeshissue] CollectRenderers: Starting scan on targetRoot='{targetRoot.name}'");

        foreach (Renderer r in targetRoot.GetComponentsInChildren<Renderer>(true))

        {

            var exclusion = r.GetComponentInParent<ExcludeFromXRay>();

            if (exclusion != null && exclusion.mode == XRayExclusionMode.KeepOriginalMaterial)

            {

                Debug.Log($"[XrayMeshissue] CollectRenderers: Skipping excluded renderer on '{r.gameObject.name}'");

                continue;

            }



            var origMats = r.sharedMaterials;

            string matNames = string.Join(", ", origMats.Select(m => m != null ? m.name : "NULL"));

            Debug.Log($"[XrayMeshissue] CollectRenderers: Saved GameObject='{r.gameObject.name}' | SharedMats Count={origMats.Length} | Materials=[{matNames}]");



            _renderers.Add(new RendererData { renderer = r, originalMaterials = origMats });

        }

        Debug.Log($"[XrayMeshissue] CollectRenderers finished. Total renderers collected: {_renderers.Count}");

    }



    private void HandleExclusions(bool activate)

    {

        if (targetRoot == null) return;



        if (activate)

        {

            _hiddenExcludedObjects.Clear();

            foreach (var exclusion in targetRoot.GetComponentsInChildren<ExcludeFromXRay>(true))

            {

                if (exclusion.mode == XRayExclusionMode.HideGameObject && exclusion.gameObject.activeSelf)

                {

                    exclusion.gameObject.SetActive(false);

                    _hiddenExcludedObjects.Add(exclusion.gameObject);

                }

            }

        }

        else

        {

            foreach (var go in _hiddenExcludedObjects)

            {

                if (go != null) go.SetActive(true);

            }

            _hiddenExcludedObjects.Clear();

        }

    }



    void ComputeEngineBounds()

    {

        _engineBounds = new Bounds(targetRoot != null ? targetRoot.position : Vector3.zero, Vector3.zero);

        foreach (var rd in _renderers)

            if (rd.renderer != null) _engineBounds.Encapsulate(rd.renderer.bounds);

    }



    void CreateSharedMaterial()

    {

        if (xRayMaterial == null) { Debug.LogError("[XRayVision] No XRay material assigned!", this); return; }

        if (_sharedMat != null) Destroy(_sharedMat);

        _sharedMat = new Material(xRayMaterial);

        PushProperties(_sharedMat, hoverIntensity: 0f);

        // Shared template uses neutral per-part values — real per-part data lives on instance materials

        _sharedMat.SetFloat("_PartIntensity", 1f);

        _sharedMat.SetFloat("_PartAlpha",     1f);

        _sharedMat.SetColor("_PartTint",      Color.white);

    }



    void SwapToXRay()

    {

        if (_sharedMat == null) return;



        // Destroy any stale instance materials

        foreach (var m in _instanceMaterials)

            if (m != null) Destroy(m);

        _instanceMaterials.Clear();



        Debug.Log($"[XrayMeshissue] SwapToXRay: Swapping {_renderers.Count} renderers to X-Ray materials...");



        foreach (var rd in _renderers)

        {

            if (rd.renderer == null) continue;



            // Fix B: Create per-renderer material instance so each part can have its own _PartTint

            Material inst = new Material(_sharedMat);



            // Derive tint, glow intensity, and body alpha from EnginePartVisuals, defaulting to neutral

            var visuals = rd.renderer.GetComponentInParent<EnginePartVisuals>();

            Color partTint      = visuals != null ? visuals.xrayColor          : Color.white;

            float partIntensity = visuals != null ? visuals.xrayGlowIntensity  : 1f;

            float partAlpha     = visuals != null ? visuals.xrayAlpha          : 1f;



            // Ensure tint alpha is 1 so it doesn't dim the outline alpha

            partTint.a = 1f;

            inst.SetColor("_PartTint",        partTint);

            inst.SetFloat("_PartIntensity",   partIntensity);

            inst.SetFloat("_PartAlpha",       partAlpha);

            inst.SetFloat("_HoverIntensity",  0f);   // start with no hover pulse



            _instanceMaterials.Add(inst);



            int savedCount = rd.originalMaterials.Length;

            var mats = new Material[savedCount];

            for (int i = 0; i < mats.Length; i++) mats[i] = inst;



            rd.renderer.sharedMaterials = mats;



            Debug.Log($"[XrayMeshissue] SwapToXRay: GameObject='{rd.renderer.gameObject.name}' | Saved Count={savedCount} | Assigned Array Len={mats.Length} | Resulting sharedMaterials.Length={rd.renderer.sharedMaterials.Length} | Resulting materials.Length={rd.renderer.materials.Length}");



            // Apply sweep start state (hidden above sweep)

            inst.SetFloat("_SweepY",          _sharedMat.GetFloat("_SweepY"));

            inst.SetFloat("_SweepProgress",   _sharedMat.GetFloat("_SweepProgress"));

            inst.SetFloat("_SweepIntensity",  _sharedMat.GetFloat("_SweepIntensity"));

            inst.SetFloat("_GlitchIntensity", _sharedMat.GetFloat("_GlitchIntensity"));

        }

    }



    void RestoreOriginals()

    {

        Debug.Log($"[XrayMeshissue] RestoreOriginals: Restoring {_renderers.Count} renderers...");

        foreach (var rd in _renderers)

        {

            if (rd.renderer != null)

            {

                rd.renderer.enabled = true;  // Re-enable renderers that were hidden on hover

                int origCount = rd.originalMaterials != null ? rd.originalMaterials.Length : 0;

                string origNames = rd.originalMaterials != null ? string.Join(", ", rd.originalMaterials.Select(m => m != null ? m.name : "NULL")) : "NONE";



                rd.renderer.sharedMaterials = rd.originalMaterials;



                Debug.Log($"[XrayMeshissue] RestoreOriginals: GameObject='{rd.renderer.gameObject.name}' | Saved Count={origCount} [{origNames}] | Resulting sharedMaterials.Length={rd.renderer.sharedMaterials.Length} | Resulting materials.Length={rd.renderer.materials.Length}");

            }

        }



        foreach (var kv in _hoverMat)

            if (kv.Value != null) Destroy(kv.Value);

        _hoverMat.Clear();



        foreach (var m in _instanceMaterials)

            if (m != null) Destroy(m);

        _instanceMaterials.Clear();



        if (_sharedMat != null) { Destroy(_sharedMat); _sharedMat = null; }

        Debug.Log("[XrayMeshissue] RestoreOriginals: Cleanup finished.");

    }



    // ── Scanning ring helpers ─────────────────────────────────────────────────



    void SpawnScanningRing(Renderer targetRenderer)

    {

        if (scanningRingMaterial == null || targetRenderer == null) return;



        GameObject ringObj = new GameObject("XRayScanningRing");

        ringObj.transform.position = targetRenderer.bounds.center;



        ScanningRingController controller = ringObj.AddComponent<ScanningRingController>();

        controller.Initialize(scanningRingMaterial);

        controller.StartScan(targetRenderer);

        _currentScanningRing = controller;

    }



    void DestroyScanningRing()

    {

        if (_currentScanningRing != null)

        {

            _currentScanningRing.StopScan();

            _currentScanningRing = null;

        }

    }



    void PushProperties(Material mat, float hoverIntensity)

    {

        mat.SetColor("_RimColor",        rimColor);

        mat.SetColor("_DepthNearColor",  depthNearColor);

        mat.SetColor("_DepthFarColor",   depthFarColor);

        mat.SetColor("_CircuitColor",    circuitColor);

        mat.SetFloat("_RimPower",        rimPower);

        mat.SetFloat("_RimIntensity",    rimIntensity);

        mat.SetFloat("_BodyAlpha",       bodyAlpha);

        mat.SetFloat("_DepthRange",      depthRange);

        mat.SetFloat("_DepthInfluence",  depthInfluence);

        mat.SetFloat("_ScanSpeed",       scanSpeed);

        mat.SetFloat("_ScanDensity",     scanDensity);

        mat.SetFloat("_ScanLineWidth",   scanLineWidth);

        mat.SetFloat("_ScanBrightness",  scanBrightness);

        mat.SetFloat("_ScanAlpha",       scanAlpha);

        mat.SetFloat("_CircuitSpeed",    circuitSpeed);

        mat.SetFloat("_CircuitDensity",  circuitDensity);

        mat.SetFloat("_CircuitWidth",    circuitWidth);

        mat.SetFloat("_CircuitIntensity",circuitIntensity);

        mat.SetFloat("_SweepWidth",      sweepBandWidth);

        mat.SetFloat("_OutlineWidth",    outlineWidth);

        mat.SetFloat("_HoverIntensity",  hoverIntensity);

    }



    /// <summary>

    /// Copies the runtime sweep/glitch properties from _sharedMat to all instance materials.

    /// Called every frame and during the entry sequence.

    /// </summary>

    void SyncToInstances()

    {

        SyncInstanceFloat("_SweepY",          _sharedMat.GetFloat("_SweepY"));

        SyncInstanceFloat("_SweepProgress",   _sharedMat.GetFloat("_SweepProgress"));

        SyncInstanceFloat("_SweepIntensity",  _sharedMat.GetFloat("_SweepIntensity"));

        SyncInstanceFloat("_GlitchIntensity", _sharedMat.GetFloat("_GlitchIntensity"));

    }



    void SyncInstanceFloat(string propertyName, float value)

    {

        foreach (var m in _instanceMaterials)

            if (m != null) m.SetFloat(propertyName, value);

    }

    private void DisableConfiguredParts()

    {

        if (partsToDisable == null) return;

        foreach (var go in partsToDisable)

        {

            if (go != null && go.activeSelf) go.SetActive(false);

        }

    }



    private void EnableConfiguredParts()

    {

        if (partsToDisable == null) return;

        foreach (var go in partsToDisable)

        {

            if (go != null && !go.activeSelf) go.SetActive(true);

        }

    }



}



