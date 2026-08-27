using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Tools > Attach Hover Panels
/// 
/// Automatically instantiates and configures hover panels for all parts of an engine model.
/// </summary>
public class AttachHoverPanelsTool : EditorWindow
{
    private GameObject _engineModel;
    private GameObject _hoverPanelTemplate;
    private Vector3 _defaultPanelOffset = new Vector3(0.3f, 0.3f, 0f);
    private bool _overwriteExisting = true;
    private bool _setTMPText = true;

    [MenuItem("Tools/Attach Hover Panels")]
    public static void Open() => GetWindow<AttachHoverPanelsTool>("Attach Hover Panels");

    void OnGUI()
    {
        GUILayout.Label("Attach Hover Panels Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _engineModel = (GameObject)EditorGUILayout.ObjectField(
            "Engine GameObject (Root)", _engineModel, typeof(GameObject), true);

        _hoverPanelTemplate = (GameObject)EditorGUILayout.ObjectField(
            "Hover Panel Template / Prefab", _hoverPanelTemplate, typeof(GameObject), true);

        _defaultPanelOffset = EditorGUILayout.Vector3Field("Panel Offset", _defaultPanelOffset);

        EditorGUILayout.Space(4);
        _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing Panels", _overwriteExisting);
        _setTMPText = EditorGUILayout.Toggle("Set TMP Text to Part Name", _setTMPText);

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Attach Hover Panels", GUILayout.Height(30)))
        {
            ProcessEngineParts();
        }
    }

    private void ProcessEngineParts()
    {
        if (_engineModel == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign an Engine Model / Root GameObject.", "OK");
            return;
        }

        if (_hoverPanelTemplate == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Hover Panel Template / Prefab.", "OK");
            return;
        }

        EnginePart[] parts = _engineModel.GetComponentsInChildren<EnginePart>(true);
        if (parts.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No EnginePart components found in the hierarchy of the selected Engine Model.", "OK");
            return;
        }

        int attachedCount = 0;
        int overwrittenCount = 0;

        foreach (var part in parts)
        {
            if (part == null) continue;

            // Register undo for the part component and its GameObject
            Undo.RegisterCompleteObjectUndo(part, "Attach Hover Panel");

            // Check if there is an existing hover panel
            if (part.hoverPanel != null)
            {
                if (!_overwriteExisting)
                {
                    continue; // Skip if we don't want to overwrite
                }

                // If it is in the hierarchy of the part, destroy it
                if (part.hoverPanel.transform.IsChildOf(part.transform))
                {
                    Undo.DestroyObjectImmediate(part.hoverPanel);
                    overwrittenCount++;
                }
            }
            else
            {
                // Fallback check: see if there is any child object already named "*Hover Panel" or containing PartHoverPanel script
                PartHoverPanel existingScript = part.GetComponentInChildren<PartHoverPanel>(true);
                if (existingScript != null)
                {
                    if (!_overwriteExisting)
                    {
                        part.hoverPanel = existingScript.gameObject;
                        EditorUtility.SetDirty(part);
                        continue;
                    }
                    Undo.DestroyObjectImmediate(existingScript.gameObject);
                    overwrittenCount++;
                }
            }

            // Instantiate hover panel template
            GameObject newPanel;
            if (PrefabUtility.IsPartOfPrefabAsset(_hoverPanelTemplate))
            {
                newPanel = PrefabUtility.InstantiatePrefab(_hoverPanelTemplate, part.transform) as GameObject;
            }
            else
            {
                newPanel = Instantiate(_hoverPanelTemplate, part.transform);
            }

            if (newPanel == null) continue;

            newPanel.name = $"{part.PartName} Hover Panel";
            newPanel.transform.localPosition = Vector3.zero;
            newPanel.transform.localRotation = Quaternion.identity;
            newPanel.transform.localScale = Vector3.one;

            // Configure PartHoverPanel component
            PartHoverPanel hoverScript = newPanel.GetComponent<PartHoverPanel>();
            if (hoverScript == null)
            {
                hoverScript = newPanel.AddComponent<PartHoverPanel>();
            }

            // Remove any LineRenderer component to ensure no orange connecting line is drawn
            LineRenderer lr = newPanel.GetComponent<LineRenderer>();
            if (lr != null)
            {
                Undo.DestroyObjectImmediate(lr);
            }

            hoverScript.panelOffset = _defaultPanelOffset;

            // Look for PanelAnchor child on part, fallback to part itself
            Transform anchor = part.transform.Find("PanelAnchor");
            if (anchor == null) anchor = part.transform;
            hoverScript.partAnchor = anchor;

            // Set TextMeshPro / TextMeshProUGUI text if enabled
            if (_setTMPText)
            {
                var texts = newPanel.GetComponentsInChildren<TextMeshPro>(true);
                foreach (var t in texts)
                {
                    t.text = part.PartName;
                    EditorUtility.SetDirty(t);
                }

                var uiTexts = newPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in uiTexts)
                {
                    t.text = part.PartName;
                    EditorUtility.SetDirty(t);
                }
            }

            // Assign to part
            part.hoverPanel = newPanel;

            // Register undo for creation
            Undo.RegisterCreatedObjectUndo(newPanel, "Create Hover Panel");
            EditorUtility.SetDirty(part);

            attachedCount++;
        }

        // Force scene and assets saving/refreshing
        EditorUtility.SetDirty(_engineModel);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", 
            $"Attached hover panels to {attachedCount} parts successfully!\nOverwrote {overwrittenCount} old panels.", "OK");
    }
}
