using UnityEngine;

namespace EngineMR.Anchoring
{
    /// <summary>
    /// Handles attaching and managing Meta Spatial Anchors on placed MR objects to ensure spatial stability.
    /// </summary>
    public class SpatialAnchorManager : MonoBehaviour
    {
        public static SpatialAnchorManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Attaches an OVRSpatialAnchor component to the target GameObject and handles anchor creation.
        /// </summary>
        public void AnchorObject(GameObject targetObject)
        {
            if (targetObject == null) return;

            #if META_XR_SDK || OCULUS
            OVRSpatialAnchor anchor = targetObject.GetComponent<OVRSpatialAnchor>();
            if (anchor == null)
            {
                anchor = targetObject.AddComponent<OVRSpatialAnchor>();
            }

            Debug.Log($"[SpatialAnchorManager] Attached OVRSpatialAnchor to {targetObject.name}");
            #else
            Debug.Log($"[SpatialAnchorManager] Standard World Lock initialized on {targetObject.name}");
            #endif
        }
    }
}
