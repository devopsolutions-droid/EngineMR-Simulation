using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using EngineMR.XR;

namespace EngineMR.Editor
{
    public static class ConvertHomeSceneToMRTool
    {
        [MenuItem("Meta MR/Convert Current Scene to MR Passthrough", false, 10)]
        public static void ConvertCurrentSceneToMR()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Convert Scene to MR Passthrough");

            // 1. Configure Main Camera
            var mainCam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                Undo.RecordObject(mainCam, "Configure Camera for Passthrough");
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                EditorUtility.SetDirty(mainCam);

                // Configure URP Additional Camera Data
                var urpCamData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (urpCamData != null)
                {
                    Undo.RecordObject(urpCamData, "Configure URP Camera Data");
                    urpCamData.renderPostProcessing = false;
                    urpCamData.renderShadows = true;
                    EditorUtility.SetDirty(urpCamData);
                }

                // Ensure OVRPassthroughLayer is attached
                var passthroughLayer = mainCam.GetComponent<OVRPassthroughLayer>();
                if (passthroughLayer == null)
                {
                    passthroughLayer = Undo.AddComponent<OVRPassthroughLayer>(mainCam.gameObject);
                }
                passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
                passthroughLayer.hidden = false;
                EditorUtility.SetDirty(passthroughLayer);
                Debug.Log($"[ConvertHomeSceneToMRTool] Main Camera '{mainCam.name}' configured with SolidColor (0,0,0,0) and OVRPassthroughLayer.");
            }
            else
            {
                Debug.LogWarning("[ConvertHomeSceneToMRTool] No Camera found in scene!");
            }

            // 2. Ensure OVRManager exists in the scene
            var ovrManager = Object.FindFirstObjectByType<OVRManager>();
            if (ovrManager == null)
            {
                var ovrGO = new GameObject("[OVRManager]");
                Undo.RegisterCreatedObjectUndo(ovrGO, "Create [OVRManager]");
                ovrManager = Undo.AddComponent<OVRManager>(ovrGO);
            }

            Undo.RecordObject(ovrManager, "Configure OVRManager Passthrough");
            ovrManager.isInsightPassthroughEnabled = true;
            ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            EditorUtility.SetDirty(ovrManager);
            Debug.Log("[ConvertHomeSceneToMRTool] OVRManager configured with Insight Passthrough Enabled.");

            // 3. Remove Skybox Material
            RenderSettings.skybox = null;

            // 4. Disable VR background environment objects
            string[] objectsToDisable = new string[] { "Decor", "Background- Homescene", "VR  Hall", "Skybox" };
            foreach (var name in objectsToDisable)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Undo.RecordObject(go, "Disable VR Background Object");
                    go.SetActive(false);
                    EditorUtility.SetDirty(go);
                    Debug.Log($"[ConvertHomeSceneToMRTool] Disabled VR background object: '{name}'");
                }
            }

            // 5. Create / Ensure [MR_Controller] GameObject exists with MRPassthroughController
            var mrController = GameObject.Find("[MR_Controller]");
            if (mrController == null)
            {
                mrController = new GameObject("[MR_Controller]");
                Undo.RegisterCreatedObjectUndo(mrController, "Create [MR_Controller]");
            }

            var mrPass = mrController.GetComponent<MRPassthroughController>();
            if (mrPass == null)
            {
                mrPass = Undo.AddComponent<MRPassthroughController>(mrController);
            }

            // Look for Universal Canvas to assign
            var universalCanvas = GameObject.Find("Universal Canvas");
            if (universalCanvas != null)
            {
                var field = typeof(MRPassthroughController).GetField("floatingCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(mrPass, universalCanvas.transform);
            }

            EditorUtility.SetDirty(mrController);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"<color=green>[ConvertHomeSceneToMRTool] Scene '{activeScene.name}' is now fully converted to MR Passthrough!</color>");
            EditorUtility.DisplayDialog("MR Conversion Complete", 
                $"Scene '{activeScene.name}' is now configured for Meta Quest Mixed Reality Passthrough!\n\n• OVRManager: Added & Insight Passthrough Enabled\n• Camera ClearFlags: SolidColor (0,0,0,0)\n• OVRPassthroughLayer: Added (Underlay)\n• VR Skybox/Background: Cleared", 
                "OK");
        }
    }
}
