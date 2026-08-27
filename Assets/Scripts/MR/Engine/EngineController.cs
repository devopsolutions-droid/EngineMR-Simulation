using UnityEngine;
using EngineMR.Interaction;

namespace EngineMR.Engine
{
    /// <summary>
    /// Controls the 3D Engine entity, storing initial placement transforms and component hierarchy roots.
    /// </summary>
    public class EngineController : MonoBehaviour
    {
        [Header("Initial Transform Cache")]
        [SerializeField] private Vector3 initialPosition;
        [SerializeField] private Quaternion initialRotation;
        [SerializeField] private Vector3 initialScale;

        [Header("Modular Engine Hierarchy (Future Phasing)")]
        [SerializeField] private Transform engineBody;
        [SerializeField] private Transform cylinderHead;
        [SerializeField] private Transform pistonGroup;
        [SerializeField] private Transform crankshaft;
        [SerializeField] private Transform accessories;

        private void Start()
        {
            // Register self with InteractionManager
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.SetTargetEngine(gameObject);
            }
        }

        /// <summary>
        /// Initializes the baseline transform data upon first physical placement.
        /// </summary>
        public void InitializeEngine(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            initialPosition = pos;
            initialRotation = rot;
            initialScale = scale;
        }

        /// <summary>
        /// Resets the engine back to its original placement position, rotation, and scale.
        /// </summary>
        public void ResetToInitialTransform()
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            transform.localScale = initialScale;
            Debug.Log("[EngineController] Engine reset to initial placement transform.");
        }

        public Transform GetBody() => engineBody;
        public Transform GetCylinderHead() => cylinderHead;
        public Transform GetPistons() => pistonGroup;
        public Transform GetCrankshaft() => crankshaft;
        public Transform GetAccessories() => accessories;
    }
}
