using UnityEngine;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Grounding")]
    public float gravity = -9.81f;

    private CharacterController _cc;
    private XROrigin _xrOrigin;
    private float _verticalVelocity;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _xrOrigin = GetComponent<XROrigin>();
    }

    void Update()
    {
        SyncCapsuleHeight();
        ApplyGravity();
    }

    void SyncCapsuleHeight()
    {
        // only sync the HEIGHT of the capsule to the camera — NOT the XZ position
        // syncing XZ causes the CharacterController to push the XR Origin when
        // the player physically leans, creating unwanted drift
        var cameraLocalPos = _xrOrigin.CameraInOriginSpacePos;
        _cc.height = Mathf.Max(0.1f, cameraLocalPos.y);
        _cc.center = new Vector3(0f, _cc.height / 2f, 0f);
    }

    void ApplyGravity()
    {
        if (_cc.isGrounded)
            _verticalVelocity = -0.5f;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        _cc.Move(new Vector3(0, _verticalVelocity * Time.deltaTime, 0));
    }
}
