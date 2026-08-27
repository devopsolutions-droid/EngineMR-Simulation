using System;
using UnityEngine;
using EngineMR.Common;
using EngineMR.Environment;
using EngineMR.Engine;
using EngineMR.Anchoring;

namespace EngineMR.Placement
{
    /// <summary>
    /// Coordinates surface scanning, ghost preview display, and instantiating the 3D Engine model onto physical surfaces.
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        public static PlacementManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private SurfaceDetector surfaceDetector;
        [SerializeField] private PlacementPreview placementPreview;
        [SerializeField] private GameObject enginePrefab;

        [Header("State")]
        [SerializeField] private PlacementState currentState = PlacementState.Searching;

        public PlacementState CurrentState => currentState;
        public GameObject PlacedEngineInstance { get; private set; }

        public event Action<PlacementState> OnStateChanged;
        public event Action<GameObject> OnEnginePlaced;
        public event Action OnPlacementReset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SetState(PlacementState.Searching);
        }

        private void Update()
        {
            HandlePlacementFlow();
            HandleInput();
        }

        private void HandlePlacementFlow()
        {
            if (currentState == PlacementState.Placed) return;

            if (surfaceDetector == null) return;

            SurfaceHitResult hit = surfaceDetector.DetectSurface();

            if (hit.IsValid)
            {
                if (currentState != PlacementState.Previewing)
                {
                    SetState(PlacementState.Previewing);
                }

                if (placementPreview != null)
                {
                    Transform head = Camera.main != null ? Camera.main.transform : null;
                    placementPreview.UpdatePose(hit.Position, hit.Normal, head);
                }
            }
            else
            {
                if (currentState != PlacementState.Searching)
                {
                    SetState(PlacementState.Searching);
                }

                if (placementPreview != null)
                {
                    placementPreview.SetVisible(false);
                }
            }
        }

        private void HandleInput()
        {
            // Trigger placement when in preview mode and user clicks / pinches (space or mouse for editor testing / primary button)
            if (currentState == PlacementState.Previewing)
            {
                bool triggerPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
                
                #if META_XR_SDK || OCULUS
                triggerPressed |= OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);
                #endif

                if (triggerPressed)
                {
                    PlaceEngine();
                }
            }
        }

        /// <summary>
        /// Spawns the physical 3D engine prefab at the current preview location and attaches spatial anchoring.
        /// </summary>
        public void PlaceEngine()
        {
            if (enginePrefab == null || placementPreview == null)
            {
                Debug.LogError("[PlacementManager] Cannot place engine: Missing prefab or preview reference!");
                return;
            }

            Vector3 spawnPos = placementPreview.transform.position;
            Quaternion spawnRot = placementPreview.transform.rotation;

            PlacedEngineInstance = Instantiate(enginePrefab, spawnPos, spawnRot);

            // Register with EngineController
            var controller = PlacedEngineInstance.GetComponent<EngineController>();
            if (controller == null)
            {
                controller = PlacedEngineInstance.AddComponent<EngineController>();
            }
            controller.InitializeEngine(spawnPos, spawnRot, PlacedEngineInstance.transform.localScale);

            // Attach Spatial Anchor
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.AnchorObject(PlacedEngineInstance);
            }

            // Hide Preview & switch state
            placementPreview.SetVisible(false);
            SetState(PlacementState.Placed);

            OnEnginePlaced?.Invoke(PlacedEngineInstance);
            Debug.Log("[PlacementManager] 3D Engine successfully placed and anchored on real surface.");
        }

        /// <summary>
        /// Resets the placed engine and returns to surface searching mode.
        /// </summary>
        public void ResetPlacement()
        {
            if (PlacedEngineInstance != null)
            {
                Destroy(PlacedEngineInstance);
                PlacedEngineInstance = null;
            }

            SetState(PlacementState.Searching);
            OnPlacementReset?.Invoke();
            Debug.Log("[PlacementManager] Placement reset. Searching for new surface.");
        }

        private void SetState(PlacementState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
