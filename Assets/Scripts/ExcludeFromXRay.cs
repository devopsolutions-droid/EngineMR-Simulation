using UnityEngine;

public enum XRayExclusionMode
{
    KeepOriginalMaterial, // Remains visible with original materials/textures
    HideGameObject        // Completely hidden (SetActive(false)) when X-Ray is active
}

/// <summary>
/// Attach this component to any child GameObject in an engine model prefab
/// to exclude it from going into holographic X-Ray mode.
/// </summary>
public class ExcludeFromXRay : MonoBehaviour
{
    [Tooltip("KeepOriginalMaterial: Remains rendered with its normal textures and shaders.\nHideGameObject: The object is deactivated (hidden) while X-Ray mode is active.")]
    public XRayExclusionMode mode = XRayExclusionMode.KeepOriginalMaterial;
}
