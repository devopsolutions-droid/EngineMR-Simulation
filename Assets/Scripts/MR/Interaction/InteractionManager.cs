using UnityEngine;
using EngineMR.Common;

namespace EngineMR.Interaction
{
    /// <summary>
    /// Handles mixed reality direct & distance manipulation: Move, Rotate, and Two-Hand Scale on the placed Engine.
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }

        [Header("Manipulation Settings")]
        [SerializeField] private float minScale = 0.2f;
        [SerializeField] private float maxScale = 2.5f;
        [SerializeField] private float rotationSpeed = 90f;

        private GameObject targetEngine;
        private ManipulationMode currentMode = ManipulationMode.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetTargetEngine(GameObject engine)
        {
            targetEngine = engine;
        }

        private void Update()
        {
            if (targetEngine == null) return;

            HandleDesktopEditorManipulation();
        }

        /// <summary>
        /// Applies translation to the target engine.
        /// </summary>
        public void TranslateEngine(Vector3 deltaPosition)
        {
            if (targetEngine == null) return;
            targetEngine.transform.position += deltaPosition;
        }

        /// <summary>
        /// Applies rotation around the vertical axis or gesture vector.
        /// </summary>
        public void RotateEngine(float angleDelta, Vector3 axis)
        {
            if (targetEngine == null) return;
            targetEngine.transform.Rotate(axis, angleDelta, Space.World);
        }

        /// <summary>
        /// Uniformly scales the engine with minimum and maximum bounds clamping.
        /// </summary>
        public void ScaleEngine(float scaleFactor)
        {
            if (targetEngine == null) return;

            Vector3 currentScale = targetEngine.transform.localScale;
            float newScaleValue = Mathf.Clamp(currentScale.x * scaleFactor, minScale, maxScale);

            targetEngine.transform.localScale = Vector3.one * newScaleValue;
        }

        private void HandleDesktopEditorManipulation()
        {
            // Rotate with Q / E keys in Editor
            if (Input.GetKey(KeyCode.Q))
            {
                RotateEngine(-rotationSpeed * Time.deltaTime, Vector3.up);
            }
            if (Input.GetKey(KeyCode.E))
            {
                RotateEngine(rotationSpeed * Time.deltaTime, Vector3.up);
            }

            // Scale with Scroll Wheel in Editor
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float scaleMultiplier = scroll > 0 ? 1.1f : 0.9f;
                ScaleEngine(scaleMultiplier);
            }
        }
    }
}
