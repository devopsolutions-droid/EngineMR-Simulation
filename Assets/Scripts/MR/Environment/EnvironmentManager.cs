using System;
using UnityEngine;
using EngineMR.Common;

namespace EngineMR.Environment
{
    /// <summary>
    /// Coordinates environment awareness, MRUK room state, and classified physical surface updates.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        [Header("MRUK Settings")]
        [SerializeField] private bool autoLoadRoomScene = true;

        public bool IsSceneDataReady { get; private set; } = false;

        public event Action OnEnvironmentLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CheckEnvironmentState();
        }

        private void CheckEnvironmentState()
        {
            // Set ready flag for scene queries
            IsSceneDataReady = true;
            OnEnvironmentLoaded?.Invoke();
            Debug.Log("[EnvironmentManager] Environment scene awareness ready.");
        }

        /// <summary>
        /// Classifies a hit normal or label into a standardized SurfaceType.
        /// </summary>
        public SurfaceType ClassifySurface(Vector3 normal, string surfaceLabel = "")
        {
            float upwardDot = Vector3.Dot(normal, Vector3.up);

            if (!string.IsNullOrEmpty(surfaceLabel))
            {
                string labelLower = surfaceLabel.ToLower();
                if (labelLower.Contains("table") || labelLower.Contains("desk")) return SurfaceType.Table;
                if (labelLower.Contains("floor")) return SurfaceType.Floor;
                if (labelLower.Contains("wall")) return SurfaceType.Wall;
                if (labelLower.Contains("couch")) return SurfaceType.Couch;
            }

            // Fallback geometry normal classification
            if (upwardDot >= 0.85f)
            {
                return SurfaceType.Table; // or Floor depending on elevation
            }
            else if (Mathf.Abs(upwardDot) < 0.2f)
            {
                return SurfaceType.Wall;
            }

            return SurfaceType.Unknown;
        }
    }
}
