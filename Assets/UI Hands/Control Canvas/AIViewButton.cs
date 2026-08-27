using iXR.Machines;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIViewButton : BaseViewButtons
{
    [SerializeField] private GameObject aiModule;

    public override void SelectView()
    {
        base.SelectView();
        Debug.Log("Toggle AI");
        aiModule.SetActive(!aiModule.activeSelf);
    }
}
