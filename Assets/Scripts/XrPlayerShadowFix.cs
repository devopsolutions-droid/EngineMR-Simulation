using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// VR hands/controllers parented under XR Origin should not cast floor shadows.
/// Attach to XR Origin (XR Rig).
/// </summary>
[DisallowMultipleComponent]
public class XrPlayerShadowFix : MonoBehaviour
{
    [Tooltip("Disable shadow casting on all renderers under this rig (recommended).")]
    public bool disableCastShadows = true;

    [Tooltip("Also stop renderers from receiving shadows (usually leave off).")]
    public bool disableReceiveShadows;

    void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (disableCastShadows)
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            if (disableReceiveShadows)
                renderer.receiveShadows = false;
        }
    }
}
