using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace iXR.Machines
{
    public abstract class BaseMachineView : MonoBehaviour
    {
        [SerializeField] private MachineViews m_view;
        [SerializeField] private bool enableLocomotionOnView = false;
        [SerializeField] private bool enableRotationOnView = false;
        [SerializeField] private float movementSpeed = 10f;
        [SerializeField] private float rotateSpeed = 30f;
        [SerializeField] private bool setUserPositionOnView = true;
        [SerializeField] private Transform UserPositionOnViewTransform;
        public MachineViews View { get => m_view; set => m_view = value; }
        [Space]
        [SerializeField] protected UnityEvent startViewActions;
        [SerializeField] protected UnityEvent endViewActions;
        
        protected virtual void OnEnable()
        {
            
        }

        protected virtual void OnDisable()
        {
            
        }
        
        public virtual void Init()
        {
            if (MachineUser.Instance != null)
            {
                MachineUser.Instance.EnableLocomotion(enableLocomotionOnView, movementSpeed);
                MachineUser.Instance.EnableRotation(enableRotationOnView, rotateSpeed);
                if (setUserPositionOnView && UserPositionOnViewTransform != null)
                    MachineUser.Instance.MoveUserToTarget(UserPositionOnViewTransform);
            }
            startViewActions?.Invoke();
        }

        public virtual void Hide(Action callback)
        {
            endViewActions?.Invoke();
        }
        public abstract void ResetView();
    }
}
