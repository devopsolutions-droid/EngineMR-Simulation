// This script acts as a persistent registry for engine inspection offset coordinates.
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EngineInspectionConfig", menuName = "Engine VR/Engine Inspection Config")]
public class EngineInspectionConfig : ScriptableObject
{
    public List<EngineInspectionEntry> engineConfigs = new List<EngineInspectionEntry>();
}

[System.Serializable]
public class EngineInspectionEntry
{
    public EngineData engineData;
    public Vector3 inspectionLocalPosition;
}
