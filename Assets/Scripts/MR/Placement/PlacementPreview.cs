using UnityEngine;

namespace EngineMR.Placement
{
    /// <summary>
    /// Visual representation (reticle and ghost mesh) showing where the engine will be placed.
    /// </summary>
    public class PlacementPreview : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private GameObject reticleObject;
        [SerializeField] private GameObject ghostEngineObject;

        [Header("Smoothing")]
        [SerializeField] private float positionLerpSpeed = 15f;
        [SerializeField] private float rotationLerpSpeed = 10f;

        private bool isVisible = false;

        private void Awake()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Updates the target preview pose smoothly along the detected surface normal.
        /// </summary>
        public void UpdatePose(Vector3 targetPosition, Vector3 surfaceNormal, Transform userHeadTransform)
        {
            if (!isVisible)
            {
                SetVisible(true);
                transform.position = targetPosition;
            }

            // Smooth position interpolation
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionLerpSpeed);

            // Align preview to surface normal while facing roughly towards the user
            Vector3 forwardProjection = Vector3.ProjectOnPlane(userHeadTransform != null ? userHeadTransform.forward : Vector3.forward, surfaceNormal).normalized;
            if (forwardProjection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardProjection, surfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
            }
        }

        /// <summary>
        /// Shows or hides the preview ghost and reticle visuals.
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (reticleObject != null) reticleObject.SetActive(visible);
            if (ghostEngineObject != null) ghostEngineObject.SetActive(visible);
        }
    }
}
