using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Makes a screen (like Tutorial Player) grabbable and movable by holding onto a handle bar.
/// Uses XR Interaction Toolkit, matching the physics settings of GrabbableTablet.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VRGrabScreenHandle : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The handle bar GameObject that the user grabs to move the screen. If null, will search for a child named 'Grab Bar'.")]
    public GameObject grabHandle;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    void Awake()
    {
        // ── Auto-find Grab Bar if unassigned ──────────────────────────────────
        if (grabHandle == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name.ToLower() == "grab bar")
                {
                    grabHandle = child.gameObject;
                    break;
                }
            }
        }

        // ── Rigidbody Setup ───────────────────────────────────────────────────
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;

        // ── XRGrabInteractable Setup ──────────────────────────────────────────
        _grab = GetComponent<XRGrabInteractable>();
        if (_grab == null)
            _grab = gameObject.AddComponent<XRGrabInteractable>();

        _grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        _grab.throwOnDetach = false;

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

        if (grabHandle != null)
        {
            // Set the physics layer to Default (0) so VR controller rays and direct hands can hit it
            grabHandle.layer = 0;

            // ── Auto-generate Collider if missing ─────────────────────────────
            // Canvas UI items don't have 3D colliders by default.
            // If the handle is a UI element (has RectTransform) and lacks a collider,
            // we dynamically fit a BoxCollider to its dimensions.
            var collider = grabHandle.GetComponent<Collider>();
            if (collider == null)
            {
                var rect = grabHandle.GetComponent<RectTransform>();
                if (rect != null)
                {
                    var boxCol = grabHandle.AddComponent<BoxCollider>();
                    
                    // Fit the size to the RectTransform
                    // Z size should be 5cm in world coordinates, adjusted for the lossy scale of the handle
                    float worldZThickness = 0.05f; // 5 cm thickness
                    float lossyZ = grabHandle.transform.lossyScale.z;
                    float localZ = lossyZ > 0.0001f ? (worldZThickness / lossyZ) : 1f;

                    boxCol.size = new Vector3(rect.rect.width, rect.rect.height, localZ);

                    // Adjust center based on the RectTransform pivot to align it perfectly with the visual representation
                    boxCol.center = new Vector3(
                        (0.5f - rect.pivot.x) * rect.rect.width,
                        (0.5f - rect.pivot.y) * rect.rect.height,
                        0f
                    );

                    collider = boxCol;
                    Debug.Log($"[VRGrabScreenHandle] Dynamically added BoxCollider (size: {boxCol.size}, center: {boxCol.center}) to '{grabHandle.name}'");
                }
            }

            // Assign only the handle's collider to the interactable
            if (collider != null)
            {
                _grab.colliders.Clear();
                _grab.colliders.Add(collider);
                Debug.Log($"[VRGrabScreenHandle] Registered collider of '{grabHandle.name}' for grab interactions.");
            }
            else
            {
                Debug.LogWarning($"[VRGrabScreenHandle] Grab Handle '{grabHandle.name}' has no collider and is not a RectTransform.");
            }
        }
        else
        {
            Debug.LogWarning("[VRGrabScreenHandle] Grab Handle 'Grab Bar' not found or assigned! The entire screen might be grabbable.");
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _rb.isKinematic = false;
        _rb.useGravity = false;

        // Stop the joystick from moving the screen closer/further
        if (args.interactorObject is XRRayInteractor rayInteractor)
        {
            rayInteractor.allowAnchorControl = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;
    }
}
