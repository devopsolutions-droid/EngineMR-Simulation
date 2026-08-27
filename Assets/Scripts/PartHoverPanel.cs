using UnityEngine;

/// <summary>
/// Attach this to each engine part's dedicated hover panel (the one YOU design in Unity).
/// The panel starts hidden. EngineInteractor will call Show/Hide via EnginePart.
/// 
/// This script anchors the panel to follow a specific engine part:
/// - If partAnchor is assigned: panel follows that anchor point
/// - If partAnchor is NOT assigned: panel is parented to the engine part automatically
/// 
/// Line connects from this panel to the engine part anchor point.
/// It updates every LateUpdate so it stays correct even while the engine rotates/moves.
/// </summary>
public class PartHoverPanel : MonoBehaviour
{
    [Header("Part Anchor")]
    [Tooltip("Assign an empty child GameObject on the engine part as the anchor point. If empty, will auto-find from parent.")]
    public Transform partAnchor;

    [Header("Panel Offset")]
    [Tooltip("Offset from the part anchor where the panel should appear (in world space). Only used if useInitialPositionAsOffset is false.")]
    public Vector3 panelOffset = new Vector3(0.3f, 0.3f, 0f);

    [Tooltip("If true, the panel's position in the editor will be used to calculate its offset relative to the partAnchor at startup, ignoring the panelOffset field.")]
    public bool useInitialPositionAsOffset = true;

    private EnginePart _enginePart;
    private Vector3 _localOffset;
    private bool _hasLocalOffset = false;

    void Awake()
    {
        // Remove LineRenderer component if present to ensure no orange lines are drawn
        LineRenderer lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            Destroy(lr);
        }

        // Try to find the engine part this panel belongs to
        _enginePart = GetComponentInParent<EnginePart>();
        
        // If partAnchor is not assigned, try to find it on the engine part
        if (partAnchor == null && _enginePart != null)
        {
            // Look for a child named "PanelAnchor" or similar
            partAnchor = _enginePart.transform.Find("PanelAnchor");
            if (partAnchor == null)
            {
                // Fallback: use the engine part's transform itself
                partAnchor = _enginePart.transform;
            }
        }

        if (useInitialPositionAsOffset && partAnchor != null)
        {
            _localOffset = partAnchor.InverseTransformPoint(transform.position);
            _hasLocalOffset = true;
        }

    }

    void LateUpdate()
    {
        // If we have an engine part, follow it
        if (_enginePart != null && partAnchor != null)
        {
            if (_hasLocalOffset)
            {
                // Update panel position following the rotation and position of the anchor
                transform.position = partAnchor.TransformPoint(_localOffset);
            }
            else
            {
                // Update panel position to follow the part anchor + offset
                Vector3 targetPos = partAnchor.position + panelOffset;
                transform.position = targetPos;
            }
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
