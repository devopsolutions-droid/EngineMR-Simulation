using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the Oculus hand mesh animator from XR controller input.
/// Attach to the hand mesh GameObject (child of Left/Right Controller).
/// Animator must have float params: "Grip" and "Trigger".
/// </summary>
public class VRHandAnimator : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference gripAction;
    public InputActionReference triggerAction;

    private Animator _animator;
    private static readonly int GripHash    = Animator.StringToHash("Grip");
    private static readonly int TriggerHash = Animator.StringToHash("Trigger");

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        gripAction?.action.Enable();
        triggerAction?.action.Enable();
    }

    void OnDisable()
    {
        gripAction?.action.Disable();
        triggerAction?.action.Disable();
    }

    void Update()
    {
        if (_animator == null) return;

        float grip    = gripAction    != null ? gripAction.action.ReadValue<float>()    : 0f;
        float trigger = triggerAction != null ? triggerAction.action.ReadValue<float>() : 0f;

        _animator.SetFloat(GripHash,    grip);
        _animator.SetFloat(TriggerHash, trigger);
    }
}
