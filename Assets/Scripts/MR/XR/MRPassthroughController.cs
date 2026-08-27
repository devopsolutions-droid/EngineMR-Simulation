using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EngineMR.XR
{
    /// <summary>
    /// Converts any VR scene into a true Mixed Reality (MR) Passthrough scene on Meta Quest.
    /// Safe for both Editor Play Mode and Headset Runtime:
    ///   1. Configures Main Camera ClearFlags to SolidColor with RGBA(0, 0, 0, 0) for alpha transparency.
    ///   2. Enables Meta Insight Passthrough when supported.
    ///   3. Disables skyboxes and opaque VR environment meshes/backgrounds.
    ///   4. Recenters and floats the UI Canvas in front of the headset.
    /// </summary>
    public class MRPassthroughController : MonoBehaviour
    {
        [Header("Passthrough Settings")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool enablePassthroughOnStart = true;
        [SerializeField] private Color clearColor = new Color(0f, 0f, 0f, 0f);

        [Header("VR Background Cleanup")]
        [SerializeField] private string[] objectsToHide = new string[] { "Decor", "Background- Homescene", "VR  Hall", "Skybox" };

        [Header("UI Floating Position")]
        [SerializeField] private Transform floatingCanvas;
        [SerializeField] private float spawnDistance = 1.25f;
        [SerializeField] private float heightOffset = -0.05f;

        private void Awake()
        {
            ApplyCameraSettings();
        }

        private void Start()
        {
            ApplyCameraSettings();
            TryEnableMetaPassthrough();
            CleanupVRBackgroundObjects();

            StartCoroutine(RecenterUICanvasRoutine());
        }

        /// <summary>
        /// Configures Main Camera and URP Camera Data for true 0-alpha transparency.
        /// </summary>
        public void ApplyCameraSettings()
        {
            try
            {
                if (targetCamera == null)
                {
                    targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
                }

                if (targetCamera != null)
                {
                    targetCamera.clearFlags = CameraClearFlags.SolidColor;
                    targetCamera.backgroundColor = clearColor;

                    var urpCamData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                    if (urpCamData != null)
                    {
                        urpCamData.renderPostProcessing = false;
                        urpCamData.renderShadows = true;
                    }

                    Debug.Log($"[MRPassthroughController] Camera '{targetCamera.name}' configured for alpha transparency.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MRPassthroughController] Camera setup notice: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely configures Meta Quest Insight Passthrough without locking editor threads.
        /// </summary>
        public void TryEnableMetaPassthrough()
        {
            if (!enablePassthroughOnStart) return;

            try
            {
                var ovrManager = FindFirstObjectByType<OVRManager>();
                if (ovrManager != null)
                {
                    ovrManager.isInsightPassthroughEnabled = true;
                    Debug.Log("[MRPassthroughController] OVRManager.isInsightPassthroughEnabled = true.");
                }

                if (targetCamera == null)
                {
                    targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
                }

                if (targetCamera != null)
                {
                    var passthroughLayer = targetCamera.GetComponent<OVRPassthroughLayer>();
                    if (passthroughLayer != null)
                    {
                        passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
                        passthroughLayer.hidden = false;
                        passthroughLayer.enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MRPassthroughController] Passthrough activation notice: {ex.Message}");
            }
        }

        /// <summary>
        /// Disables skybox and opaque VR background meshes that block real-world visibility.
        /// </summary>
        public void CleanupVRBackgroundObjects()
        {
            try
            {
                RenderSettings.skybox = null;

                foreach (var objName in objectsToHide)
                {
                    if (string.IsNullOrEmpty(objName)) continue;
                    var found = GameObject.Find(objName);
                    if (found != null)
                    {
                        found.SetActive(false);
                        Debug.Log($"[MRPassthroughController] Disabled VR background object: '{objName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MRPassthroughController] VR cleanup notice: {ex.Message}");
            }
        }

        private IEnumerator RecenterUICanvasRoutine()
        {
            yield return null;
            yield return null;

            if (floatingCanvas != null && targetCamera != null)
            {
                Vector3 headForward = targetCamera.transform.forward;
                headForward.y = 0f;
                headForward.Normalize();

                if (headForward != Vector3.zero)
                {
                    Vector3 targetPos = targetCamera.transform.position + headForward * spawnDistance;
                    targetPos.y += heightOffset;
                    floatingCanvas.position = targetPos;

                    Vector3 lookDir = floatingCanvas.position - targetCamera.transform.position;
                    lookDir.y = 0f;
                    if (lookDir != Vector3.zero)
                    {
                        floatingCanvas.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }
    }
}
