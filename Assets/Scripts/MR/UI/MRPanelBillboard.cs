using UnityEngine;

namespace EngineMR.UI
{
    /// <summary>
    /// Keeps a world-space MR UI panel comfortably positioned and oriented in front of the headset/user.
    /// Supports smooth follow and look-at rotation.
    /// </summary>
    public class MRPanelBillboard : MonoBehaviour
    {
        [Header("Target & Positioning")]
        [SerializeField] private Transform targetCamera;
        [SerializeField] private float defaultDistance = 1.2f;
        [SerializeField] private float heightOffset = -0.1f;
        [SerializeField] private bool autoPositionOnStart = true;
        [SerializeField] private bool lockPitch = true;

        [Header("Smoothing")]
        [SerializeField] private bool smoothFollow = false;
        [SerializeField] private float followSpeed = 5.0f;
        [SerializeField] private float rotationSpeed = 5.0f;

        private void Start()
        {
            if (targetCamera == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) targetCamera = mainCam.transform;
            }

            if (autoPositionOnStart && targetCamera != null)
            {
                RecenterPanel();
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            if (smoothFollow)
            {
                // Compute desired forward position
                Vector3 forward = targetCamera.forward;
                if (lockPitch)
                {
                    forward.y = 0;
                    forward.Normalize();
                }

                Vector3 targetPos = targetCamera.position + forward * defaultDistance + Vector3.up * heightOffset;
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

                // Rotate to face camera
                Vector3 lookDir = transform.position - targetCamera.position;
                if (lockPitch) lookDir.y = 0;

                if (lookDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                }
            }
            else
            {
                // Immediate look at
                Vector3 lookDir = transform.position - targetCamera.position;
                if (lockPitch) lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        /// <summary>
        /// Instantly places and faces the panel right in front of the user's current view.
        /// </summary>
        public void RecenterPanel()
        {
            if (targetCamera == null) return;

            Vector3 forward = targetCamera.forward;
            if (lockPitch)
            {
                forward.y = 0;
                forward.Normalize();
            }

            transform.position = targetCamera.position + forward * defaultDistance + Vector3.up * heightOffset;
            Vector3 lookDir = transform.position - targetCamera.position;
            if (lockPitch) lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
}
