using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace iXR.Machines
{
    public class ControlCanvas : MonoBehaviour
    {
        #region Instance
        public static ControlCanvas Instance;
        private void Awake()
        {
            if(Instance == null)
                Instance = this;
        }
        #endregion

        [SerializeField] private InputActionProperty toggleCanvas;
        [SerializeField] private ActionBasedController _leftController;

        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject controlButtonsContainer;
        bool checkScroll = false;
        public int selected, highlighted;
        [SerializeField] private List<BaseViewButtons> viewBtns;
        [SerializeField] private AudioClip scrollClip;
        
        
        private void OnEnable()
        {
            toggleCanvas.action.performed += ToggleCanvas;
            _leftController.activateAction.action.started += ViewSelected;
            _leftController.uiScrollAction.action.started += ScrollMenus;

            MachineEventSystem.OnMachineViewSelected += MachineViewSelected;
        }

        private void OnDisable()
        {
            toggleCanvas.action.performed -= ToggleCanvas;
            _leftController.activateAction.action.started -= ViewSelected;
            _leftController.uiScrollAction.action.started -= ScrollMenus;

            MachineEventSystem.OnMachineViewSelected -= MachineViewSelected;
        }

        private void Start()
        {
            ToggleCanvas(false);
            checkScroll = false;
            menuButton.gameObject.SetActive(false);
        }

        #region Toggle Canvas
        
        public void SetupCanvas()
        {
            StartCoroutine(IESetupCanvas());
        }

        IEnumerator IESetupCanvas()
        {
            Debug.Log("Setting Up Canvas");
            menuButton.gameObject.SetActive(true);
            for (int i = 0; i < viewBtns.Count; i++)
            {
                viewBtns[i].buttonIndex = i;
                if (viewBtns[i].m_view?.View == MachineViews.INTERACTION)
                {
                    selected = i;
                    highlighted = i;
                    viewBtns[i].state = ButtonState.SELECTED;
                    viewBtns[i].GetComponent<Animator>().SetTrigger("Selected");
                }
            }

            Debug.Log($"selected {selected}");
            foreach (BaseViewButtons btn in viewBtns)
                btn.UnhighighlightView();
            viewBtns[selected].state = ButtonState.HIGHLIGHTED;
            viewBtns[selected].SelectView();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            checkScroll = false;
        }
        public void ToggleCanvas(bool isOn)
        {
            viewBtns[highlighted].GetComponent<Animator>().SetTrigger(isOn ? "Selected" : "Normal");
            highlighted = selected;
            menuButton.gameObject.SetActive(!isOn);
            controlButtonsContainer.transform.localScale = isOn ? Vector3.one : Vector3.zero;
        }

        private void ToggleCanvas(InputAction.CallbackContext context)
        {
            // if (ModuleController.Instance.moduleStarted)
            // {
                checkScroll = !checkScroll;
                ToggleCanvas(checkScroll);
            // }
        }

        private void ScrollMenus(InputAction.CallbackContext context)
        {

            Debug.Log($"Scroll Canvas : {checkScroll}");
            if (checkScroll)
            {

                checkScroll = false;
                StartCoroutine(IEScrollMenu());
            }
        }

        IEnumerator IEScrollMenu()
        {

            // AudioManager.instance.PlaySFX(scrollClip);
            yield return new WaitForEndOfFrame();
            if(_leftController.uiScrollValue.y < 0)
            {

                highlighted++;
                highlighted = Mathf.Clamp(highlighted, 0, viewBtns.Count - 1);
                if (highlighted == selected)
                    highlighted = highlighted == viewBtns.Count - 1 ? viewBtns.Count - 2 : highlighted + 1;
            }
            else
            {

                highlighted--;
                highlighted = Mathf.Clamp(highlighted, 0, viewBtns.Count - 1);
                if (highlighted == selected)
                    highlighted = highlighted == 0 ? 1 : highlighted - 1;
            }

            foreach(BaseViewButtons btn in viewBtns)
                btn.HighlightView(highlighted);
            //viewBtns[highlighted].HighlightView();
            checkScroll = true;
        }

        private void ViewSelected(InputAction.CallbackContext context)
        {

            if (highlighted == selected)
            {
                return;
            }

            if (viewBtns[highlighted].GetType() == typeof(MachineViewButton))
            {
                Debug.Log("Machine Views Selected");
                checkScroll = false;
                foreach (BaseViewButtons view in viewBtns)
                {
                    if (viewBtns[highlighted].GetType() == typeof(MachineViewButton) && view.state == ButtonState.SELECTED)
                    {
                        view.HideView(() => StartCoroutine(IESelectView()));
                        break;
                    }
                }
            }
            else if (viewBtns[highlighted].GetType() == typeof(ResetButton))
            {
                viewBtns[selected].m_view.ResetView();
                foreach (BaseViewButtons viewBtn in viewBtns)
                    viewBtn.UnhighighlightView();
                viewBtns[selected].HighlightView();
                highlighted = selected;
            }
            else if(viewBtns[highlighted].GetType() == typeof(SettingsButtonView))
            {
                Debug.Log("Settings Button Called");
                viewBtns[highlighted].SelectView();
            }
            else if (viewBtns[highlighted].GetType() == typeof(QuitViewButton))
            {
                Debug.Log("Quit Application Called");
                viewBtns[highlighted].SelectView();
            }
            
        }

        IEnumerator IESelectView()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            viewBtns[highlighted].SelectView();
            selected = highlighted;
        }

        private void MachineViewSelected(MachineViews sender)
        {
            if (sender != MachineViews.NONE)
            {
                checkScroll = true;
            }
        }

        #endregion

    }

}
