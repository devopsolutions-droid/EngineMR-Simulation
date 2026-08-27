using UnityEngine;
using EngineMR.Common;

namespace EngineMR.Environment
{
    public struct SurfaceHitResult
    {
        public bool IsValid;
        public Vector3 Position;
        public Vector3 Normal;
        public SurfaceType SurfaceType;
        public GameObject HitObject;
    }

    /// <summary>
    /// Performs raycasting from user input (controllers/hands/gaze) to detect valid real-world surfaces for engine placement.
    /// </summary>
    public class SurfaceDetector : MonoBehaviour
    {
        [Header("Raycast Configuration")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private float maxRayDistance = 10f;
        [SerializeField] private LayerMask surfaceLayerMask = ~0; // All layers by default

        [Header("Surface Filtering")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float minUpwardNormalThreshold = 0.7f; // Must face mostly upwards (floor/table)

        public SurfaceHitResult CurrentHit { get; private set; }

        private void Update()
        {
            DetectSurface();
        }

        /// <summary>
        /// Casts a ray from the designated origin and evaluates hit geometry.
        /// </summary>
        public SurfaceHitResult DetectSurface()
        {
            Transform origin = rayOrigin != null ? rayOrigin : (Camera.main != null ? Camera.main.transform : transform);
            Ray ray = new Ray(origin.position, origin.forward);

            SurfaceHitResult result = new SurfaceHitResult { IsValid = false };

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, surfaceLayerMask))
            {
                float upwardDot = Vector3.Dot(hit.normal, Vector3.up);

                // Filter out non-horizontal surfaces (e.g. walls/ceilings)
                if (upwardDot >= minUpwardNormalThreshold)
                {
                    SurfaceType type = EnvironmentManager.Instance != null 
                        ? EnvironmentManager.Instance.ClassifySurface(hit.normal, hit.collider.gameObject.name) 
                        : SurfaceType.Table;

                    result.IsValid = true;
                    result.Position = hit.point;
                    result.Normal = hit.normal;
                    result.SurfaceType = type;
                    result.HitObject = hit.collider.gameObject;
                }
            }

            CurrentHit = result;
            return result;
        }

        public void SetRayOrigin(Transform newOrigin)
        {
            rayOrigin = newOrigin;
        }
    }
}
