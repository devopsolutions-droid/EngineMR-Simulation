using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks a runtime blade group created by a BladeSpin step.
/// Stores the temp parent GameObject, its TurbineBladeRotator, and each blade's
/// original parent so everything can be restored on reversal or cleanup.
/// </summary>
public class RuntimeBladeGroup
{
    public GameObject tempParent;
    public TurbineBladeRotator rotator;
    public List<(GameObject blade, Transform originalParent)> originalParents;

    public RuntimeBladeGroup(GameObject tempParent, TurbineBladeRotator rotator,
        List<(GameObject, Transform)> originalParents)
    {
        this.tempParent = tempParent;
        this.rotator = rotator;
        this.originalParents = originalParents;
    }
}