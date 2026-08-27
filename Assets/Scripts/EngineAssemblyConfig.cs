using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach this component to the root GameObject of an Engine Prefab (e.g. Jet Engine, Steam Engine)
/// to define its specific reassembly steps, part groups, and explosion overrides.
/// 
/// This keeps each engine model self-contained and allows new engines to be added 
/// with zero modification to the Main Scene or other scripts.
/// </summary>
public class EngineAssemblyConfig : MonoBehaviour
{
    [Header("Step-by-Step Assembly Order")]
    [Tooltip("Define the assembly sequence for this specific engine prefab.")]
    public AssemblyStep[] assemblySteps;

    [Header("Part Grouping")]
    [Tooltip("Group multiple engine parts together (e.g. blades, flywheels) so they hover and grab/snap as a single unit.")]
    public List<EnginePartGroupData> partGroups;

    [Header("Explode Position Overrides")]
    [Tooltip("Define exact local positions parts should animate to in Grab Mode (e.g. specific caps or valves).")]
    public EngineViewManager.ExplodeOverride[] explodeOverrides;

#if UNITY_EDITOR
    [ContextMenu("📁 Organize Hierarchy into Group Nodes")]
    public void OrganizeHierarchyIntoGroupNodes()
    {
        if (partGroups == null || partGroups.Count == 0) return;
        foreach (var group in partGroups)
        {
            if (group.parts == null || group.parts.Count == 0) continue;

            Transform existingParent = transform.Find(group.groupName);
            GameObject parentGO = existingParent != null ? existingParent.gameObject : new GameObject(group.groupName);

            UnityEditor.Undo.RegisterCreatedObjectUndo(parentGO, "Create Group Parent");
            parentGO.transform.SetParent(transform, worldPositionStays: false);

            Vector3 avgPos = Vector3.zero;
            int count = 0;
            foreach (var p in group.parts)
            {
                if (p != null) { avgPos += p.transform.position; count++; }
            }
            if (count > 0) parentGO.transform.position = avgPos / count;

            foreach (var p in group.parts)
            {
                if (p != null)
                {
                    UnityEditor.Undo.SetTransformParent(p.transform, parentGO.transform, "Reparent Part to Group Node");
                }
            }
        }
        Debug.Log($"[EngineAssemblyConfig] Organized {partGroups.Count} groups into Hierarchy nodes.");
    }

    [ContextMenu("🔍 Select All Grouped Parts in Hierarchy")]
    public void SelectAllGroupedPartsInHierarchy()
    {
        if (partGroups == null || partGroups.Count == 0) return;
        List<GameObject> allGOs = new List<GameObject>();
        foreach (var group in partGroups)
        {
            if (group.parts != null)
            {
                foreach (var p in group.parts)
                {
                    if (p != null && !allGOs.Contains(p.gameObject))
                        allGOs.Add(p.gameObject);
                }
            }
        }
        if (allGOs.Count > 0)
        {
            UnityEditor.Selection.objects = allGOs.ToArray();
            Debug.Log($"[EngineAssemblyConfig] Selected {allGOs.Count} parts in Hierarchy.");
        }
    }
#endif
}
