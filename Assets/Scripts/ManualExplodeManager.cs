// ManualExplodeManager.cs
// This script enables manual explode mode for the entire engine.
// When activated, it ensures parts stay assembled and can be grabbed individually.
using UnityEngine;

public class ManualExplodeManager : MonoBehaviour
{
    /// <summary>
    /// Call this to switch all engine parts to manual explode mode.
    /// Parts will not animate to exploded positions; users can grab and move them freely.
    /// </summary>
    public void ActivateManualExplode()
    {
        // Find all EnginePartExplode components in the scene
        var exploders = FindObjectsByType<EnginePartExplode>(FindObjectsSortMode.None);
        foreach (var exploder in exploders)
        {
            exploder.EnableManualExplodeMode();
        }
        // Optionally disable other automatic systems (e.g., XRay, Snap)
        var grabManager = FindObjectOfType<EngineGrabManager>();
        if (grabManager != null)
        {
            // Ensure manual grabbing is permitted
            grabManager.allowGrabbing = true;
        }
    }
}
