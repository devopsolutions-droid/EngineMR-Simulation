using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Fully self-contained free grab controller.
/// Lets the user grab and move any engine part freely.
/// No snap, no steps, no indicators, no dependency on EngineGrabManager.
/// Wire: Manual Separate button → FreeGrabController.Activate
/// </summary>
public class FreeGrabController : MonoBehaviour
{
    [Header("References")]
    public XRRayInteractor      rayInteractor;
    public InputActionReference grabAction;

    [Header("Layer")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Settings")]
    [Range(0.05f, 1f)] public float followSpeed    = 0.35f;
    [Min(0.01f)]       public float depthMoveSpeed = 0.8f;
    [Range(0f, 0.5f)]  public float depthDeadzone  = 0.08f;

    private Transform _grabbed;
    private Vector3   _grabOffset;
    private float     _grabDepth;
    private float     _grabZ;
    private bool      _triggerHeld;
    private bool      _active;

    void OnEnable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed += OnTriggerDown;
            grabAction.action.canceled  += OnTriggerUp;
            grabAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed -= OnTriggerDown;
            grabAction.action.canceled  -= OnTriggerUp;
        }
        _grabbed = null;
    }

    void Update()
    {
        if (!_active || _grabbed == null || !_triggerHeld) return;
        MovePart();
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public void Activate()   { _active = true; }
    public void Deactivate() { _active = false; _grabbed = null; }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void OnTriggerDown(InputAction.CallbackContext ctx)
    {
        _triggerHeld = true;
        if (!_active || rayInteractor == null) return;
        if (!TryRaycast(out RaycastHit hit)) return;

        var root = hit.collider.GetComponentInParent<EnginePart>();
        if (root == null) return;

        _grabbed    = root.transform;
        _grabZ      = _grabbed.position.z;
        _grabOffset = hit.point - _grabbed.position;

        Vector3 toHit = hit.point - rayInteractor.transform.position;
        _grabDepth    = Vector3.Dot(toHit, rayInteractor.transform.forward);
    }

    private void OnTriggerUp(InputAction.CallbackContext ctx)
    {
        _triggerHeld = false;
        _grabbed     = null;
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    private void MovePart()
    {
        float stick = ReadStick();
        if (Mathf.Abs(stick) > depthDeadzone)
            _grabZ += stick * depthMoveSpeed * Time.deltaTime;

        Vector3 target = rayInteractor.transform.position
                       + rayInteractor.transform.forward * _grabDepth
                       - _grabOffset;
        target.z = _grabZ;

        Vector3 cur = _grabbed.position;
        _grabbed.position = new Vector3(
            Mathf.Lerp(cur.x, target.x, followSpeed),
            Mathf.Lerp(cur.y, target.y, followSpeed),
            _grabZ
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryRaycast(out RaycastHit hit)
    {
        hit = default;
        if (rayInteractor == null) return false;
        if (rayInteractor.TryGetCurrent3DRaycastHit(out hit))
            if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                return true;
        return false;
    }

    private float ReadStick()
    {
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            return axis.y;
        return 0f;
    }
}
