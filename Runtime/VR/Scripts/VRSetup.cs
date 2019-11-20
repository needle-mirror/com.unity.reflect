using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEngine.XR;
using UnityEngine.Reflect.Controller;
using UnityEngine.Events;
using UnityEngine.SpatialTracking;

namespace UnityEngine.Reflect
{
    public class VRSetup : MonoBehaviour, 
        IUsesMoveCameraRig, 
        IUsesSpatialHash
    {
        public enum MenuActions
        {
            Projects, 
            ScreenMode, 
            Sync, 
            Filter, 
            Settings, 
            BimViewer, 
            MiniWorld
        }

        [SerializeField] protected GameObject m_EditingContextManagerPrefab;
        [SerializeField] protected Canvas m_UICanvas;
        [SerializeField] protected Vector2 m_UICanvasSize;
        [SerializeField] protected Transform m_InitialPlayerTransform;
        [SerializeField] protected FreeCamController m_FreeCamController;
        [SerializeField] protected MultipleRayInputModule m_MultipleRayInputModule;
        [SerializeField] protected SettingsTopMenu m_SettingsMenu;
        [SerializeField] protected bool m_AutoEnable;
        [SerializeField] protected List<GameObject> m_GameObjectsToEnable;
        [SerializeField] protected List<GameObject> m_GameObjectsToDisable;
        [SerializeField] protected UnityEvent m_MenuCancelAction;
        [SerializeField] protected List<UnityEvent> m_MenuActions;

        protected GameObject m_EditingContextManagerInstance;
        protected UserPresenceState m_UserPresenceState = UserPresenceState.NotPresent;
        protected bool m_Initialized;
        protected Transform m_InitialCanvasParent;
        protected Transform m_InitialCameraParent;
        protected Camera m_MainCamera;
        protected float m_InitialFOV;
        protected bool m_VRModeEnabled;
        protected SettingsTopMenu.Quality m_PreviousQuality;
        protected HideFlags m_PreviousHideFlags;

        InputDevice hmdInput, leftInput, rightInput;

        public bool IsVRModeEnabled { get { return m_VRModeEnabled; } }

        public IProvidesSpatialHash provider { get; set; }
        IProvidesMoveCameraRig IFunctionalitySubscriber<IProvidesMoveCameraRig>.provider { get; set; }

        protected void Start()
        {
            m_MainCamera = Camera.main;
            m_InitialFOV = m_MainCamera.fieldOfView;

            XRSettings.gameViewRenderMode = GameViewRenderMode.None;

            m_InitialCanvasParent = m_UICanvas.transform.parent;
            m_InitialCameraParent = m_FreeCamController.transform.parent;
        }

        protected void Update()
        {
            if (m_AutoEnable && m_UserPresenceState != XRDevice.userPresence)
            {
                m_UserPresenceState = XRDevice.userPresence;
                Debug.Log(string.Format("User presence: {0}", m_UserPresenceState.ToString()));
                EnableVR(m_UserPresenceState == UserPresenceState.Present);
            }

            if (m_VRModeEnabled)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    EnableVR(false);
                }
            }
            else
            {
                if (AreAllVRDevicesValid() && 
                    leftInput.TryGetFeatureValue(CommonUsages.triggerButton, out bool isLeftTriggerPressed) && 
                    isLeftTriggerPressed && 
                    rightInput.TryGetFeatureValue(CommonUsages.triggerButton, out bool isRightTriggerPressed) && 
                    isRightTriggerPressed)
                {
                    EnableVR(true);
                }
            }
        }

        protected void OnApplicationQuit()
        {
            if (Application.isEditor)
            {
                ResetCanvas();
            }
        }

        protected void OnDestroy()
        {
            Detach();
        }

        public void EnableVR(bool isEnabled)
        {
            if (m_VRModeEnabled == isEnabled)
            {
                return;
            }

            m_VRModeEnabled = isEnabled;

            if (isEnabled)
            {
                m_Initialized = true;

                m_PreviousQuality = m_SettingsMenu.m_Quality;
                m_SettingsMenu.SetQuality(SettingsTopMenu.Quality.MoreResponsive);

                m_EditingContextManagerInstance = Instantiate(m_EditingContextManagerPrefab, transform);

                FunctionalityInjectionModule.instance.activeIsland.InjectFunctionalitySingle(this);

                Attach();

                m_FreeCamController.enabled = false;
                m_MultipleRayInputModule.enabled = true;
                m_MainCamera.stereoTargetEye = StereoTargetEyeMask.Both;

                ShowModulesAndCameraRig(true);

                InitCameraTransform();

                // force all menus to close when entering VR
                TopMenu.s_CanShowButtons = false;
                m_MenuCancelAction.Invoke();
            }
            else
            {
                // early exit if VR hasn't been enabled yet
                if (!m_Initialized)
                {
                    return;
                }

                m_SettingsMenu.SetQuality(m_PreviousQuality);
                
                m_FreeCamController.enabled = true;
                m_MultipleRayInputModule.enabled = false;
                ResetCanvas();

                ShowModulesAndCameraRig(false);

                // we need to keep a reference to the grandparent because EditorXR deparents the camera rig when disabled
                Transform topParent = m_InitialCameraParent.parent;
                m_FreeCamController.gameObject.transform.SetParent(m_InitialCameraParent, false);
                m_FreeCamController.transform.localScale = Vector3.one;

                Detach();

                Destroy(m_EditingContextManagerInstance.gameObject);

                m_InitialCameraParent.SetParent(topParent);

                m_MainCamera.stereoTargetEye = StereoTargetEyeMask.None;
                m_MainCamera.fieldOfView = m_InitialFOV;
                TrackedPoseDriver trackedPoseDriver = m_MainCamera.GetComponent<TrackedPoseDriver>();
                if (trackedPoseDriver != null)
                {
                    Destroy(trackedPoseDriver);
                }

                TopMenu.s_CanShowButtons = true;
                m_SettingsMenu.ShowButtons();
            }
            
            for (int i = 0; i < m_GameObjectsToEnable.Count; ++i)
            {
                m_GameObjectsToEnable[i].SetActive(isEnabled);
            }
            for (int i = 0; i < m_GameObjectsToDisable.Count; ++i)
            {
                m_GameObjectsToDisable[i].SetActive(!isEnabled);
            }

            Debug.Log(string.Format("VR {0}abled!", isEnabled ? "en" : "dis"));
        }

        public Canvas SetupCanvas(Transform canvasParent)
        {
            if (m_UICanvas.transform.parent != canvasParent)
            {
                m_UICanvas.transform.SetParent(canvasParent);
                m_UICanvas.renderMode = RenderMode.WorldSpace;
                m_UICanvas.worldCamera = m_MultipleRayInputModule.eventCamera;
                if (m_UICanvas.transform is RectTransform rectTransform)
                {
                    rectTransform.pivot = Vector2.right * 0.5f;

                    rectTransform.anchorMin = Vector2.one * 0.5f;
                    rectTransform.anchorMax = Vector2.one * 0.5f;

                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;

                    rectTransform.localPosition = Vector3.zero;
                    rectTransform.localRotation = Quaternion.identity;
                    rectTransform.localScale = Vector3.one * 0.001f;

                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_UICanvasSize.x);
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, m_UICanvasSize.y);
                }
            }
            return m_UICanvas;
        }

        protected void ResetCanvas()
        {
            m_UICanvas.transform.SetParent(m_InitialCanvasParent);
            m_UICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        protected void InitCameraTransform()
        {
            this.MoveCameraRig(m_InitialPlayerTransform.position, m_InitialPlayerTransform.rotation * Vector3.forward);
        }

        protected void Attach()
        {
            SyncObjectBinding.OnCreated += this.AddToSpatialHash;
            SyncObjectBinding.OnDestroyed += this.RemoveFromSpatialHash;
        }

        protected void Detach()
        {
            SyncObjectBinding.OnCreated -= this.AddToSpatialHash;
            SyncObjectBinding.OnDestroyed -= this.RemoveFromSpatialHash;
        }

        public void InvokeMenuAction(int index)
        {
            m_MenuCancelAction.Invoke();
            if (0 <= index && index < m_MenuActions.Count)
            {
                m_MenuActions[index].Invoke();
            }
        }

        public bool AreAllVRDevicesValid()
        {
            return !GetFirstInvalidNode().HasValue;
        }

        public XRNode? GetFirstInvalidNode()
        {
            if (hmdInput == null || !hmdInput.isValid)
            {
                hmdInput = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (!hmdInput.isValid)
                {
                    return XRNode.Head;
                }
            }
            if (leftInput == null || !leftInput.isValid)
            {
                leftInput = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (!leftInput.isValid)
                {
                    return XRNode.LeftHand;
                }
            }
            if (rightInput == null || !rightInput.isValid)
            {
                rightInput = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (!rightInput.isValid)
                {
                    return XRNode.RightHand;
                }
            }

            return null;
        }

        protected void ShowModulesAndCameraRig(bool show)
        {
            GameObject moduleLoaderCoreParent = ModuleLoaderCore.instance.GetModuleParent();
            HideFlags newHideFlags = m_PreviousHideFlags;
            if (show)
            {
                newHideFlags = HideFlags.None;
                m_PreviousHideFlags = moduleLoaderCoreParent.hideFlags;
            }

            moduleLoaderCoreParent.SetHideFlagsRecursively(newHideFlags);

            Transform cameraRig = CameraUtils.GetCameraRig();
            if (cameraRig != null)
            {
                cameraRig.gameObject.SetHideFlagsRecursively(newHideFlags);
            }
        }
    }
}
