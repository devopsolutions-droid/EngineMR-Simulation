using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Fixes the invisible raycast line in URP projects.
///
/// ROOT CAUSE: The LineRenderer on XR Ray Interactors uses Unity's built-in
/// legacy "Default-Line" material (fileID 10306). This shader is NOT supported
/// by the Universal Render Pipeline (URP), making the ray line completely
/// invisible even though the raycasting logic still works.
///
/// FIX: On Awake(), this script finds ALL XRRayInteractors in the scene and
/// replaces their LineRenderer material with a URP-compatible shader
/// ("Sprites/Default") that supports vertex colours and alpha — exactly what
/// XRInteractorLineVisual needs.
///
/// HOW TO USE:
///   1. Create an empty GameObject in the scene (e.g. "Raycast Fix")
///   2. Attach this script to it
///   That's it — it auto-patches every XR ray line in the scene.
/// </summary>
public class RaycastLineMaterialFix : MonoBehaviour
{
    [Header("Line Appearance")]
    [Tooltip("Width of the ray line in world-space metres.")]
    [Range(0.001f, 0.5f)]
    public float lineWidth = 0.012f;

    [Tooltip("Width of the ray line when hovering over the tablet UI or tablet object.")]
    [Range(0.001f, 0.1f)]
    public float tabletLineWidth = 0.006f;

    [Tooltip("If true, also patches controllers that are spawned later.")]
    public bool continuousCheck = true;

    private Material _urpLineMaterial;
    private Gradient _whiteToBlueGradient;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<RaycastLineMaterialFix>() == null)
        {
            var go = new GameObject("[Auto_RaycastLineMaterialFix]");
            go.AddComponent<RaycastLineMaterialFix>();
            DontDestroyOnLoad(go);
            Debug.Log("[RaycastLineMaterialFix] Automatically initialized for scene.");
        }
    }

    void Awake()
    {
        // Build the replacement material once
        _urpLineMaterial = CreateURPLineMaterial();

        if (_urpLineMaterial == null)
        {
            Debug.LogError("[RaycastLineMaterialFix] Failed to create URP material — raycast lines may stay invisible.");
            enabled = false;
            return;
        }

        // Build white-to-blue gradient matching the Main Scene style
        _whiteToBlueGradient = new Gradient();
        _whiteToBlueGradient.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0f, 0.6f, 1f), 0.5f),
                new GradientColorKey(new Color(0f, 0.2f, 0.8f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        // Fix all existing interactors right away
        PatchAllInteractors();
    }

    void Update()
    {
        // Continuously ensure width is applied and scan for new interactors
        PatchAllInteractors();
    }

    void PatchAllInteractors()
    {
        var interactors = FindObjectsByType<XRRayInteractor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var interactor in interactors)
        {
            LineRenderer lr = interactor.GetComponent<LineRenderer>();
            if (lr == null) continue;

            // Apply URP material if not already applied
            if (lr.sharedMaterial == null || lr.sharedMaterial.name != "RaycastLine_URP_Fix")
            {
                string oldShader = lr.sharedMaterial != null ? lr.sharedMaterial.shader.name : "NULL";
                lr.material = _urpLineMaterial;
                Debug.Log($"[RaycastLineMaterialFix] Patched '{interactor.gameObject.name}' — replaced shader '{oldShader}' with '{_urpLineMaterial.shader.name}'");
            }

            // Determine if raycast is hovering over a tablet UI element or tablet object
            bool isHittingTablet = false;
            if (interactor.TryGetCurrentUIRaycastResult(out var uiHit) && uiHit.isValid)
            {
                if (IsTabletObject(uiHit.gameObject))
                {
                    isHittingTablet = true;
                }
            }

            if (!isHittingTablet && interactor.TryGetCurrent3DRaycastHit(out var hit3D) && hit3D.collider != null)
            {
                if (IsTabletObject(hit3D.collider.gameObject))
                {
                    isHittingTablet = true;
                }
            }

            float currentTargetWidth = isHittingTablet ? tabletLineWidth : lineWidth;

            // Update thickness & gradient on XRInteractorLineVisual if present
            var lineVisual = interactor.GetComponent<XRInteractorLineVisual>();
            if (lineVisual != null)
            {
                lineVisual.lineWidth = currentTargetWidth;
                lineVisual.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);

                if (_whiteToBlueGradient != null)
                {
                    lineVisual.validColorGradient = _whiteToBlueGradient;
                    lineVisual.invalidColorGradient = _whiteToBlueGradient;
                    lineVisual.blockedColorGradient = _whiteToBlueGradient;
                }
            }

            // Enforce thickness on LineRenderer
            lr.startWidth = currentTargetWidth;
            lr.endWidth = currentTargetWidth;
            lr.widthMultiplier = currentTargetWidth;
            lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            lr.enabled = true;
        }
    }

    private bool IsTabletObject(GameObject obj)
    {
        if (obj == null) return false;

        if (obj.GetComponentInParent<TabletUIController>() != null ||
            obj.GetComponentInParent<GrabbableTablet>() != null)
        {
            return true;
        }

        Transform current = obj.transform;
        while (current != null)
        {
            if (current.name.IndexOf("Tablet", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    Material CreateURPLineMaterial()
    {
        // "Sprites/Default" works on Built-in, URP, and HDRP.
        // It supports vertex colours and transparency out of the box.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            // Fallback: URP particles unlit
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            Debug.LogError("[RaycastLineMaterialFix] No compatible shader found " +
                           "('Sprites/Default' or 'URP/Particles/Unlit').");
            return null;
        }

        var mat = new Material(shader);
        mat.name = "RaycastLine_URP_Fix";

        if (mat.HasProperty("_Color"))
            mat.color = Color.white;

        return mat;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f,
            "[RaycastLineMaterialFix] Active");
    }
#endif
}
