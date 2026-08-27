using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iXR.Machines
{
    public static class MachineEventSystem
    {
        public delegate void MachineEvents();
        public delegate void MachineEvents<T>(T sender);
        public delegate void MachineEvents<T1, T2>(T1 sender, T2 obj);

        public static event MachineEvents<MachineViews> OnMachineViewSelected;
        public static event MachineEvents<int> OnPartSelected;
        public static event MachineEvents<int> OnPartHighlight;
        public static event MachineEvents<int> OnShowXrayLayer;
        public static event MachineEvents<bool> OnPartGrabSelected;
        public static event MachineEvents<bool> OnPartInteractionSelected;
        public static event MachineEvents<int, Transform> OnLabelClicked;
        public static event MachineEvents<int> OnStepStarted;
        public static event MachineEvents<int> OnPartInfoClosed;
        public static event MachineEvents OnThemeSwitch;
        public static event MachineEvents OnDoorsDeactivate;
        public static event MachineEvents<int> OnXrayPartChanged;

        public static void SelectMachineView(MachineViews view) => OnMachineViewSelected?.Invoke(view);
        public static void SelectPart(int index) => OnPartSelected?.Invoke(index);
        public static void HighlightPart(int index) => OnPartHighlight?.Invoke(index);
        public static void ShowXRayLayer(int layerIndex) => OnShowXrayLayer?.Invoke(layerIndex);
        public static void TogglePartsGrab(bool isOn) => OnPartGrabSelected?.Invoke(isOn);
        public static void TogglePartsInteraction(bool isOn) => OnPartInteractionSelected?.Invoke(isOn);
        public static void ShowLabelInfo(int index, Transform infoPos = null) => OnLabelClicked?.Invoke(index, infoPos);
        public static void StartStep(int index) => OnStepStarted?.Invoke(index);
        public static void ClosePartInfo(int index) => OnPartInfoClosed?.Invoke(index);
        public static void SwitchTheme() => OnThemeSwitch?.Invoke();
        public static void DeactivateDoors() => OnDoorsDeactivate?.Invoke();
        public static void XrayPartChanged(int index) => OnXrayPartChanged?.Invoke(index);
    }

    [System.Serializable]
    public enum MachineViews
    {
        NONE,
        INTERACTION,
        EXPLODE,
        XRAY,
        WORKING
    }
}
