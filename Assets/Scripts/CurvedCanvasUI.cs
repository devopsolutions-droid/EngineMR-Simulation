using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Bends a World-Space Canvas into a curved display — like Samsung curved monitors.
///
/// HOW IT WORKS:
///   Each child RectTransform is repositioned along the surface of a cylinder.
///   Its horizontal (X) offset from the canvas centre is converted into an
///   angular position on the cylinder, giving a smooth wrap-around curve.
///
/// HOW TO USE:
///   1. Set your Canvas to "World Space" render mode.
///   2. Attach this script to the Canvas GameObject.
///   3. Adjust "Bend Amount" in the Inspector (or at runtime via the slider).
///      - 0   = perfectly flat
///      - 30  = gentle Samsung-style curve
///      - 90  = aggressive half-pipe
///      - 180 = full semicircle wrap
///
/// NOTES:
///   - Works with Buttons, Images, Text, TMP, ScrollViews, etc.
///   - For VR, pair with a TrackedDeviceGraphicRaycaster on the Canvas.
///   - The script stores each child's original local position on Start,
///     so you can safely adjust bend at runtime without drift.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Canvas))]
public class CurvedCanvasUI : MonoBehaviour
{
    [Header("Curve Settings")]
    [Tooltip("Total angular span in degrees. 0 = flat, 30 = gentle curve, 90 = strong, 180 = semicircle.")]
    [Range(0f, 180f)]
    public float bendAmount = 30f;

    [Tooltip("Radius of the virtual cylinder (world-space units). " +
             "Larger = gentler curve at the same bend angle. " +
             "Set to 0 for auto-calculation based on canvas width.")]
    [Min(0f)]
    public float radius = 0f;

    [Tooltip("Apply the curve on every frame (useful for animated UI). " +
             "Disable for better performance if the canvas is static.")]
    public bool continuousUpdate = true;

    [Tooltip("Also rotate each child so it faces outward from the cylinder surface. " +
             "This looks more natural on thick/3D UI elements.")]
    public bool rotateChildren = true;

    // ── Internal ──────────────────────────────────────────────────────────
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private List<ChildData> _children = new List<ChildData>();
    private float _lastBend = -1f;
    private float _lastRadius = -1f;
    private bool _initialised;

    /// Stores each direct-and-nested child's flat (unbent) local position.
    private struct ChildData
    {
        public RectTransform rect;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
    }

    // =====================================================================
    //  LIFECYCLE
    // =====================================================================

    void OnEnable()
    {
        Initialise();
        ApplyCurve();
    }

    void OnDisable()
    {
        // Restore flat positions so the canvas isn't stuck bent in edit mode
        RestoreFlat();
    }

    void Update()
    {
        if (!_initialised) Initialise();

        // Detect new/removed children
        if (transform.childCount != CountTrackedChildren())
            Initialise();

        if (continuousUpdate || bendAmount != _lastBend || radius != _lastRadius)
            ApplyCurve();
    }

    void OnValidate()
    {
        // React to Inspector slider changes immediately in Edit mode
        if (_initialised)
            ApplyCurve();
    }

    // =====================================================================
    //  CORE LOGIC
    // =====================================================================

    void Initialise()
    {
        _canvas = GetComponent<Canvas>();
        _canvasRect = GetComponent<RectTransform>();
        CaptureChildren();
        _initialised = true;
    }

    /// Walk the hierarchy and store every RectTransform's original position.
    void CaptureChildren()
    {
        // First restore any previously-bent positions
        RestoreFlat();

        _children.Clear();
        CaptureRecursive(transform);
    }

    void CaptureRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            _children.Add(new ChildData
            {
                rect = rt,
                originalLocalPos = rt.localPosition,
                originalLocalRot = rt.localRotation
            });

            // Recurse into nested children (panels inside panels, etc.)
            CaptureRecursive(child);
        }
    }

    int CountTrackedChildren()
    {
        int count = 0;
        CountRecursive(transform, ref count);
        return count;
    }

    void CountRecursive(Transform parent, ref int count)
    {
        foreach (Transform child in parent)
        {
            if (child is RectTransform) count++;
            CountRecursive(child, ref count);
        }
    }

    /// <summary>
    /// Reposition each child along a cylindrical surface.
    ///
    /// The cylinder's axis runs vertically (local Y). Each child's local-X
    /// offset from the canvas centre maps to an angle on the cylinder:
    ///
    ///         angle = (localX / halfWidth) * (bendAmount / 2)
    ///
    /// Then:
    ///         newX  = R * sin(angle)
    ///         newZ  = R * (1 - cos(angle))   ← pushes the edges forward
    /// </summary>
    void ApplyCurve()
    {
        if (_canvasRect == null || _children.Count == 0) return;

        _lastBend = bendAmount;
        _lastRadius = radius;

        // No bend → just restore flat
        if (bendAmount < 0.01f)
        {
            RestoreFlat();
            return;
        }

        float canvasWidth = _canvasRect.rect.width * _canvasRect.localScale.x;
        float halfWidth = canvasWidth * 0.5f;

        if (halfWidth < 0.001f) return;

        // Auto-calculate radius from bend angle if not explicitly set
        float R = radius;
        if (R <= 0.001f)
        {
            // Arc-length = R * theta  →  R = arcLength / theta
            float thetaRad = bendAmount * Mathf.Deg2Rad;
            R = halfWidth / (thetaRad * 0.5f);
        }

        float halfAngle = bendAmount * 0.5f * Mathf.Deg2Rad;

        for (int i = 0; i < _children.Count; i++)
        {
            ChildData cd = _children[i];
            if (cd.rect == null) continue;

            // Get the world-space position of this child's original flat pos
            // relative to the canvas centre.  We need to work in the canvas's
            // own local space, so we convert the child's original local pos
            // into canvas-local coordinates.
            Vector3 canvasLocal = GetCanvasLocalPosition(cd);

            // Normalise X to [-1, +1] across the canvas width
            float normX = canvasLocal.x / halfWidth;
            normX = Mathf.Clamp(normX, -1f, 1f);

            // Map to angle
            float angle = normX * halfAngle;

            // Cylindrical projection
            float newX = R * Mathf.Sin(angle);
            float newZ = R * (1f - Mathf.Cos(angle));

            // Convert back from canvas-local to the child's parent-local space
            Vector3 bentCanvasLocal = new Vector3(newX, canvasLocal.y, canvasLocal.z - newZ);
            Vector3 newLocalPos = ConvertFromCanvasLocal(cd, bentCanvasLocal);

            cd.rect.localPosition = newLocalPos;

            // Optionally rotate so the child faces outward from the cylinder
            if (rotateChildren)
            {
                // The rotation is around the local Y axis by the angle
                Quaternion curveRot = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                cd.rect.localRotation = cd.originalLocalRot * curveRot;
            }
        }
    }

    /// Get a child's original position in the Canvas's own local coordinate system.
    Vector3 GetCanvasLocalPosition(ChildData cd)
    {
        if (cd.rect.parent == transform)
        {
            // Direct child — its localPosition is already canvas-local
            return cd.originalLocalPos;
        }

        // Nested child — compute canvas-local by walking up the parent chain.
        // We use the original local positions we stored, combined with current
        // parent transforms, to get the flat (unbent) canvas-local position.
        // Simplified: use the rect's world position mapped to canvas local.
        // Since we apply curve each frame, we use the stored original positions.
        Vector3 worldPos = GetOriginalWorldPosition(cd);
        return transform.InverseTransformPoint(worldPos);
    }

    Vector3 GetOriginalWorldPosition(ChildData cd)
    {
        // For direct children
        if (cd.rect.parent == transform)
            return transform.TransformPoint(cd.originalLocalPos);

        // For nested children, approximate by using current parent transform
        // (the parent itself may have been bent, but we handle top-level children
        // primarily — nested children follow their parent's transform).
        return cd.rect.parent.TransformPoint(cd.originalLocalPos);
    }

    Vector3 ConvertFromCanvasLocal(ChildData cd, Vector3 canvasLocal)
    {
        if (cd.rect.parent == transform)
            return canvasLocal;

        // Nested child — convert canvas-local back to parent-local
        Vector3 worldPos = transform.TransformPoint(canvasLocal);
        return cd.rect.parent.InverseTransformPoint(worldPos);
    }

    void RestoreFlat()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            if (_children[i].rect != null)
            {
                _children[i].rect.localPosition = _children[i].originalLocalPos;
                _children[i].rect.localRotation = _children[i].originalLocalRot;
            }
        }
    }

    // =====================================================================
    //  PUBLIC API (for runtime control from other scripts / UI sliders)
    // =====================================================================

    /// <summary>Set bend amount from a UI Slider (0–180).</summary>
    public void SetBendAmount(float value)
    {
        bendAmount = Mathf.Clamp(value, 0f, 180f);
        ApplyCurve();
    }

    /// <summary>Set radius from a UI Slider.</summary>
    public void SetRadius(float value)
    {
        radius = Mathf.Max(0f, value);
        ApplyCurve();
    }

    /// <summary>Force a full re-scan of children (call after adding/removing UI elements).</summary>
    public void Refresh()
    {
        Initialise();
        ApplyCurve();
    }
}
