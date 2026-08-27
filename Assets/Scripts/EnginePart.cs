using UnityEngine;

/// <summary>
/// Core identity of an engine part.
/// Holds data (name, description, audio, hover panel) and
/// caches references to sibling components for clean external access.
///
/// Callers use:
///   part.Visuals.SetHighlight(true)
///   part.Explode.AnimateToExploded(1.2f)
/// </summary>
public class EnginePart : MonoBehaviour
{
    [Header("Part Info")]
    [Tooltip("Assign a PartData asset to drive this part's name, description and audio. Overrides the fields below.")]
    public PartData partData;

    [Tooltip("Used only if partData is not assigned.")]
    public string partName = "Engine Part";
    [TextArea, Tooltip("Used only if partData is not assigned.")]
    public string description = "Part description here.";
    [Tooltip("Used only if partData is not assigned.")]
    public AudioClip audioExplanation;

    [Header("Hover Panel")]
    [Tooltip("Drag the pre-designed panel for THIS part here.")]
    public GameObject hoverPanel;

    [HideInInspector]
    public System.Collections.Generic.List<EnginePart> groupedParts;

    public System.Collections.Generic.List<EnginePart> GetGroupParts()
    {
        return (groupedParts != null && groupedParts.Count > 0) ? groupedParts : new System.Collections.Generic.List<EnginePart> { this };
    }

    // ── Sibling component accessors ───────────────────────────────────────────
    public EnginePartVisuals Visuals { get; private set; }
    public EnginePartExplode Explode { get; private set; }

    // ── Data accessors (prefer PartData SO if assigned) ───────────────────────
    public string    PartName    => (partData != null && !string.IsNullOrEmpty(partData.partName))         ? partData.partName         : partName;
    public string    Description => (partData != null && !string.IsNullOrEmpty(partData.description))      ? partData.description      : description;
    public AudioClip AudioClip   => (partData != null && partData.audioExplanation != null) ? partData.audioExplanation : audioExplanation;

    void Awake()
    {
        Visuals = GetComponent<EnginePartVisuals>();
        Explode = GetComponent<EnginePartExplode>();

        if (Visuals == null) Debug.LogError($"[EnginePart] '{gameObject.name}' is missing EnginePartVisuals component!");
        if (Explode == null) Debug.LogError($"[EnginePart] '{gameObject.name}' is missing EnginePartExplode component!");

        // Deactivate hover panel initially so it starts hidden
        if (hoverPanel != null)
        {
            hoverPanel.SetActive(false);
        }
    }

    // ── Hover Panel ───────────────────────────────────────────────────────────
    public void ShowPanel() { if (hoverPanel != null) hoverPanel.SetActive(true); }
    public void HidePanel() { if (hoverPanel != null) hoverPanel.SetActive(false); }

    // ── Visibility ────────────────────────────────────────────────────────────
    public void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    // ── Convenience pass-throughs so existing callers need zero changes ───────
    public void SetHighlight(bool on)       => Visuals?.SetHighlight(on);
    public void SetSelected()               => Visuals?.SetSelected();
    public void SetGhost(float alpha = -1f) => Visuals?.SetGhost(alpha);
    public void SetXRayView()               => Visuals?.SetXRayView();
    public void SetShowWorkingActive()      => Visuals?.SetShowWorkingActive();
    public void SetShowWorkingBackground()  => Visuals?.SetShowWorkingBackground();
    public void RestoreOriginal()           => Visuals?.RestoreOriginal();

    public void ComputeExplodeTarget(Vector3 center)    => Explode?.ComputeExplodeTarget(center);
    public void SetExplodeWorldTarget(Vector3 worldPos) => Explode?.SetExplodeWorldTarget(worldPos);
    public void SetExplodeLocalTarget(Vector3 localPos) => Explode?.SetExplodeLocalTarget(localPos);
    public void AnimateToExploded(float duration)       => Explode?.AnimateToExploded(duration);
    public void AnimateToAssembled(float duration)      => Explode?.AnimateToAssembled(duration);
    public void AnimateToCustomLocalPos(Vector3 localPos, float duration) => Explode?.AnimateToCustomPos(localPos, duration);
    public void AnimateToCustomWorldPos(Vector3 worldPos, float duration) => Explode?.AnimateToWorldPos(worldPos, duration);
    public void LiftUp(float amount, float duration)    => Explode?.LiftUp(amount, duration);
    public void LowerDown(float duration)               => Explode?.LowerDown(duration);

    // ── Expose explodeDistance so EngineViewManager can still set it ──────────
    public float explodeDistance
    {
        get => Explode != null ? Explode.explodeDistance : 0f;
        set { if (Explode != null) Explode.explodeDistance = value; }
    }
}
