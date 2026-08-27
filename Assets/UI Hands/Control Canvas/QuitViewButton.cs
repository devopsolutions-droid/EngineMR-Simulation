using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iXR.Machines
{
    public class QuitViewButton : BaseViewButtons
    {
        public override void SelectView()
        {
            Application.Quit();
            //if (state == ButtonState.HIGHLIGHTED)
            //{
            //}
        }

        public override void HideView(Action callback)
        {

        }

        public override void ResetView()
        {

        }
    }

}
