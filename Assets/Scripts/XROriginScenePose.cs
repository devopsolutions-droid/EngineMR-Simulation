using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enforces strict Position and Rotation (Euler) for the XR Origin (or attached GameObject)
/// whenever a scene starts, shifts, or loads.
/// Attach this script directly to your XR Origin in both scenes.
/// </summary>
[DisallowMultipleComponent]
public class XROriginScenePose : MonoBehaviour
{
    [System.Serializable]
    public struct ScenePoseData
    {
        [Tooltip("Exact scene name (e.g. 'EngineButtons HomeScene' or 'Main Scene').")]
        public string sceneName;

        [Tooltip("Target world position for the XR Origin in this scene.")]
        public Vector3 position;

        [Tooltip("Target world rotation (Euler angles: X, Y, Z) for the XR Origin in this scene.")]
        public Vector3 rotationEuler;
    }

    [Header("Default Target Pose (Fallback)")]
    [Tooltip("If checked, applies default position and rotation unless overridden by Scene Settings below.")]
    public bool overrideDefaultPose = true;
    public Vector3 defaultPosition = Vector3.zero;
    public Vector3 defaultRotationEuler = new Vector3(0f, -70f, 0f);

    [Header("Scene-Specific Poses")]
    [Tooltip("Define custom position and rotation per scene name.")]
    public List<ScenePoseData> scenePoses = new List<ScenePoseData>();

    private void Awake()
    {
        ApplyPoseForCurrentScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ApplyPoseForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyPoseForCurrentScene();
    }

    /// <summary>
    /// Forces strict application of position and rotation for the current active scene.
    /// Can also be called manually via code or UnityEvents.
    /// </summary>
    public void ApplyPoseForCurrentScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        // 1. Check if a scene-specific pose exists for the active scene
        if (scenePoses != null)
        {
            foreach (var poseData in scenePoses)
            {
                if (!string.IsNullOrEmpty(poseData.sceneName) &&
                    string.Equals(poseData.sceneName.Trim(), activeSceneName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ApplyPose(poseData.position, poseData.rotationEuler);
                    Debug.Log($"[XROriginScenePose] Applied scene-specific pose for '{activeSceneName}': Pos={poseData.position}, Rot={poseData.rotationEuler}");
                    return;
                }
            }
        }

        // 2. Fallback to default pose if enabled
        if (overrideDefaultPose)
        {
            ApplyPose(defaultPosition, defaultRotationEuler);
            Debug.Log($"[XROriginScenePose] Applied default pose for '{activeSceneName}': Pos={defaultPosition}, Rot={defaultRotationEuler}");
        }
    }

    private void ApplyPose(Vector3 pos, Vector3 rotEuler)
    {
        transform.position = pos;
        transform.rotation = Quaternion.Euler(rotEuler);
    }
}
