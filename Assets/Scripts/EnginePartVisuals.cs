using UnityEngine;
using UnityEngine.Rendering;

public enum OutlineColorPreset
{
    Custom, Red, Orange, Yellow, LightGreen, Green, Cyan, Blue, Purple, White, Pink
}

/// <summary>
/// Handles all visual states for an engine part:
/// highlight (hover), selected, ghost, xray, restore, outline.
/// Requires EnginePartVisuals + EnginePartExplode on the same GameObject as EnginePart.
/// </summary>
[RequireComponent(typeof(EnginePart))]
public class EnginePartVisuals : MonoBehaviour
{
    [Header("Hover Highlight")]
    [Tooltip("Set to 0 for outline-only hover — no emissive tint on the mesh.")]
    public Color highlightColor     = new Color(1f, 0.6f, 0f, 1f);
    [Range(0f, 1f)] public float highlightIntensity = 0f;

    [Header("Selection Glow")]
    public Color glowColor          = new Color(0f, 0.8f, 1f, 1f);
    [Range(0f, 4f)] public float glowIntensity = 2.5f;

    [Header("Ghost")]
    [Range(0f, 1f)] public float ghostAlpha        = 0.2f;
    [Range(0f, 1f)] public float ghostFadeDuration = 0.25f;

    [Header("Show Working")]
    [Tooltip("Color of the active part pulse glow during Show Working (currently unused — active part uses white outline only).")]
    public Color showWorkingActiveColor  = new Color(0f, 1f, 1f, 1f);
    [Range(0f, 6f)] public float showWorkingGlowIntensity = 4f;
    [Tooltip("Alpha of background parts during Show Working (X-Ray style).")]
    [Range(0f, 1f)] public float showWorkingBgAlpha = 0.12f;
    [Tooltip("Color tint of background parts during Show Working. Set to white for neutral transparency.")]
    public Color showWorkingBgColor = new Color(1f, 1f, 1f, 1f);

    [Header("X-Ray View")]
    public Color xrayColor          = new Color(0f, 0.8f, 0.8f, 1f);
    [Range(0f, 1f)] public float xrayAlpha         = 0.3f;
    [Range(0f, 4f)] public float xrayGlowIntensity = 1.0f;

    [Header("Outline")]
    public OutlineColorPreset outlineColorPreset = OutlineColorPreset.Red;
    public Color outlineColor       = new Color(1f, 0.08f, 0.08f, 1f);
    [Range(1f, 10f)] public float outlineWidth   = 3.5f;

    [Header("Hover Animation")]
    [Tooltip("Duration of the smooth fade-in/out when hovering. Set to 0 for instant (legacy).")]
    [Range(0f, 1f)] public float hoverFadeDuration = 0.2f;
    [Tooltip("Speed of the sine-wave pulse on the outline width while hovering.")]
    [Range(0f, 8f)] public float pulseSpeed = 3f;
    [Tooltip("How much the outline width pulses (0 = no pulse, 1 = max amplitude).")]
    [Range(0f, 1f)] public float pulseIntensity = 0.25f;
    [Tooltip("Brightness multiplier on the outline color while hovering (makes it glow).")]
    [Range(1f, 5f)] public float hoverGlowIntensity = 1.5f;

    // ── Assembly wow-factor visuals ──────────────────────────────────────────
    [Header("Assembly Feedback")]
    [Tooltip("Alpha of assembled parts so focus stays on remaining ones.")]
    [Range(0f, 1f)] public float assembledDimAlpha = 0.35f;
    [Tooltip("Idle glow color on assembled parts.")]
    public Color assembledGlowColor = new Color(0.2f, 1f, 0.4f, 1f);
    [Range(0f, 2f)] public float assembledGlowIntensity = 0.6f;
    [Tooltip("Green burst color on correct snap.")]
    public Color snapSuccessColor = new Color(0.1f, 1f, 0.3f, 1f);
    [Range(0f, 4f)] public float snapSuccessIntensity = 3f;
    [Range(0.1f, 1f)] public float snapSuccessDuration = 0.4f;
    [Tooltip("Red shake color on wrong placement.")]
    public Color wrongPlacementColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Range(0f, 4f)] public float wrongPlacementIntensity = 0.5f;
    [Range(0.05f, 0.5f)] public float wrongPlacementShakeDuration = 0.2f;
    [Range(0.005f, 0.05f)] public float wrongPlacementShakeMagnitude = 0.002f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Renderer[] _renderers;
    private Material[] _materials;
    private Material[][] _originalMaterialsBackup;
    private Material   _outlineMat;
    private bool       _outlineActive;
    private bool       _initialised;
    private Coroutine  _ghostCoroutine;
    private Coroutine  _hoverCoroutine;
    private float      _currentHoverWidth;
    private EngineGrabManager _cachedGrabManager;

    public Color ActiveOutlineColor
    {
        get
        {
            if (EngineViewManager.IsGrabModeActive)
            {
                if (_cachedGrabManager == null)
                    _cachedGrabManager = FindFirstObjectByType<EngineGrabManager>();

                if (_cachedGrabManager != null && _cachedGrabManager.IsStepByStepActive)
                {
                    if (_cachedGrabManager.IsCurrentStepPart(GetComponent<EnginePart>()))
                    {
                        return new Color(0.10f, 0.85f, 0.20f); // Green outline for correct step part
                    }
                    else
                    {
                        return new Color(1.00f, 0.15f, 0.15f); // Red outline for incorrect step part
                    }
                }
            }

            switch (outlineColorPreset)
            {
                case OutlineColorPreset.Red:        return new Color(1.00f, 0.15f, 0.15f);
                case OutlineColorPreset.Orange:     return new Color(1.00f, 0.50f, 0.05f);
                case OutlineColorPreset.Yellow:     return new Color(1.00f, 0.92f, 0.10f);
                case OutlineColorPreset.LightGreen: return new Color(0.50f, 1.00f, 0.30f);
                case OutlineColorPreset.Green:      return new Color(0.10f, 0.85f, 0.20f);
                case OutlineColorPreset.Cyan:       return new Color(0.00f, 0.90f, 1.00f);
                case OutlineColorPreset.Blue:       return new Color(0.15f, 0.40f, 1.00f);
                case OutlineColorPreset.Purple:     return new Color(0.70f, 0.20f, 1.00f);
                case OutlineColorPreset.White:      return new Color(0.95f, 0.95f, 0.95f);
                case OutlineColorPreset.Pink:       return new Color(1.00f, 0.40f, 0.70f);
                default:                            return outlineColor;
            }
        }
    }

    void Awake() => Initialise();

    void OnDestroy()
    {
        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);
        if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
    }

    void Initialise()
    {
        if (_initialised) return;

        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers == null || _renderers.Length == 0)
        {
            Debug.LogWarning($"[EnginePartVisuals] '{gameObject.name}' has no renderers!");
            _initialised = true;
            return;
        }

        System.Collections.Generic.List<Material> allMats = new System.Collections.Generic.List<Material>();
        _originalMaterialsBackup = new Material[_renderers.Length][];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Material[] sharedMats = _renderers[i].sharedMaterials;
            _originalMaterialsBackup[i] = new Material[sharedMats.Length];
            for (int m = 0; m < sharedMats.Length; m++)
            {
                _originalMaterialsBackup[i][m] = sharedMats[m];
                if (sharedMats[m] != null) allMats.Add(sharedMats[m]);
            }
        }
        _materials = allMats.ToArray();

        var shader = Shader.Find("Custom/Outline");
        if (shader != null)
        {
            _outlineMat           = new Material(shader);
            _outlineMat.SetColor("_OutlineColor",    ActiveOutlineColor);
            _outlineMat.SetFloat("_OutlineWidth",     outlineWidth);
            _outlineMat.SetFloat("_PulseSpeed",       pulseSpeed);
            _outlineMat.SetFloat("_PulseIntensity",   pulseIntensity);
            _outlineMat.SetFloat("_GlowIntensity",    hoverGlowIntensity);
            _outlineMat.hideFlags = HideFlags.HideAndDontSave;
        }
        else
            Debug.LogError($"[EnginePartVisuals] '{gameObject.name}': Custom/Outline shader not found.");

        _currentHoverWidth = 0f;
        _initialised = true;
    }

    // ── Public visual state API ───────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        // Use animated fade when fade duration > 0 and the object is active in hierarchy
        if (hoverFadeDuration > 0.01f && gameObject.activeInHierarchy)
        {
            if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
            _hoverCoroutine = StartCoroutine(FadeHighlight(on));
            return;
        }

        // Legacy instant path (fadeDuration = 0 or object inactive)
        if (on)
        {
            if (_outlineMat != null)
            {
                _outlineMat.SetColor("_OutlineColor",    ActiveOutlineColor);
                _outlineMat.SetFloat("_OutlineWidth",     outlineWidth);
                _outlineMat.SetFloat("_PulseSpeed",       pulseSpeed);
                _outlineMat.SetFloat("_PulseIntensity",   pulseIntensity);
                _outlineMat.SetFloat("_GlowIntensity",    hoverGlowIntensity);
            }
            if (highlightIntensity > 0f)
                foreach (var mat in _materials) { if (mat != null) ApplyGlow(mat, highlightColor, highlightIntensity); }
            ShowOutline();
        }
        else
        {
            ClearEmissionOnRenderers();
            HideOutline();
        }
    }

    public void SetSelected()
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;
            _renderers[i].enabled  = true;
            _renderers[i].sharedMaterials = _originalMaterialsBackup[i];
        }
        ShowOutline();
    }

    public void SetGhost(float overrideAlpha = -1f)
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        HideOutline();

        float targetAlpha = overrideAlpha >= 0f ? overrideAlpha : ghostAlpha;

        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);
        if (ghostFadeDuration > 0.01f && gameObject.activeInHierarchy)
            _ghostCoroutine = StartCoroutine(FadeToGhost(targetAlpha));
        else
            ApplyGhostImmediate(targetAlpha);
    }

    /// <summary>
    /// Active part during Show Working — full opacity with original materials so airflow
    /// tube visuals remain clearly visible. Uses a white outline as the sole indicator
    /// instead of a colored emissive glow that would hide the airflow effect underneath.
    /// </summary>
    public void SetShowWorkingActive()
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        if (_ghostCoroutine != null) { StopCoroutine(_ghostCoroutine); _ghostCoroutine = null; }

        // Restore full opacity original materials — no colored glow, preserve original look
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;
            _renderers[i].enabled   = true;
            _renderers[i].sharedMaterials = _originalMaterialsBackup[i];
        }

        // Clear any baked-in emission so original material colours show through cleanly
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            ClearEmission(mat);
        }

        // White outline — clearly marks the active part without hiding the airflow tubes
        if (_outlineMat != null)
        {
            _outlineMat.SetColor("_OutlineColor", Color.white);
            _outlineMat.SetFloat("_OutlineWidth",  outlineWidth * 1.5f);
        }
        ShowOutline();
    }

    /// <summary>
    /// Background parts during Show Working — semi-transparent with neutral tint so the
    /// airflow tube visuals glow through clearly. Uses the dedicated showWorkingBg* fields
    /// instead of xrayColor to avoid the blue tint that hides the airflow colours.
    /// </summary>
    public void SetShowWorkingBackground()
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        HideOutline();
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);

            Color baseCol     = new Color(showWorkingBgColor.r, showWorkingBgColor.g, showWorkingBgColor.b, showWorkingBgAlpha);
            Color emissiveCol = showWorkingBgColor * 0.3f; // very subtle glow, just enough to read the part

            if (mat.HasProperty("_BaseColorFactor")) mat.SetColor("_BaseColorFactor", baseCol);
            else if (mat.HasProperty("_Color"))      mat.SetColor("_Color", baseCol);
            else                                     mat.color = baseCol;

            if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor", emissiveCol);
            if (mat.HasProperty("_EmissionColor"))   { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emissiveCol); }
            if (mat.HasProperty("_EmissiveColor"))   mat.SetColor("_EmissiveColor", emissiveCol);
        }
    }

    public void SetXRayView()
    {
        if (!_initialised) Initialise();
        if (_renderers == null || _renderers.Length == 0) return;

        HideOutline();
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            SetTransparent(mat);

            Color baseCol     = new Color(xrayColor.r, xrayColor.g, xrayColor.b, xrayAlpha);
            Color emissiveCol = xrayColor * xrayGlowIntensity;

            if (mat.HasProperty("_BaseColorFactor")) mat.SetColor("_BaseColorFactor", baseCol);
            else if (mat.HasProperty("_Color"))      mat.SetColor("_Color", baseCol);
            else                                     mat.color = baseCol;

            if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor", emissiveCol);
            if (mat.HasProperty("_EmissionColor"))   { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emissiveCol); }
            if (mat.HasProperty("_EmissiveColor"))   mat.SetColor("_EmissiveColor", emissiveCol);
        }
    }

    public void RestoreOriginal()
    {
        if (!_initialised) Initialise();
        if (_ghostCoroutine != null) { StopCoroutine(_ghostCoroutine); _ghostCoroutine = null; }

        _outlineActive = false;
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _originalMaterialsBackup[i] == null) continue;
            _renderers[i].enabled   = true;
            _renderers[i].sharedMaterials = _originalMaterialsBackup[i];
        }
    }

    /// <summary>Dim the part and add a subtle idle glow — called when this part is snapped into place.</summary>
    public void SetAssembled()
    {
        if (!_initialised) Initialise();
        HideOutline();
        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);
        _ghostCoroutine = StartCoroutine(AssembledEffect());
    }

    System.Collections.IEnumerator AssembledEffect()
    {
        // Fade to dim alpha
        foreach (var mat in _materials) { if (mat != null) SetTransparent(mat); }
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(1f, assembledDimAlpha, elapsed / 0.3f));
            yield return null;
        }
        SetAlpha(assembledDimAlpha);

        // Idle glow pulse forever
        while (true)
        {
            float glow = assembledGlowIntensity * (0.6f + 0.4f * Mathf.Sin(Time.time * 2f));
            foreach (var mat in _materials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_EmissionColor"))  { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",  assembledGlowColor * glow); }
                if (mat.HasProperty("_EmissiveFactor")) mat.SetColor("_EmissiveFactor", assembledGlowColor * glow);
                if (mat.HasProperty("_EmissiveColor"))  mat.SetColor("_EmissiveColor",  assembledGlowColor * glow);
            }
            yield return null;
        }
    }

    /// <summary>Green burst flash on correct snap.</summary>
    public void FlashSnapSuccess()
    {
        if (!_initialised) Initialise();
        if (gameObject.activeInHierarchy)
            StartCoroutine(FlashColor(snapSuccessColor, snapSuccessIntensity, snapSuccessDuration));
    }

    /// <summary>Red shake + flash on wrong placement.</summary>
    public void ShakeWrongPlacement()
    {
        if (!_initialised) Initialise();
        if (gameObject.activeInHierarchy)
            StartCoroutine(ShakeAndFlash());
    }

    System.Collections.IEnumerator FlashColor(Color color, float intensity, float duration)
    {
        float half = duration * 0.5f;
        // Flash in
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            foreach (var mat in _materials) { if (mat != null) ApplyGlow(mat, color, intensity * t); }
            yield return null;
        }
        // Flash out
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = 1f - elapsed / half;
            foreach (var mat in _materials) { if (mat != null) ApplyGlow(mat, color, intensity * t); }
            yield return null;
        }
        ClearEmissionOnRenderers();
    }

    System.Collections.IEnumerator ShakeAndFlash()
    {
        Vector3 origin  = transform.localPosition;
        float   elapsed = 0f;
        while (elapsed < wrongPlacementShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / wrongPlacementShakeDuration;
            // Shake decays over time
            float mag = wrongPlacementShakeMagnitude * (1f - t);
            transform.localPosition = origin + (Vector3)UnityEngine.Random.insideUnitCircle * mag;
            // Red flash
            foreach (var mat in _materials) { if (mat != null) ApplyGlow(mat, wrongPlacementColor, wrongPlacementIntensity * (1f - t)); }
            yield return null;
        }
        transform.localPosition = origin;
        ClearEmissionOnRenderers();
    }

    // ── Outline ───────────────────────────────────────────────────────────────

    void ShowOutline()
    {
        if (_outlineMat == null)
        {
            var shader = Shader.Find("Custom/Outline");
            if (shader == null) { Debug.LogError($"[EnginePartVisuals] Outline shader not found on '{gameObject.name}'"); return; }
            _outlineMat           = new Material(shader);
            _outlineMat.SetColor("_OutlineColor",    ActiveOutlineColor);
            _outlineMat.SetFloat("_OutlineWidth",     outlineWidth);
            _outlineMat.SetFloat("_PulseSpeed",       pulseSpeed);
            _outlineMat.SetFloat("_PulseIntensity",   pulseIntensity);
            _outlineMat.SetFloat("_GlowIntensity",    hoverGlowIntensity);
            _outlineMat.hideFlags = HideFlags.HideAndDontSave;
        }

        if (_outlineActive || _renderers == null) return;
        _outlineActive = true;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var current = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            bool has = false;
            foreach (var m in current) if (m != null && m.shader == _outlineMat.shader) { has = true; break; }
            if (has) continue;

            var extended = new Material[current.Length + 1];
            current.CopyTo(extended, 0);
            extended[extended.Length - 1] = _outlineMat;
            _renderers[i].materials = extended;
        }
    }

    void PushPulseProperties()
    {
        if (_outlineMat == null) return;
        _outlineMat.SetColor("_OutlineColor",    ActiveOutlineColor);
        _outlineMat.SetFloat("_PulseSpeed",       pulseSpeed);
        _outlineMat.SetFloat("_PulseIntensity",   pulseIntensity);
        _outlineMat.SetFloat("_GlowIntensity",    hoverGlowIntensity);
    }

    // ── Animated hover fade ──────────────────────────────────────────────────

    /// <summary>
    /// Smoothly fades the outline width from 0 → target (hover on)
    /// or target → 0 (hover off) using an ease-out curve.
    /// The shader's own pulse modulation runs on top of the base width.
    /// </summary>
    System.Collections.IEnumerator FadeHighlight(bool on)
    {
        float targetWidth = on ? outlineWidth : 0f;
        float startWidth  = _currentHoverWidth;
        float elapsed     = 0f;

        // On hover enter, set up the outline material + emission before fading in
        if (on)
        {
            if (_outlineMat != null)
            {
                _outlineMat.SetColor("_OutlineColor",    ActiveOutlineColor);
                _outlineMat.SetFloat("_PulseSpeed",       pulseSpeed);
                _outlineMat.SetFloat("_PulseIntensity",   pulseIntensity);
                _outlineMat.SetFloat("_GlowIntensity",    hoverGlowIntensity);
            }
            if (highlightIntensity > 0f)
                foreach (var mat in _materials) { if (mat != null) ApplyGlow(mat, highlightColor, highlightIntensity); }
            ShowOutline();
        }

        while (elapsed < hoverFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / hoverFadeDuration;
            // Ease-out quadratic: smooth deceleration
            float eased = t * (2f - t);
            _currentHoverWidth = Mathf.Lerp(startWidth, targetWidth, eased);

            if (_outlineMat != null)
                _outlineMat.SetFloat("_OutlineWidth", _currentHoverWidth);

            yield return null;
        }

        _currentHoverWidth = targetWidth;
        if (_outlineMat != null)
            _outlineMat.SetFloat("_OutlineWidth", _currentHoverWidth);

        // On hover exit, clean up after the fade-out completes
        if (!on)
        {
            ClearEmissionOnRenderers();
            HideOutline();
        }

        _hoverCoroutine = null;
    }

    void ClearEmissionOnRenderers()
    {
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            if (mat.HasProperty("_EmissiveFactor")) mat.SetColor("_EmissiveFactor", Color.black);
            if (mat.HasProperty("_EmissionColor"))  { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }
            if (mat.HasProperty("_EmissiveColor"))  mat.SetColor("_EmissiveColor", Color.black);
        }
    }

    void HideOutline()
    {
        if (!_outlineActive || _renderers == null) return;
        _outlineActive = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var current = _renderers[i].materials;
            if (current == null || current.Length == 0) continue;

            var trimmed = new System.Collections.Generic.List<Material>();
            foreach (var m in current)
                if (m == null || _outlineMat == null || m.shader != _outlineMat.shader)
                    trimmed.Add(m);

            _renderers[i].materials = trimmed.ToArray();
        }
    }

    // ── Ghost helpers ─────────────────────────────────────────────────────────

    System.Collections.IEnumerator FadeToGhost(float targetAlpha)
    {
        foreach (var mat in _materials) { if (mat != null) { SetTransparent(mat); ClearEmission(mat); } }

        float elapsed = 0f;
        while (elapsed < ghostFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(1f, targetAlpha, elapsed / ghostFadeDuration));
            yield return null;
        }
        SetAlpha(targetAlpha);
        _ghostCoroutine = null;
    }

    void ApplyGhostImmediate(float targetAlpha)
    {
        foreach (var mat in _materials) { if (mat != null) { SetTransparent(mat); ClearEmission(mat); } }
        SetAlpha(targetAlpha);
    }

    void SetAlpha(float alpha)
    {
        foreach (var mat in _materials)
        {
            if (mat == null) continue;
            if (mat.HasProperty("_BaseColorFactor")) { var c = mat.GetColor("_BaseColorFactor"); mat.SetColor("_BaseColorFactor", new Color(c.r, c.g, c.b, alpha)); }
            if (mat.HasProperty("_BaseColor"))       { var c = mat.GetColor("_BaseColor");       mat.SetColor("_BaseColor",       new Color(c.r, c.g, c.b, alpha)); }
            else if (mat.HasProperty("_Color"))      { var c = mat.GetColor("_Color");           mat.SetColor("_Color",           new Color(c.r, c.g, c.b, alpha)); }
            else                                     { var c = mat.color; mat.color = new Color(c.r, c.g, c.b, alpha); }
        }
    }

    // ── Static material helpers ───────────────────────────────────────────────

    static void ApplyGlow(Material mat, Color color, float intensity)
    {
        if (mat.HasProperty("_BaseColorFactor")) mat.SetColor("_BaseColorFactor", new Color(color.r, color.g, color.b, 1f));
        if (mat.HasProperty("_EmissiveFactor"))  mat.SetColor("_EmissiveFactor",  color * intensity);
        if (mat.HasProperty("_EmissionColor"))   { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * intensity); }
        if (mat.HasProperty("_EmissiveColor"))   mat.SetColor("_EmissiveColor",   color * intensity);
    }

    static void ClearEmission(Material mat)
    {
        if (mat.HasProperty("_EmissiveFactor")) mat.SetColor("_EmissiveFactor", Color.black);
        if (mat.HasProperty("_EmissionColor"))  { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }
        if (mat.HasProperty("_EmissiveColor"))  mat.SetColor("_EmissiveColor",  Color.black);
    }

    static void SetTransparent(Material mat)
    {
        if (mat.HasProperty("_AlphaMode"))
        {
            mat.SetFloat("_AlphaMode", 1);
            mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.SetInt("_AlphaClip", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            return;
        }
        mat.SetFloat("_Mode", 2);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",   0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
