#pragma warning disable 618

using System.Collections;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace UnityEngine.Reflect
{
    public class ARModeTopMenu : TopMenu
    {
        enum Mode
        {
            On_Screen, 
            AR_Tabletop, 
            AR_Immersive, 
            VR
        }

        enum VRState
        {
            Disabled, 
            Invalid, 
            Inactive, 
            Ready, 
            Unknown, 
            Unsupported, 
            UserNotPresent
        }

        const string STATUS_FORMAT = "Status: <color=#{0}>{1}</color>";

        public ListControl m_ListControl;
        public TableTopTopMenu m_TableTopMenu;
        public VRSetup vrSetup;
        public Color defaultColor;
        public Color failColor;
        public Color partialColor;
        public Color successColor;
        public Sprite m_OnScreenImage;
        public Sprite m_TableTopImage;
        public Sprite m_ImmersiveImage;

        ListControlDataSource source = new ListControlDataSource();
        Mode currentMode = Mode.On_Screen;

        protected override void Start()
        {
            base.Start();

            StartCoroutine(CheckARAvailability());
            
            m_ListControl.SetDataSource(source);
            m_ListControl.onOpen += OnModeChanged;
        }

        protected void Update()
        {
            // auto update current mode since VR can switch without going through this menu
            if (vrSetup.IsVRModeEnabled ^ currentMode == Mode.VR)
            {
                currentMode = vrSetup.IsVRModeEnabled ? Mode.VR : Mode.On_Screen;
            }
        }

        IEnumerator CheckARAvailability()
        {
            if ((ARSession.state == ARSessionState.None) || (ARSession.state == ARSessionState.CheckingAvailability))
            {
                yield return ARSession.CheckAvailability();
            }
        }
        
        public override void OnClick()
        {
            FillMenu();
            
            //  align window with button
            Vector2 windowpos = m_ListControl.GetComponent<RectTransform>().offsetMin;
            windowpos.x = buttonBackground.GetComponent<RectTransform>().offsetMin.x;
            m_ListControl.GetComponent<RectTransform>().offsetMin = windowpos;

            base.OnClick();
        }

        void FillMenu()
        {
            if (source.GetItemCount() != 0)
            {
                source.Clear();
            }

            ListControlItemData onscreen = new ListControlItemData();
            onscreen.id = Mode.On_Screen.ToString();
            onscreen.title = "On Screen";
            onscreen.description = "View model on device screen";
            onscreen.image = m_OnScreenImage;
            onscreen.options = ListControlItemData.Option.Open;
            onscreen.enabled = currentMode.ToString() != onscreen.id;
            source.AddItem(onscreen);

            if (ARSession.state != ARSessionState.Unsupported)
            {
                ListControlItemData tabletop = new ListControlItemData();
                tabletop.id = Mode.AR_Tabletop.ToString();
                tabletop.title = "Tabletop AR";
                tabletop.description = $"Walk around a small-scale model in augmented reality\n{GetARStateMessage()}";
                tabletop.image = m_TableTopImage;
                tabletop.options = ListControlItemData.Option.Open;
                tabletop.enabled = currentMode.ToString() != tabletop.id;
                source.AddItem(tabletop);
            }

#if IMMERSIVE_AR
                    
                ListControlItemData immersive = new ListControlItemData();
                immersive.id = Mode.AR_Immersive.ToString();
                immersive.title = "Immersive AR";
                immersive.description = "Walk inside a large-scale model in augmented reality";
                immersive.image = m_ImmersiveImage;
                immersive.options = ListControlItemData.Option.Open;
                immersive.enabled = false;
                source.AddItem(immersive);
                
#endif

#if !(UNITY_IPHONE || UNITY_ANDROID)
            ListControlItemData vr = new ListControlItemData();
            vr.id = Mode.VR.ToString();
            vr.title = "Headset VR";
            vr.description = $"Explore a model in virtual reality\n{GetVRStateMessage()}";
            vr.image = m_ImmersiveImage;
            vr.options = ListControlItemData.Option.Open;
            vr.enabled = currentMode.ToString() != vr.id && 
                XRDevice.userPresence == UserPresenceState.Present && 
                vrSetup.AreAllVRDevicesValid();
            source.AddItem(vr);
#endif
        }
        
        public void OnModeChanged(ListControlItemData data)
        {
            button.image.sprite = data.image;
            Deactivate();

            currentMode = (Mode)System.Enum.Parse(typeof(Mode), data.id);

            if (data.id == Mode.On_Screen.ToString())
            {
                vrSetup.EnableVR(false);
                ShowButtons();
                m_TableTopMenu.Deactivate();
                m_TableTopMenu.LeaveAR();
            }
            else if (data.id == Mode.AR_Tabletop.ToString())
            {
                m_TableTopMenu.Activate();
                HideButtons();
            }
            else if (data.id == Mode.VR.ToString())
            {
                vrSetup.EnableVR(true);
                HideButtons();
            }
            else
            {
                Debug.Log("Unsupported mode");
            }
        }

        public void OnCancel()
        {
            Deactivate();
        }

        string GetXRStateMessage(Color color, string message)
        {
            return string.Format(STATUS_FORMAT, ColorUtility.ToHtmlStringRGB(color), message);
        }

        string GetARStateMessage()
        {
            Color textColor;
            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                case ARSessionState.NeedsInstall:
                    textColor = failColor;
                    break;
                case ARSessionState.CheckingAvailability:
                case ARSessionState.Installing:
                case ARSessionState.SessionInitializing:
                    textColor = partialColor;
                    break;
                case ARSessionState.Ready:
                case ARSessionState.SessionTracking:
                    textColor = successColor;
                    break;
                default:
                    textColor = defaultColor;
                    break;
            }
            return GetXRStateMessage(textColor, ARSession.state.ToString());
        }

        string GetVRStateMessage()
        {
            if (!XRSettings.enabled)
            {
                return GetXRStateMessage(failColor, VRState.Disabled.ToString());
            }
            else if (!vrSetup.AreAllVRDevicesValid())
            {
                XRNode node = vrSetup.GetFirstInvalidNode().Value;
                return GetXRStateMessage(failColor, $"{node.ToString()} {VRState.Invalid.ToString()}");
            }
            else if (!XRSettings.isDeviceActive)
            {
                return GetXRStateMessage(partialColor, VRState.Inactive.ToString());
            }
            else
            {
                switch (XRDevice.userPresence)
                {
                    case UserPresenceState.NotPresent:
                        return GetXRStateMessage(partialColor, VRState.UserNotPresent.ToString());
                    case UserPresenceState.Present:
                        return GetXRStateMessage(successColor, VRState.Ready.ToString());
                    case UserPresenceState.Unsupported:
                        return GetXRStateMessage(failColor, VRState.Unsupported.ToString());
                    default:
                        return GetXRStateMessage(failColor, VRState.Unknown.ToString());
                }
            }
        }
    }
}
