using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Detects hand movement and disables the controller's raycast when hand is inactive.
/// Attach to each hand (LeftHand, RightHand) that has an XRRayInteractor.
/// </summary>
public class HandActivityDetector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The XRRayInteractor on this controller to disable when hand is inactive.")]
    public XRRayInteractor rayInteractor;

    [Header("Settings")]
    [Tooltip("Minimum movement distance to consider hand as active (in meters).")]
    public float movementThreshold = 0.01f;

    [Tooltip("Time in seconds of no movement before disabling the controller.")]
    public float inactivityTimeout = 2f;

    [Tooltip("Time in seconds to wait before re-enabling after detecting movement.")]
    public float reactivationDelay = 0.1f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private float _timeSinceLastMovement = 0f;
    private bool _isActive = true;
    private float _reactivationTimer = 0f;

    void Start()
    {
        if (rayInteractor == null)
        {
            rayInteractor = GetComponentInChildren<XRRayInteractor>();
        }

        if (rayInteractor == null)
        {
            Debug.LogError($"[HandActivityDetector] No XRRayInteractor found on {gameObject.name}");
            enabled = false;
            return;
        }

        _lastPosition = transform.position;
        _lastRotation = transform.rotation;
        _timeSinceLastMovement = 0f;

        Debug.Log($"[HandActivityDetector] Initialized on {gameObject.name}, monitoring {rayInteractor.gameObject.name}");
    }

    void Update()
    {
        // Check if hand has moved
        float positionDelta = Vector3.Distance(transform.position, _lastPosition);
        float rotationDelta = Quaternion.Angle(transform.rotation, _lastRotation);

        if (positionDelta > movementThreshold || rotationDelta > movementThreshold)
        {
            // Hand is moving
            _timeSinceLastMovement = 0f;
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;

            // If was inactive, reactivate
            if (!_isActive)
            {
                _reactivationTimer += Time.deltaTime;
                if (_reactivationTimer >= reactivationDelay)
                {
                    EnableController();
                    _reactivationTimer = 0f;
                }
            }
        }
        else
        {
            // Hand is not moving
            _timeSinceLastMovement += Time.deltaTime;

            // If inactive timeout reached, disable controller
            if (_isActive && _timeSinceLastMovement >= inactivityTimeout)
            {
                DisableController();
            }
        }
    }

    private void EnableController()
    {
        if (_isActive) return;

        _isActive = true;
        rayInteractor.enabled = true;
        Debug.Log($"[HandActivityDetector] Enabled raycast for {gameObject.name}");
    }

    private void DisableController()
    {
        if (!_isActive) return;

        _isActive = false;
        rayInteractor.enabled = false;
        Debug.Log($"[HandActivityDetector] Disabled raycast for {gameObject.name} (inactive for {inactivityTimeout}s)");
    }

    /// <summary>
    /// Manually force enable/disable the controller.
    /// </summary>
    public void SetControllerActive(bool active)
    {
        if (active)
            EnableController();
        else
            DisableController();
    }

    /// <summary>
    /// Check if this hand is currently active.
    /// </summary>
    public bool IsHandActive => _isActive;
}
