using UnityEngine;

/// <summary>
/// Makes the hand follow the controller's position and rotation.
/// Attach to the hand GameObject (LeftHand or RightHand).
/// The hand will be positioned at the controller's location and rotate with head/camera.
/// </summary>
public class HandFollowController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The controller this hand should follow (usually the parent).")]
    public Transform controllerTransform;

    [Header("Settings")]
    [Tooltip("If true, hand will follow controller position. If false, hand stays in place.")]
    public bool followPosition = true;

    [Tooltip("If true, hand will follow controller rotation. If false, hand keeps its rotation.")]
    public bool followRotation = true;

    [Tooltip("Position offset from controller (in local space).")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Rotation offset from controller (in local space).")]
    public Vector3 rotationOffset = Vector3.zero;

    private bool _initialized = false;

    void Start()
    {
        // If no controller assigned, try to find it
        if (controllerTransform == null)
        {
            // Try parent first
            if (transform.parent != null)
            {
                controllerTransform = transform.parent;
            }
        }

        if (controllerTransform == null)
        {
            Debug.LogError($"[HandFollowController] No controller transform found for {gameObject.name}. Hand will not follow.");
            enabled = false;
            return;
        }

        _initialized = true;
        Debug.Log($"[HandFollowController] {gameObject.name} initialized - following {controllerTransform.name}");
    }

    void LateUpdate()
    {
        if (!_initialized || controllerTransform == null) return;

        if (followPosition)
        {
            // Follow controller position + offset
            transform.position = controllerTransform.position + controllerTransform.TransformDirection(positionOffset);
        }

        if (followRotation)
        {
            // Follow controller rotation + offset
            transform.rotation = controllerTransform.rotation * Quaternion.Euler(rotationOffset);
        }
    }

    /// <summary>
    /// Manually set the controller to follow.
    /// </summary>
    public void SetControllerTransform(Transform newController)
    {
        controllerTransform = newController;
        if (controllerTransform != null)
        {
            Debug.Log($"[HandFollowController] {gameObject.name} now following {controllerTransform.name}");
        }
    }
}
