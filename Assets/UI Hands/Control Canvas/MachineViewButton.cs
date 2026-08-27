using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iXR.Machines
{
    public class MachineViewButton : BaseViewButtons
    {
        public override void SelectView()
        {
            base.SelectView();
            Debug.Log($"Selected View {transform.name}");
            GetComponent<Animator>().SetTrigger("Selected");
            m_view?.Init();
            state = ButtonState.SELECTED;
            // ModuleController.Instance.currentView = buttonView;
        }

        public override void HideView(Action callback)
        {
            base.HideView(callback);
            Debug.Log($"Hiding {buttonView} View");
            m_view.Hide(callback);
            state = ButtonState.NORMAL;
            GetComponent<Animator>().SetTrigger("Normal");
        }
    }

}
