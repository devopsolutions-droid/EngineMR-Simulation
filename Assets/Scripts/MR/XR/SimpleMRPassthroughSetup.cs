using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EngineMR.XR
{
    /// <summary>
    /// Simple, crash-safe MR Passthrough setup for EngineButtons HomeScene.
    /// Add this component to any GameObject in the scene (recommended: create an empty "[MR_Setup]" GameObject).
    /// Automatically configures camera, passthrough, and floating canvas on Awake/Start.
    /// </summary>
    public class SimpleMRPassthroughSetup : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [Tooltip("Auto-find main camera if not assigned")]
        [SerializeField] private Camera targetCamera;
        
        [Tooltip("Camera clear color for passthrough (alpha must be 0)")]
        [SerializeField] private Color passthroughClearColor = new Color(0f, 0f, 0f, 0f);

        [Header("Passthrough Settings")]
        [Tooltip("Enable Meta Quest passthrough on start")]
        [SerializeField] private bool enablePassthrough = true;
        
        [Tooltip("Use floor-level tracking (recommended for MR)")]
        [SerializeField] private bool useFloorLevelTracking = true;

        [Header("Canvas Setup")]
        [Tooltip("Auto-find Universal Canvas if not assigned")]
        [SerializeField] private Transform floatingCanvas;
        
        [Tooltip("Distance from camera to place canvas")]
        [SerializeField] private float canvasDistance = 1.5f;
        
        [Tooltip("Vertical offset for canvas position")]
        [SerializeField] private float canvasHeightOffset = -0.1f;
        
        [Tooltip("Make canvas face camera on startup")]
        [SerializeField] private bool faceCanvasToCamera = true;

        [Header("VR Background Cleanup")]
        [Tooltip("Automatically disable these VR background objects")]
        [SerializeField] private string[] objectsToDisable = new string[] 
        { 
            "Decor", 
            "Background- Homescene", 
            "VR  Hall", 
            "Skybox",
            "Room",
            "Environment"
        };

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            // Find camera if not assigned
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }

            // Find canvas if not assigned
            if (floatingCanvas == null)
            {
                var canvasObj = GameObject.Find("Universal Canvas");
                if (canvasObj != null)
                {
                    floatingCanvas = canvasObj.transform;
                }
            }

            if (enableDebugLogs)
                Debug.Log("[SimpleMRPassthroughSetup] Awake complete. Camera: " + 
                          (targetCamera ? targetCamera.name : "NOT FOUND") + 
                          ", Canvas: " + (floatingCanvas ? floatingCanvas.name : "NOT FOUND"));
        }

        private void Start()
        {
            StartCoroutine(SetupMRRoutine());
        }

        private System.Collections.IEnumerator SetupMRRoutine()
        {
            // Wait a frame to ensure everything is initialized
            yield return null;

            // Step 1: Configure Camera
            ConfigureCamera();
            yield return null;

            // Step 2: Setup Passthrough
            if (enablePassthrough)
            {
                SetupPassthrough();
            }
            yield return null;

            // Step 3: Cleanup VR Background
            CleanupVRBackground();
            yield return null;

            // Step 4: Position Canvas
            PositionFloatingCanvas();
            yield return null;

            if (enableDebugLogs)
                Debug.Log("[SimpleMRPassthroughSetup] MR Setup complete!");
        }

        private void ConfigureCamera()
        {
            if (targetCamera == null)
            {
                Debug.LogWarning("[SimpleMRPassthroughSetup] No camera found for configuration!");
                return;
            }

            try
            {
                // Set camera clear flags for transparency
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = passthroughClearColor;

                // Configure URP camera data if present
                var urpCamData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                if (urpCamData != null)
                {
                    urpCamData.renderPostProcessing = false;
                    urpCamData.renderShadows = true;
                }

                if (enableDebugLogs)
                    Debug.Log($"[SimpleMRPassthroughSetup] Camera '{targetCamera.name}' configured for passthrough");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimpleMRPassthroughSetup] Camera configuration failed: {e.Message}");
            }
        }

        private void SetupPassthrough()
        {
            try
            {
                // Find or create OVRManager
                var ovrManager = FindFirstObjectByType<OVRManager>();
                if (ovrManager == null)
                {
                    var ovrGO = new GameObject("[OVRManager_Auto]");
                    ovrManager = ovrGO.AddComponent<OVRManager>();
                    if (enableDebugLogs)
                        Debug.Log("[SimpleMRPassthroughSetup] Created OVRManager");
                }

                // Enable passthrough
                ovrManager.isInsightPassthroughEnabled = true;
                
                // Set tracking origin
                if (useFloorLevelTracking)
                {
                    ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                }

                if (enableDebugLogs)
                    Debug.Log("[SimpleMRPassthroughSetup] OVRManager passthrough enabled");

                // Setup passthrough layer on camera
                if (targetCamera != null)
                {
                    var passthroughLayer = targetCamera.GetComponent<OVRPassthroughLayer>();
                    if (passthroughLayer == null)
                    {
                        passthroughLayer = targetCamera.gameObject.AddComponent<OVRPassthroughLayer>();
                        if (enableDebugLogs)
                            Debug.Log("[SimpleMRPassthroughSetup] Added OVRPassthroughLayer to camera");
                    }

                    passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
                    passthroughLayer.hidden = false;
                    passthroughLayer.enabled = true;

                    if (enableDebugLogs)
                        Debug.Log("[SimpleMRPassthroughSetup] OVRPassthroughLayer configured");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimpleMRPassthroughSetup] Passthrough setup failed: {e.Message}");
            }
        }

        private void CleanupVRBackground()
        {
            try
            {
                // Clear skybox
                RenderSettings.skybox = null;

                // Disable VR background objects
                if (objectsToDisable != null)
                {
                    foreach (var objName in objectsToDisable)
                    {
                        if (string.IsNullOrEmpty(objName)) continue;
                        
                        var obj = GameObject.Find(objName);
                        if (obj != null)
                        {
                            obj.SetActive(false);
                            if (enableDebugLogs)
                                Debug.Log($"[SimpleMRPassthroughSetup] Disabled: {objName}");
                        }
                    }
                }

                if (enableDebugLogs)
                    Debug.Log("[SimpleMRPassthroughSetup] VR background cleanup complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimpleMRPassthroughSetup] VR cleanup failed: {e.Message}");
            }
        }

        private void PositionFloatingCanvas()
        {
            if (floatingCanvas == null)
            {
                Debug.LogWarning("[SimpleMRPassthroughSetup] No canvas to position!");
                return;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning("[SimpleMRPassthroughSetup] No camera reference for canvas positioning!");
                return;
            }

            try
            {
                // Get camera forward direction (flattened to horizontal plane)
                Vector3 forward = targetCamera.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                // Position canvas in front of camera
                Vector3 targetPosition = targetCamera.transform.position + forward * canvasDistance;
                targetPosition.y += canvasHeightOffset;

                floatingCanvas.position = targetPosition;

                // Face canvas toward camera
                if (faceCanvasToCamera)
                {
                    Vector3 lookDirection = floatingCanvas.position - targetCamera.transform.position;
                    lookDirection.y = 0f;
                    
                    if (lookDirection != Vector3.zero)
                    {
                        floatingCanvas.rotation = Quaternion.LookRotation(lookDirection);
                    }
                }

                // Ensure canvas is in world space
                var canvas = floatingCanvas.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    if (enableDebugLogs)
                        Debug.Log("[SimpleMRPassthroughSetup] Set canvas to World Space");
                }

                if (enableDebugLogs)
                    Debug.Log($"[SimpleMRPassthroughSetup] Canvas positioned at: {floatingCanvas.position}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimpleMRPassthroughSetup] Canvas positioning failed: {e.Message}");
            }
        }

        /// <summary>
        /// Call this method to reposition the canvas at runtime (e.g., after user moves)
        /// </summary>
        public void RepositionCanvas()
        {
            PositionFloatingCanvas();
        }

        /// <summary>
        /// Toggle passthrough on/off at runtime
        /// </summary>
        public void SetPassthroughEnabled(bool enabled)
        {
            var ovrManager = FindFirstObjectByType<OVRManager>();
            if (ovrManager != null)
            {
                ovrManager.isInsightPassthroughEnabled = enabled;
                
                if (enableDebugLogs)
                    Debug.Log($"[SimpleMRPassthroughSetup] Passthrough {(enabled ? "enabled" : "disabled")}");
            }
        }
    }
}
