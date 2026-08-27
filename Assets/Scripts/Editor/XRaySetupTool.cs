using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > XRay Vision Setup
///
/// One click does everything:
///   1. Creates the XRayVision material from the shader
///   2. Adds XRayVisionController to the EngineViewManager GameObject
///   3. Assigns the engine root + material on the controller
///   4. Wires the controller into EngineViewManager.xRayController
///   5. Wires the controller into EngineInteractor.xRayController
/// </summary>
public class XRaySetupTool : EditorWindow
{
    private const string ShaderName   = "Custom/XRayVision";
    private const string MaterialPath = "Assets/Common/XrayMaterials/XRayVision.mat";

    private EngineViewManager _viewManager;
    private EngineInteractor  _interactor;
    private Transform         _engineRoot;

    [MenuItem("Tools/XRay Vision Setup")]
    public static void Open() => GetWindow<XRaySetupTool>("XRay Vision Setup");

    void OnEnable()
    {
        // Auto-find scene references on open
        _viewManager = FindFirstObjectByType<EngineViewManager>();
        _interactor  = FindFirstObjectByType<EngineInteractor>();
    }

    void OnGUI()
    {
        GUILayout.Label("XRay Vision Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _viewManager = (EngineViewManager)EditorGUILayout.ObjectField(
            "Engine View Manager", _viewManager, typeof(EngineViewManager), true);

        _interactor = (EngineInteractor)EditorGUILayout.ObjectField(
            "Engine Interactor", _interactor, typeof(EngineInteractor), true);

        _engineRoot = (Transform)EditorGUILayout.ObjectField(
            "Engine Root", _engineRoot, typeof(Transform), true);

        EditorGUILayout.Space(4);

        // Auto-fill engine root from EngineViewManager if not set
        if (_engineRoot == null && _viewManager != null && _viewManager.engineRoot != null)
        {
            _engineRoot = _viewManager.engineRoot;
            Repaint();
        }

        if (_viewManager == null)
            EditorGUILayout.HelpBox("EngineViewManager not found in scene. Open the engine scene first.", MessageType.Warning);
        if (_engineRoot == null)
            EditorGUILayout.HelpBox("Assign the engine root Transform (the root of your engine model).", MessageType.Warning);

        EditorGUILayout.Space(10);

        bool canRun = _viewManager != null && _engineRoot != null;
        GUI.enabled = canRun;

        if (GUILayout.Button("RUN XRAY SETUP  (one click)", GUILayout.Height(44)))
            Run();

        GUI.enabled = true;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "One click does everything:\n" +
            "  • Creates XRayVision material at Assets/Common/XrayMaterials/\n" +
            "  • Adds XRayVisionController to the EngineViewManager GameObject\n" +
            "  • Assigns engine root + material on the controller\n" +
            "  • Wires controller → EngineViewManager.xRayController\n" +
            "  • Wires controller → EngineInteractor.xRayController\n\n" +
            "Safe to re-run — skips steps already done.",
            MessageType.Info);
    }

    void Run()
    {
        Undo.SetCurrentGroupName("XRay Vision Setup");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Step 1: Ensure material exists ───────────────────────────────────
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    $"Could not find shader '{ShaderName}'.\n\n" +
                    "Make sure XRayVision.shader is in Assets/Shaders/ and has compiled.",
                    "OK");
                return;
            }

            // Ensure folder exists
            string dir = Path.GetDirectoryName(MaterialPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            mat = new Material(shader);
            mat.name = "XRayVision";
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[XRaySetup] Created material at {MaterialPath}");
        }

        // ── Step 2: Add XRayVisionController to EngineViewManager GameObject ─
        GameObject vmGO = _viewManager.gameObject;
        XRayVisionController controller = vmGO.GetComponent<XRayVisionController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<XRayVisionController>(vmGO);
            Debug.Log("[XRaySetup] Added XRayVisionController to EngineViewManager GameObject.");
        }

        // ── Step 3: Assign engine root + material on controller ───────────────
        Undo.RecordObject(controller, "XRay Setup");
        controller.targetRoot  = _engineRoot;
        controller.xRayMaterial = mat;
        EditorUtility.SetDirty(controller);

        // ── Step 4: Wire into EngineViewManager ───────────────────────────────
        Undo.RecordObject(_viewManager, "XRay Setup");
        _viewManager.xRayController = controller;
        EditorUtility.SetDirty(_viewManager);
        Debug.Log("[XRaySetup] Wired XRayVisionController → EngineViewManager.xRayController");

        // ── Step 5: Wire into EngineInteractor ────────────────────────────────
        if (_interactor != null)
        {
            Undo.RecordObject(_interactor, "XRay Setup");
            _interactor.xRayController = controller;
            EditorUtility.SetDirty(_interactor);
            Debug.Log("[XRaySetup] Wired XRayVisionController → EngineInteractor.xRayController");
        }
        else
        {
            Debug.LogWarning("[XRaySetup] EngineInteractor not found — assign it manually if needed.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("XRay Setup Complete ✓",
            "Everything is wired up:\n\n" +
            $"  • Material: {MaterialPath}\n" +
            $"  • XRayVisionController on: {vmGO.name}\n" +
            $"  • Engine Root: {_engineRoot.name}\n" +
            $"  • EngineViewManager.xRayController ✓\n" +
            $"  • EngineInteractor.xRayController {(_interactor != null ? "✓" : "⚠ not found — assign manually")}\n\n" +
            "Press Play and hit the XRay button to test.",
            "OK");
    }
}
