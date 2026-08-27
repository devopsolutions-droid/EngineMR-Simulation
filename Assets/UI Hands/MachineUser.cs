using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using static UnityEngine.GraphicsBuffer;

namespace iXR.Machines
{
    public class MachineUser : MonoBehaviour
    {
        #region Instance
        public static MachineUser Instance;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        #endregion

        [SerializeField] private XROrigin userOrigin;

        private ActionBasedContinuousMoveProvider m_contineousMoveProvider;
        private ActionBasedContinuousTurnProvider m_contineousTurnProvider;
        [Space]
        [SerializeField] private bool setUserPositionOnView = true;
        [SerializeField] private float yValue;

        private void OnEnable()
        {

        }

        private void OnDisable()
        {

        }

        private void Start()
        {
            if (userOrigin == null)
                userOrigin = FindObjectOfType<XROrigin>();
            if (m_contineousMoveProvider == null)
                m_contineousMoveProvider = FindObjectOfType<ActionBasedContinuousMoveProvider>();
            if (m_contineousTurnProvider == null)
                m_contineousTurnProvider = FindObjectOfType<ActionBasedContinuousTurnProvider>();
            EnableLocomotion(false);
            EnableRotation(false);
        }

        public void MoveUserToGround()
        {
            userOrigin.transform.position = new Vector3(userOrigin.transform.position.x, yValue, userOrigin.transform.position.z);
            userOrigin.transform.rotation = userOrigin.transform.rotation;
        }
        public void MoveUserToTarget(Transform target)
        {
            userOrigin.transform.position = target.position;
            userOrigin.transform.rotation = target.rotation;
        }

        public void EnableLocomotion(bool locomotion, float moveSpeed = 10f)
        {
            Debug.Log($"Enable Locomotion {locomotion}");
            m_contineousMoveProvider.moveSpeed = locomotion ? moveSpeed : 0;
        }

        public void EnableRotation(bool rotation, float rotateSpeed = 30f)
        {
            Debug.Log($"Enable Rotation {rotation}");
            m_contineousTurnProvider.turnSpeed = rotation ? rotateSpeed : 0;
        }
    }

}
