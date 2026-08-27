using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iXR.Machines
{
    public class SettingsButtonView : BaseViewButtons
    {
        [SerializeField] private GameObject stripe1;
        [SerializeField] private GameObject stripe2;

        [SerializeField] private string motionName;
        [SerializeField]
        public List<Cubemap> _skybox_Textures = new List<Cubemap>();
        public Material _skybox_mat;
        int currentSB;

        private void OnEnable()
        {
            MachineEventSystem.OnThemeSwitch += ChangeSkybox_SettingsButton_Method;
            MachineEventSystem.OnDoorsDeactivate += DeactivateGameObject;
        }

        private void OnDisable()
        {
            MachineEventSystem.OnThemeSwitch -= ChangeSkybox_SettingsButton_Method;
            MachineEventSystem.OnDoorsDeactivate -= DeactivateGameObject;
        }

        private void Start()
        {
            currentSB = 0;
        }

        public void ChangeSkybox_SettingsButton_Method()
        {
            currentSB = (currentSB + 1) % _skybox_Textures.Count;
            _skybox_mat.SetTexture("_Tex", _skybox_Textures[currentSB]); // UnityEngine.Random.Range(0, _skybox_Textures.Count)]);
        }

        public void DeactivateGameObject()
        {
            stripe1.SetActive(false);
            stripe2.SetActive(false);
        }
        public override void SelectView()
        {
            Debug.Log("Switching Skybox");
            stripe1.SetActive(true);
            stripe2.SetActive(true);
            stripe1.GetComponent<Animator>().Play(motionName);
            stripe2.GetComponent<Animator>().Play(motionName);
        }

        public override void HideView(Action callback)
        {
            
        }


        public override void ResetView()
        {

        }
    }

}
