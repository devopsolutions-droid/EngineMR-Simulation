using System;
using UnityEngine;

namespace EngineMR.XR
{
    /// <summary>
    /// Manages XR initialization, Passthrough visibility, and camera clear configurations for Quest 3 / 3S.
    /// </summary>
    public class XRManager : MonoBehaviour
    {
        public static XRManager Instance { get; private set; }

        [Header("Camera & Passthrough")]
        [SerializeField] private Camera xrCamera;
        [SerializeField] private bool enablePassthroughOnStart = true;
        [SerializeField] private Color passthroughClearColor = new Color(0, 0, 0, 0);

        [Header("Tracking Settings")]
        [SerializeField] private bool setFloorLevelTracking = true;

        public event Action OnXRInitialized;
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (xrCamera == null)
            {
                xrCamera = Camera.main;
            }
        }

        private void Start()
        {
            InitializeXR();
        }

        /// <summary>
        /// Configures the XR camera and Meta passthrough settings for mixed reality rendering.
        /// </summary>
        public void InitializeXR()
        {
            ConfigureCameraForPassthrough();
            ConfigureTracking();

            IsInitialized = true;
            OnXRInitialized?.Invoke();
            Debug.Log("[XRManager] Quest MR Environment successfully initialized with Passthrough.");
        }

        private void ConfigureCameraForPassthrough()
        {
            if (xrCamera == null)
            {
                Debug.LogWarning("[XRManager] Main Camera reference is missing! Attempting to find Camera.main.");
                xrCamera = Camera.main;
                if (xrCamera == null) return;
            }

            // In URP / Quest Passthrough, setting solid color with alpha = 0 renders the underlying real-world feed.
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = passthroughClearColor;

            #if META_XR_SDK || OCULUS
            // Configure OVRManager passthrough if present in runtime
            var ovrManager = FindObjectOfType<OVRManager>();
            if (ovrManager != null && enablePassthroughOnStart)
            {
                ovrManager.isInsightPassthroughEnabled = true;
            }
            #endif
        }

        private void ConfigureTracking()
        {
            #if META_XR_SDK || OCULUS
            if (setFloorLevelTracking)
            {
                OVRManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }
            #endif
        }

        public Camera GetXRCamera() => xrCamera;
    }
}
