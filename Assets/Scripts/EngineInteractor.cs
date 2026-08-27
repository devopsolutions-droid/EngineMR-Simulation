using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class EngineInteractor : MonoBehaviour
{
    [Header("References")]
    public XRRayInteractor rayInteractor;
    public PartInfoPanel infoPanel;
    public XRayVisionController xRayController;
    public EngineInspectionConfig inspectionConfig;
    public EngineInspectionSettings inspectionSettings;

    [Header("Input")]
    public InputActionReference selectAction;
    public InputActionReference moveAction;

    [Header("Layer")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Movement Detection")]
    public float moveThreshold = 0.1f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<EnginePart> OnPartSelected;
    public event Action<EnginePart> OnPartHovered;

    public bool HasActivePart => _activePart != null || _inspectedPart != null || (EngineViewManager.IsXRayActive && xRayController != null && xRayController.LockedPart != null);

    public bool InteractionEnabled { get; private set; } = false;

    public void EnableInteraction()
    {
        InteractionEnabled = true;
        Debug.Log("[EngineInteractor] Interaction ENABLED.");
    }

    public void DisableInteraction()
    {
        InteractionEnabled = false;
        ClearHover();
        bool changed = false;
        if (_activePart != null)
        {
            _activePart.HidePanel();
            _activePart = null;
            changed = true;
        }
        if (_inspectedPart != null)
        {
            _inspectedPart.HidePanel();
            _inspectedPart = null;
            changed = true;
        }
        if (changed)
        {
            OnPartSelected?.Invoke(null);
        }
        Debug.Log("[EngineInteractor] Interaction DISABLED.");
    }

    private static readonly Color HoverLineColor = new Color(1f, 0.65f, 0.3f);
    private XRInteractorLineVisual _lineVisual;
    private Gradient _hoverLineColorGradient;
    private Gradient _whiteToBlueGradient;
    private EnginePart _stablePart;
    private EnginePart _pendingPart;
    private EnginePart _activePart;
    private EnginePart _inspectedPart;
    private EnginePart[] _allParts;
    private AudioSource _audioSource;
    private float _hoverChangeTime = -1f;
    private float _lastSelectTime  = -1f;
    private const float HoverDebounce  = 0.08f;
    private const float SelectCooldown = 0.4f;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (selectAction == null) { Debug.LogError("[EngineInteractor] selectAction is NOT assigned!"); return; }
        selectAction.action.performed += OnSelect;
        selectAction.action.Enable();
        moveAction?.action.Enable();
    }

    void OnDisable()
    {
        if (selectAction == null) return;
        selectAction.action.performed -= OnSelect;
        moveAction?.action.Disable();
    }

    void Start()
    {
        if (rayInteractor == null) Debug.LogError("[EngineInteractor] rayInteractor is NOT assigned!");
        if (infoPanel == null)     Debug.LogError("[EngineInteractor] infoPanel is NOT assigned!");

        // Auto-assign missing XRayVisionController programmatically to avoid inspector reference errors
        if (xRayController == null)
        {
            xRayController = FindFirstObjectByType<XRayVisionController>();
            if (xRayController != null)
                Debug.Log("[EngineInteractor] Auto-assigned missing xRayController reference programmatically!");
            else
                Debug.LogWarning("[EngineInteractor] XRayVisionController could not be found in the scene.");
        }

#if UNITY_EDITOR
        // Auto-assign inspectionConfig in Editor if unassigned
        if (inspectionConfig == null)
        {
            inspectionConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<EngineInspectionConfig>("Assets/ScriptableObjects/EngineInspectionConfig.asset");
            if (inspectionConfig != null)
                Debug.Log("[EngineInteractor] Auto-assigned inspectionConfig programmatically from AssetDatabase.");
        }
#endif

        // Auto-assign inspectionSettings if attached to the same GameObject or anywhere in the scene
        if (inspectionSettings == null)
        {
            inspectionSettings = GetComponent<EngineInspectionSettings>();
            if (inspectionSettings == null)
            {
                inspectionSettings = FindFirstObjectByType<EngineInspectionSettings>();
                if (inspectionSettings != null)
                {
                    Debug.Log("[EngineInteractor] Auto-assigned EngineInspectionSettings component found in the scene.");
                }
            }
        }

        if (rayInteractor != null)
            _lineVisual = rayInteractor.GetComponent<XRInteractorLineVisual>();

        var whiteToBlue = new Gradient();
        whiteToBlue.SetKeys(
            new[] {
                new GradientColorKey(Color.white,                  0f),
                new GradientColorKey(new Color(0f, 0.2f, 1f),     0.5f),
                new GradientColorKey(new Color(0f, 0f, 0.6f),     1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        _whiteToBlueGradient = whiteToBlue;

        if (_lineVisual != null)
        {
            _lineVisual.invalidColorGradient = _whiteToBlueGradient;
            _lineVisual.validColorGradient   = _whiteToBlueGradient;
        }

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(HoverLineColor, 0f), new GradientColorKey(HoverLineColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        _hoverLineColorGradient = g;
    }

    public void RefreshParts()
    {
        _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Debug.Log($"[EngineInteractor] RefreshParts — found {_allParts.Length} EngineParts.");
    }

    private bool IsMoving()
    {
        if (moveAction == null || moveAction.action == null) return false;
        try   { return moveAction.action.ReadValue<Vector2>().magnitude > moveThreshold; }
        catch { return moveAction.action.ReadValue<float>() > moveThreshold; }
    }

    void Update()
    {
        if (!InteractionEnabled) return;

        if (!EngineViewManager.IsExplodedActive && _inspectedPart != null)
        {
            _inspectedPart.HidePanel();
            _inspectedPart = null;
            OnPartSelected?.Invoke(null);
        }

        if (_activePart != null || IsMoving()) { ClearHover(); return; }

        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
        {
            ClearHover();
            return;
        }

        // ── X-Ray mode: smart raycast penetration ─────────────────────────────
        if (EngineViewManager.IsXRayActive)
        {
            UpdateXRayHover();
            return;
        }

        // ── Normal mode: standard single-hit raycast ──────────────────────────
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            (enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
        {
            var part = hit.collider.GetComponentInParent<EnginePart>();

            if (part != _pendingPart)
            {
                _pendingPart     = part;
                _hoverChangeTime = Time.time;
            }

            if (_pendingPart != _stablePart && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                if (_stablePart != null)
                {
                    foreach (var p in _stablePart.GetGroupParts()) if (p != null) p.SetHighlight(false);
                }
                _stablePart?.HidePanel();
                _stablePart = _pendingPart;
                if (_stablePart != null)
                {
                    foreach (var p in _stablePart.GetGroupParts()) if (p != null) p.SetHighlight(true);
                }
                SetLineColor(_stablePart != null);

                if (_stablePart != null)
                {
                    _stablePart.ShowPanel();
                    Debug.Log($"[EngineInteractor] ShowPanel called on '{_stablePart.gameObject.name}' | " +
                              $"hoverPanel={(_stablePart.hoverPanel != null ? _stablePart.hoverPanel.name + " → SetActive(true)" : "NULL ← panel not assigned!")}");
                }
                else
                    _pendingPart?.HidePanel();

                OnPartHovered?.Invoke(_stablePart);
            }
        }
        else
        {
            if (_pendingPart != null)
            {
                _pendingPart     = null;
                _hoverChangeTime = Time.time;
            }

            if (_stablePart != null && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                ClearHover();
                OnPartHovered?.Invoke(null);
            }
        }
    }

    void ClearHover()
    {
        if (_stablePart != null)
        {
            // Only clear the standard outline highlight in non-XRay mode.
            // In XRay mode, the XRayVisionController handles its own hover visuals.
            if (!EngineViewManager.IsXRayActive)
            {
                foreach (var p in _stablePart.GetGroupParts()) if (p != null) p.SetHighlight(false);
            }

            if (_stablePart != _activePart && _stablePart != _inspectedPart)
                _stablePart.HidePanel();

            // Clear xray hover highlight
            if (EngineViewManager.IsXRayActive && xRayController != null)
                xRayController.SetHoveredPart(null);

            _stablePart = null;
            SetLineColor(false);
        }
        _pendingPart = null;
    }

    void SetLineColor(bool hovering)
    {
        if (_lineVisual == null) return;
        var g = hovering ? _hoverLineColorGradient : _whiteToBlueGradient;
        _lineVisual.invalidColorGradient = g;
        _lineVisual.validColorGradient   = g;
    }

    /// <summary>
    /// X-Ray mode hover: uses RaycastAll for collider penetration.
    /// Finds the nearest EnginePart behind exterior colliders.
    /// </summary>
    void UpdateXRayHover()
    {
        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
        {
            ClearHover();
            return;
        }

        Ray ray = new Ray(rayInteractor.rayOriginTransform.position, rayInteractor.rayOriginTransform.forward);

        // RaycastAll to penetrate exterior colliders and reach interior parts
        RaycastHit[] hits = Physics.RaycastAll(ray, 50f, enginePartsLayer);

        if (hits.Length > 0)
        {
            // Sort by distance to find the nearest EnginePart
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            EnginePart closestPart = null;
            foreach (var h in hits)
            {
                var p = h.collider.GetComponentInParent<EnginePart>();
                if (p != null)
                {
                    closestPart = p;
                    break;
                }
            }

            if (closestPart != _pendingPart)
            {
                _pendingPart     = closestPart;
                _hoverChangeTime = Time.time;
            }

            if (_pendingPart != _stablePart && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                // Do NOT call SetHighlight in X-Ray mode — XRayVisionController handles hover visuals
                _stablePart?.HidePanel();
                _stablePart = _pendingPart;
                SetLineColor(_stablePart != null);

                if (_stablePart != null)
                {
                    _stablePart.ShowPanel();
                    Debug.Log($"[EngineInteractor] XRay hover: '{_stablePart.gameObject.name}'");

                    // XRay hover highlight
                    if (xRayController != null)
                        xRayController.SetHoveredPart(_stablePart.GetComponentInChildren<Renderer>());
                }

                OnPartHovered?.Invoke(_stablePart);
            }
        }
        else
        {
            if (_pendingPart != null)
            {
                _pendingPart     = null;
                _hoverChangeTime = Time.time;
            }

            if (_stablePart != null && Time.time - _hoverChangeTime >= HoverDebounce)
            {
                ClearHover();
                OnPartHovered?.Invoke(null);
            }
        }
    }

    EnginePart GetCurrentRaycastPart()
    {
        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
            return null;
        if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            return null;
        if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) == 0)
            return null;
        return hit.collider.GetComponentInParent<EnginePart>();
    }

    void OnSelect(InputAction.CallbackContext ctx)
    {
        if (!InteractionEnabled) return;
        if (Time.time - _lastSelectTime < SelectCooldown) return;
        _lastSelectTime = Time.time;

        // Grab Mode / Show Working: block selection, hover still works
        if (EngineViewManager.IsGrabModeActive)    return;
        if (EngineViewManager.IsShowWorkingActive) return;

        // Special path for X-Ray mode: toggle lock on the hovered part
        if (EngineViewManager.IsXRayActive)
        {
            if (xRayController != null)
            {
                EnginePart currentLocked = xRayController.LockedPart;
                if (currentLocked != null)
                {
                    if (_stablePart == null || _stablePart == currentLocked)
                    {
                        // Unlock current part
                        xRayController.LockedPart = null;
                        OnPartSelected?.Invoke(null);
                        Debug.Log("[EngineInteractor] X-Ray lock cleared.");
                    }
                    else
                    {
                        // Lock a new part
                        xRayController.LockedPart = _stablePart;
                        OnPartSelected?.Invoke(_stablePart);
                        Debug.Log($"[EngineInteractor] X-Ray locked to: {_stablePart.gameObject.name}");
                    }
                }
                else
                {
                    if (_stablePart != null)
                    {
                        // Lock part
                        xRayController.LockedPart = _stablePart;
                        OnPartSelected?.Invoke(_stablePart);
                        Debug.Log($"[EngineInteractor] X-Ray locked to: {_stablePart.gameObject.name}");
                    }
                }
            }
            return;
        }

        if (rayInteractor == null || !rayInteractor.gameObject.activeInHierarchy || !rayInteractor.enabled)
            return;

        // ── Exploded View: audio + info + inspection animation ────────────────
        if (EngineViewManager.IsExplodedActive)
        {
            EnginePart target = GetCurrentRaycastPart();
            if (target == null) 
            {
                // Clicked background: send inspected part back
                if (_inspectedPart != null)
                {
                    _inspectedPart.HidePanel();
                    _inspectedPart.AnimateToExploded(0.5f);
                    _inspectedPart = null;
                    OnPartSelected?.Invoke(null);
                }
                return;
            }

            // Clicked the same part: toggle it back
            if (_inspectedPart == target)
            {
                _inspectedPart.HidePanel();
                _inspectedPart.AnimateToExploded(0.5f);
                _inspectedPart = null;
                _audioSource.Stop();
                OnPartSelected?.Invoke(null);
                return;
            }

            // Return previously inspected part
            if (_inspectedPart != null)
            {
                _inspectedPart.HidePanel();
                _inspectedPart.AnimateToExploded(0.5f);
            }

            ClearHover(); // Clear hover state so we don't have stale hover states when inspecting a new part
            _inspectedPart = target;
            
            // Get the target world-space position and animate using world position directly to bypass parenting/scaling issues
            Vector3 worldInspectPos = GetInspectionWorldPosition(_inspectedPart);
            _inspectedPart.AnimateToCustomWorldPos(worldInspectPos, 0.5f);
            _inspectedPart.ShowPanel();

            _audioSource.Stop();
            if (target.AudioClip != null)
            {
                _audioSource.clip = target.AudioClip;
                _audioSource.Play();
            }

            infoPanel.Show(target);
            OnPartSelected?.Invoke(target);
            Debug.Log($"[EngineInteractor] Exploded inspect & audio: {target.PartName}");
            return;
        }

        // ── Normal mode: toggle isolation ─────────────────────────────────────
        if (_activePart != null)
        {
            _activePart.HidePanel();
            _activePart = null;
            _audioSource.Stop();
            foreach (var p in _allParts) p.RestoreOriginal();
            infoPanel.Hide();
            OnPartSelected?.Invoke(null);
            return;
        }

        EnginePart selected = GetCurrentRaycastPart();
        if (selected == null)
        {
            Debug.LogWarning("[EngineInteractor] Ray is not on an engine part — ignoring select");
            return;
        }

        _activePart = selected;
        var activeGroup = _activePart.GetGroupParts();

        if (_allParts == null || _allParts.Length == 0)
        {
            _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Debug.LogWarning($"[EngineInteractor] _allParts was empty at select-time — re-scanned, found {_allParts.Length}.");
        }

        foreach (var p in _allParts)
        {
            if (activeGroup.Contains(p)) 
            { 
                p.SetSelected(); 
                if (p == _activePart) p.ShowPanel(); 
            }
            else                  
            { 
                p.SetGhost();    
                p.HidePanel(); 
            }
        }

        _audioSource.Stop();
        if (_activePart.AudioClip != null)
        {
            _audioSource.clip = _activePart.AudioClip;
            _audioSource.Play();
        }

        infoPanel.Show(_activePart);
        OnPartSelected?.Invoke(_activePart);
        Debug.Log($"[EngineInteractor] Isolated: {_activePart.PartName}");
    }

    private Vector3 GetInspectionWorldPosition(EnginePart part)
    {
        Vector3 defaultLocalPos = new Vector3(0.037f, 0.113f, 0.432f);
        
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader == null || loader.ActiveEngineData == null)
        {
            // Default fallback relative to EngineInteractor's own transform
            return transform.TransformPoint(defaultLocalPos);
        }

        // 1. Check custom MonoBehaviour settings overrides first (precedence)
        if (inspectionSettings != null)
        {
            Vector3? overridePos = inspectionSettings.GetOverridePosition(loader.ActiveEngineData);
            if (overridePos.HasValue)
            {
                // Convert overridePos relative to the part's immediate parent instead of ActiveEngineRoot
                // This guarantees that whatever local coordinates are copied from the Inspector will be strictly followed.
                Transform parent = part.transform.parent;
                return parent != null ? parent.TransformPoint(overridePos.Value) : overridePos.Value;
            }
        }

        // 2. Check ScriptableObject registry config second (fallback)
        if (inspectionConfig != null && inspectionConfig.engineConfigs != null)
        {
            foreach (var entry in inspectionConfig.engineConfigs)
            {
                if (entry != null && entry.engineData != null)
                {
                    // Match by reference or name to prevent SO reference mismatch
                    if (entry.engineData == loader.ActiveEngineData ||
                        (entry.engineData.engineName != null && loader.ActiveEngineData.engineName != null &&
                         entry.engineData.engineName.Equals(loader.ActiveEngineData.engineName, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        // Convert registry local coordinate to world space relative to the active engine root
                        return loader.ActiveEngineRoot != null
                            ? loader.ActiveEngineRoot.transform.TransformPoint(entry.inspectionLocalPosition)
                            : transform.TransformPoint(entry.inspectionLocalPosition);
                    }
                }
            }
        }

        return transform.TransformPoint(defaultLocalPos);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (inspectionConfig == null)
        {
            inspectionConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<EngineInspectionConfig>("Assets/ScriptableObjects/EngineInspectionConfig.asset");
            if (inspectionConfig != null)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
    }
#endif
}
