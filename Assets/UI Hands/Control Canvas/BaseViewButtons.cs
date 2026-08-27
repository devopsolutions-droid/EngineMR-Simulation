using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iXR.Machines
{
    public class BaseViewButtons : MonoBehaviour
    {
        public MachineViews buttonView;
        public BaseMachineView m_view;
        public int buttonIndex;
        public ButtonState state;

        public void HighlightView()
        {
            if (state == ButtonState.SELECTED)
                return;
            GetComponent<Animator>().SetTrigger("Highlighted");
            state = ButtonState.HIGHLIGHTED;
        }
        public void HighlightView(int index)
        {
            if (state == ButtonState.SELECTED)
                return;
            GetComponent<Animator>().SetTrigger(index == buttonIndex ? "Highlighted" : "Normal");
            state = index == buttonIndex ? ButtonState.HIGHLIGHTED : ButtonState.NORMAL;
        }
        public void UnhighighlightView()
        {
            if (state == ButtonState.SELECTED)
                return;
            GetComponent<Animator>().SetTrigger("Normal");
            state = ButtonState.NORMAL;
        }

        public virtual void SelectView()
        {
            
        }

        public virtual void HideView(Action callback)
        {
            
        }

        public virtual void ResetView()
        {

        }
    }

    public enum ButtonState
    {
        NORMAL,
        HIGHLIGHTED,
        SELECTED
    }
}
