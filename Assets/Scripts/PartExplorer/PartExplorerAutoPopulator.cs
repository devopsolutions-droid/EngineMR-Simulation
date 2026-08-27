using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Automatically populates PartExplorerData from existing EnginePartManifest.
/// This script reads the part data you already have and creates the explorer data.
/// Can auto-find the engine root if not assigned.
/// </summary>
public class PartExplorerAutoPopulator : MonoBehaviour
{
    [SerializeField] private EnginePartManifest enginePartManifest;
    [SerializeField] private PartExplorerData explorerData;
    
    [Header("Scene References")]
    [SerializeField] private Transform engineRoot;
    
    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindEngineRoot = true;
    [SerializeField] private string engineRootSearchName = "V8HotRed"; // Adjust if needed

    /// <summary>
    /// Call this to populate the explorer data from the manifest.
    /// Can be called from Editor or at runtime.
    /// </summary>
    public void PopulateExplorerData()
    {
        if (enginePartManifest == null)
        {
            Debug.LogError("PartExplorerAutoPopulator: No EnginePartManifest assigned!");
            return;
        }
        
        if (explorerData == null)
        {
            Debug.LogError("PartExplorerAutoPopulator: No PartExplorerData assigned!");
            return;
        }
        
        // Try to find engine root if not assigned
        if (engineRoot == null && autoFindEngineRoot)
        {
            engineRoot = FindEngineRoot();
            if (engineRoot == null)
            {
                Debug.LogError("PartExplorerAutoPopulator: Could not find engine root! Assign it manually or check the search name.");
                return;
            }
            Debug.Log($"PartExplorerAutoPopulator: Auto-found engine root: {engineRoot.name}");
        }
        
        if (engineRoot == null)
        {
            Debug.LogError("PartExplorerAutoPopulator: No engine root assigned and auto-find is disabled!");
            return;
        }
        
        // Clear existing parts
        explorerData.parts.Clear();
        
        // Get all PartEntry from manifest
        var manifestField = enginePartManifest.GetType().GetField("parts", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (manifestField == null)
        {
            Debug.LogError("PartExplorerAutoPopulator: Could not access manifest parts!");
            return;
        }
        
        var partsList = manifestField.GetValue(enginePartManifest) as List<EnginePartManifest.PartEntry>;
        
        if (partsList == null || partsList.Count == 0)
        {
            Debug.LogWarning("PartExplorerAutoPopulator: No parts found in manifest!");
            return;
        }
        
        int addedCount = 0;
        int skippedCount = 0;
        
        // For each part in manifest
        foreach (var entry in partsList)
        {
            if (entry.partData == null) continue;
            
            // Find the EnginePart in the scene
            Transform partTransform = engineRoot.Find(entry.gameObjectName);
            if (partTransform == null)
            {
                Debug.LogWarning($"PartExplorerAutoPopulator: Could not find part '{entry.gameObjectName}' in scene!");
                skippedCount++;
                continue;
            }
            
            EnginePart enginePart = partTransform.GetComponent<EnginePart>();
            if (enginePart == null)
            {
                Debug.LogWarning($"PartExplorerAutoPopulator: Part '{entry.gameObjectName}' has no EnginePart component!");
                skippedCount++;
                continue;
            }
            
            // Create explorer part entry
            var explorerPart = new PartExplorerData.ExplorerPart
            {
                partName = entry.partData.partName,
                partDescription = entry.partData.description,
                enginePart = enginePart
            };
            
            explorerData.parts.Add(explorerPart);
            addedCount++;
            
            Debug.Log($"✓ Added part: {entry.partData.partName}");
        }
        
        Debug.Log($"PartExplorerAutoPopulator: Successfully added {addedCount} parts to explorer data! (Skipped: {skippedCount})");
    }

    /// <summary>
    /// Try to find the engine root in the scene.
    /// </summary>
    private Transform FindEngineRoot()
    {
        // First, try to find by name
        GameObject found = GameObject.Find(engineRootSearchName);
        if (found != null)
            return found.transform;
        
        // Try to find any active engine root
        var allRoots = FindObjectsOfType<Transform>();
        foreach (var root in allRoots)
        {
            // Look for objects with EnginePart children
            var engineParts = root.GetComponentsInChildren<EnginePart>();
            if (engineParts.Length > 0)
            {
                Debug.Log($"Found engine root with {engineParts.Length} parts: {root.name}");
                return root;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Clear all parts from explorer data.
    /// </summary>
    public void ClearExplorerData()
    {
        if (explorerData != null)
        {
            explorerData.parts.Clear();
            Debug.Log("PartExplorerAutoPopulator: Cleared explorer data!");
        }
    }
}
