using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tools → [Guide] → Tool Workflow Guide
///
/// Baby-step guide for integrating a new engine into the project.
/// Each tool card includes an Open button to launch it directly.
/// </summary>
public class ToolWorkflowGuide : EditorWindow
{
    private Vector2 _scroll;

    // ── Menu ───────────────────────────────────────────────────────────────────

    [MenuItem("Tools/[Guide] Tool Workflow Guide")]
    public static void Open() => GetWindow<ToolWorkflowGuide>("Engine Integration Guide");

    // ── Window ─────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        minSize = new Vector2(620, 550);
        maxSize = new Vector2(900, 1200);
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(8);
        DrawHeader("ENGINE INTEGRATION — STEP BY STEP", 14, true);
        EditorGUILayout.Space(4);

        DrawBody(
            "Follow these steps in order to integrate a new engine model. " +
            "Each step unlocks the next feature set. Take your time — " +
            "every step is important.");
        EditorGUILayout.Space(12);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 1
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❶  STEP 1 — Group Engine Parts");
        DrawToolCard(
            "Engine Part Grouping Tool",
            "Tools/Engine Part Grouping Tool",
            () => EditorWindow.GetWindow<EnginePartGroupingTool>("Part Grouping"));
        DrawBody(
            "Open the tool and select your engine's root GameObject. " +
            "Group the children into logical categories: Blades, Covers, " +
            "Shafts, FuelInjectors, Wires, etc. This keeps the hierarchy " +
            "clean and makes the next tool easier to process.");
        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 2
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❷  STEP 2 — Run Engine Part Setup Tool");
        DrawToolCard(
            "Engine Part Setup Tool",
            "Tools/Engine Part Setup",
            () => EditorWindow.GetWindow<EnginePartSetupTool>("Engine Part Setup"));
        DrawBulletList(
            "Adds Rigidbody (kinematic) + MeshCollider (convex) to every part",
            "Adds EnginePart, EnginePartVisuals, EnginePartExplode components",
            "Generates PartData ScriptableObject assets per part",
            "Creates the EnginePartManifest asset linking part names ↔ PartData"
        );

        EditorGUILayout.Space(4);
        DrawSubHeader("✅ What this unlocks:");
        DrawBulletList(
            "Engine Grabbing — grab parts in VR with the hands",
            "X-Ray Vision — see through engine with the scanning ring effect",
            "Outline Highlighting — hover glow and selection outline on parts",
            "Runtime Button — a button is automatically created in the Main Menu " +
                "on the Home Screen for this engine (only after adding to the registry)"
        );
        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 3
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❸  STEP 3 — Set Up X-Ray Vision");
        DrawToolCard(
            "X-Ray Vision Setup",
            "Tools/XRay Vision Setup",
            () => EditorWindow.GetWindow<XRaySetupTool>("XRay Vision Setup"));
        DrawBody(
            "Select your engine root, then run this tool. It creates the " +
            "X-Ray material, adds the XRayVisionController to your engine, " +
            "and wires it into the EngineViewManager and EngineInteractor. " +
            "One click and x-ray works.");
        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 4
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❹  STEP 4 — Generate Part Names & Descriptions (AI)");
        DrawToolCard(
            "Groq Part Description Generator",
            "Tools/Groq Part Description Generator",
            () => EditorWindow.GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions"));
        DrawBody(
            "Open the tool, load your engine prefab, and let the AI " +
            "generate meaningful names and descriptions for each part. " +
            "The tool extracts geometry data, batches parts, and calls " +
            "the Groq API. Results are written directly into your " +
            "PartData assets.");
        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 5
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❺  STEP 5 — Create the Dismantled Engine Copy");
        DrawSubHeader("What is this?");
        DrawBody(
            "A dismantled engine is a duplicate where every part is " +
            "manually positioned in its 'exploded' / taken-apart position. " +
            "When the user clicks Explode View in the tablet, each part " +
            "animates to this exact position instead of auto-calculating. " +
            "This gives you full control over the exploded look.");
        EditorGUILayout.Space(4);
        DrawSubHeader("How to create it:");
        DrawBulletList(
            "Duplicate your engine root GameObject in the hierarchy",
            "Rename it \"EngineName Dismantled\" (e.g. \"Jet Engine Dismantled\")",
            "Move each part child to its desired exploded position",
            "Keep this GameObject in the scene (it can stay inactive)",
            "Add it to the \"Dismantled Scene Root\" field in EngineViewManager"
        );
        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 6
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❻  STEP 6 — Register the Engine");
        DrawSubHeader("A — Add to Engine Registry");
        DrawBody(
            "Find or create the EngineRegistry asset in your project. " +
            "Add your new EngineData asset to the engines list. " +
            "This makes the engine appear on the Home Screen so users " +
            "can select it from the main menu.");
        EditorGUILayout.Space(4);
        DrawSubHeader("B — Add to Engine Scene Loader");
        DrawBody(
            "In the Engine View scene, select the EngineSceneLoader GameObject. " +
            "In the Inspector, add a new entry to the \"Engine Entries\" array. " +
            "Fill in:");
        DrawBulletList(
            "Engine Data — your EngineData asset",
            "Scene Root — the engine's root GameObject in the hierarchy",
            "Dismantled Scene Root — (optional) the dismantled copy you created"
        );
        EditorGUILayout.Space(4);
        DrawSubHeader("Location reference:");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "EngineSceneLoader (in scene)\n" +
            "  └─ Engine Entries [ ]                  ◄── add new entry\n" +
            "        ├─ Engine Data                    ◄── your EngineData asset\n" +
            "        ├─ Scene Root                     ◄── engine GameObject in scene\n" +
            "        └─ Dismantled Scene Root           ◄── dismantled copy (optional)",
            EditorStyles.miniLabel
        );
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        DrawSubHeader("C — Add to EngineViewManager");
        DrawBody(
            "Make sure the EngineViewManager in the scene has the Dismantled " +
            "Scene Root field assigned (the copy you made in Step 5). " +
            "This ensures the Explode button animates to your custom positions.");
        EditorGUILayout.Space(12);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 7 — Engine Remover
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❼  STEP 7 — Engine Remover (when you need to remove an engine)");
        DrawToolCard(
            "Engine Remover Tool",
            "Tools/Engine Remover",
            () => EditorWindow.GetWindow<EngineRemoverTool>("Engine Remover"));
        DrawBody(
            "When you need to completely remove an engine from the project, " +
            "use this tool. It handles everything:");
        DrawBulletList(
            "Removes the EngineData from the EngineRegistry",
            "Deletes all PartData assets and the EnginePartManifest",
            "Deletes the engine prefab and all related files",
            "Removes the engine entry from the EngineSceneLoader in the scene"
        );
        EditorGUILayout.Space(12);

        // ──────────────────────────────────────────────────────────────────────
        // STEP 8 — Engine List Generator
        // ──────────────────────────────────────────────────────────────────────
        DrawStep("❽  STEP 8 — Engine List Generator (Sequential & Indexed)");
        DrawToolCard(
            "Engine List Generator",
            "Tools/Engine List Generator",
            () => EditorWindow.GetWindow<EngineListGenerator>("Engine List Generator"));
        DrawBody(
            "Generates a sequential, indexed list (#01, #02, #03...) of all engine models in the project. " +
            "Supports sorting (Alphabetical, Registry, Category), one-click sync to EngineRegistry, " +
            "and exports to Markdown, Plain Text, or Clipboard.");
        EditorGUILayout.Space(12);

        // ──────────────────────────────────────────────────────────────────────
        // TROUBLESHOOTING
        // ──────────────────────────────────────────────────────────────────────
        DrawHeader("TROUBLESHOOTING", 12, true);
        DrawBulletList(
            "Engine not appearing on Home Screen? → Check EngineRegistry.engines list",
            "Grab not working? → Did you run the Engine Part Setup Tool? (Step 2)",
            "X-Ray not working? → Run X-Ray Vision Setup (Step 3)",
            "Outline not showing? → Run Engine Part Setup Tool again (Step 2)",
            "Explode looking wrong? → Create a Dismantled copy (Step 5)",
            "Need to remove an engine? → Use the Engine Remover Tool (Step 7)"
        );

        EditorGUILayout.Space(8);

        // ── Footer ──────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            "Need help?  Check the docs/ folder or ask the team.",
            EditorStyles.centeredGreyMiniLabel,
            GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    private void DrawHeader(string text, int fontSize, bool bold)
    {
        var style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = Color.white }
        };
        EditorGUILayout.LabelField(text, style, GUILayout.ExpandWidth(true));
    }

    private void DrawStep(string text)
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.4f, 0.8f, 1f) }
        };
        EditorGUILayout.LabelField(text, style);
    }

    private void DrawSubHeader(string text)
    {
        var style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.8f, 0.9f, 1f) }
        };
        EditorGUILayout.LabelField(text, style);
    }

    private void DrawBody(string text)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            wordWrap = true,
            richText = true
        };
        EditorGUILayout.LabelField(text, style, GUILayout.ExpandWidth(true));
    }

    private void DrawToolCard(string name, string menuPath, System.Action openAction)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🔧", GUILayout.Width(20));
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(name, EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(menuPath, EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
        if (GUILayout.Button("Open", GUILayout.Width(56), GUILayout.Height(36)))
            openAction?.Invoke();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBulletList(params string[] items)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            wordWrap = true,
            richText = true
        };

        foreach (var item in items)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  •", GUILayout.Width(14));
            EditorGUILayout.LabelField(item, style, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }
    }
}