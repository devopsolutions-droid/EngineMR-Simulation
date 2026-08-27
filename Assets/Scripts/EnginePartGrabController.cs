// This script attaches to engine parts to make them grabbable and responsive to ray hovers.
using UnityEngine;

/// <summary>
/// Free-placement grab for a single engine part.
///
/// Behaviour:
///   • Ray hovers over part      → red outline on (ONLY outline, no audio/panel)
///   • Trigger PRESSED           → this part is "grabbed" — it does NOT jump or rotate
///   • Controller moves          → X/Y follow the ray; Z is controlled by thumbstick
///   • Only ONE part at a time   → grabbing a new part auto-releases the previous
///   • Trigger RELEASED          → part stays exactly where it was left
///
/// In Grab Mode:
///   - Only outline shows on hover (no hover panel, no audio, no glow)
///   - Part moves smoothly with the ray on X/Y and the joystick on Z
///   - No other features active
/// </summary>
[RequireComponent(typeof(EnginePart))]
public class EnginePartGrabController : MonoBehaviour
{
    // Accessed by EngineGrabManager
    internal EnginePart Part { get; private set; }

    /// <summary>
    /// When true, this part can be grabbed during Show Working interactive flow.
    /// Set/cleared automatically by ShowWorkingInteractiveController / SimpleShowWorkingController.
    /// </summary>
    public bool grabbableInShowWorking = false;

    void Awake()
    {
        Part = GetComponent<EnginePart>();
    }

    /// <summary>Called by EngineGrabManager when this part starts being moved.</summary>
    public void OnGrabStart()
    {
        Part.SetHighlight(false);
    }

    /// <summary>Called by EngineGrabManager when this part is released.</summary>
    public void OnGrabEnd()
    {
        // Nothing — part stays where it is.
        Part.SetHighlight(false);
    }

    /// <summary>Called by EngineGrabManager on hover enter — show outline only.</summary>
    public void OnHoverEnter()
    {
        Part.SetHighlight(true);
    }

    /// <summary>Called by EngineGrabManager on hover exit — hide outline.</summary>
    public void OnHoverExit()
    {
        Part.SetHighlight(false);
    }
}
