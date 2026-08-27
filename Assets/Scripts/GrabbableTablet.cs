using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Makes the tablet grabbable with correct orientation for both hands.
/// Left hand: tablet sits naturally (screen faces player).
/// Right hand: tablet shifts left so it feels centered in the right hand.
/// Stays in place when released — no gravity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabbableTablet : MonoBehaviour
{
    [Header("Attach Points")]
    [Tooltip("Attach point for left hand grab.")]
    public Transform leftHandAttach;

    [Tooltip("Attach point for right hand grab — position it more to the right of the tablet.")]
    public Transform rightHandAttach;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity  = false;
        _rb.isKinematic = false;

        _grab = GetComponent<XRGrabInteractable>();
        _grab.movementType  = XRBaseInteractable.MovementType.Instantaneous;
        _grab.throwOnDetach = false;

        _grab.hoverEntered.AddListener(OnHoverEntered);
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    void Start()
    {
        // Disable anchor control on all ray interactors in the scene at start
        foreach (var interactor in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            interactor.allowAnchorControl = false;
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.hoverEntered.RemoveListener(OnHoverEntered);
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        // Detect which hand hovered and swap attach transform BEFORE selection begins
        string interactorName = args.interactorObject.transform.gameObject.name.ToLower();
        bool isRightHand = interactorName.Contains("right");

        if (isRightHand && rightHandAttach != null)
            _grab.attachTransform = rightHandAttach;
        else if (leftHandAttach != null)
            _grab.attachTransform = leftHandAttach;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _rb.isKinematic = false;
        _rb.useGravity  = false;

        // Double check hand at grab time (backup check)
        string interactorName = args.interactorObject.transform.gameObject.name.ToLower();
        bool isRightHand = interactorName.Contains("right");

        if (isRightHand && rightHandAttach != null)
            _grab.attachTransform = rightHandAttach;
        else if (leftHandAttach != null)
            _grab.attachTransform = leftHandAttach;

        // Stop the joystick from moving the tablet closer/further
        if (args.interactorObject is XRRayInteractor rayInteractor)
        {
            rayInteractor.allowAnchorControl = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _rb.velocity        = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.useGravity      = false;
        _rb.isKinematic     = true;
    }
}
