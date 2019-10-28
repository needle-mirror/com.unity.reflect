using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Menus;
using UnityEditor.Experimental.EditorVR.Proxies;
using UnityEditor.Experimental.EditorVR.Tools;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine.InputNew;

namespace UnityEngine.Reflect
{
    public class ReflectRadialMenu : MonoBehaviour, 
        IActions, 
        IActionsMenu, 
        ICustomActionMap, 
        IInstantiateUI, 
        IMenu, 
        IUsesConnectInterfaces, 
        IUsesControlHaptics, 
        IUsesCreateWorkspace, 
        IUsesHandedRayOrigin, 
        IUsesMenuOrigins, 
        IUsesRayVisibilitySettings, 
        IUsesRequestFeedback, 
        IUsesSelectTool
    {
        [SerializeField] protected ActionMap m_ActionMap;
        [SerializeField] protected RadialMenuUI m_RadialMenuPrefab;
        [SerializeField] protected HapticPulse m_ReleasePulse;
        [SerializeField] protected HapticPulse m_ButtonHoverPulse;
        [SerializeField] protected HapticPulse m_ButtonClickedPulse;
        [SerializeField] protected Transform m_CanvasParent;
        [SerializeField] protected Transform m_RadialParent;
        [SerializeField] protected float m_ActivationThreshold = 0.5f;
        
        readonly BindingDictionary m_Controls = new BindingDictionary();
        Transform m_RayOrigin;
        Canvas m_UICanvas;
        RadialMenuUI m_RadialMenuUI;
        VRSetup m_VRSetup;
        MenuHideFlags m_MenuHideFlags = MenuHideFlags.Hidden;
        
        public List<IAction> actions => menuActions.ConvertAll(x => x.action);

        List<ActionMenuData> m_MenuActions;
        public List<ActionMenuData> menuActions
        {
            get { return m_MenuActions; }
            set
            {
                m_MenuActions = value.FindAll(x => x.sectionName == "ReflectRadialMainMenu");

                if (m_RadialMenuUI)
                    m_RadialMenuUI.actions = m_MenuActions;
            }
        }

        public event Action<Transform> itemWasSelected;

        public ActionMap actionMap => m_ActionMap;

        public bool ignoreActionMapInputLocking => false;

        public MenuHideFlags menuHideFlags
        {
            get { return m_MenuHideFlags; }
            set
            {
                if (m_MenuHideFlags != value)
                {
                    m_MenuHideFlags = value;
                    bool isVisible = value != MenuHideFlags.Hidden;
                    if (isVisible)
                    {
                        m_UICanvas = m_VRSetup.SetupCanvas(m_CanvasParent);
                    }
                    if (m_UICanvas != null)
                    {
                        m_UICanvas.enabled = isVisible;
                    }
                    if (m_RadialMenuUI != null)
                    {
                        m_RadialMenuUI.visible = isVisible;
                    }
                }
            }
        }

        public GameObject menuContent => m_RadialMenuUI.gameObject;

        public Bounds localBounds => BoundsUtils.GetBounds(transform);

        public int priority => 1;

        IProvidesConnectInterfaces IFunctionalitySubscriber<IProvidesConnectInterfaces>.provider { get; set; }
        IProvidesControlHaptics IFunctionalitySubscriber<IProvidesControlHaptics>.provider { get; set; }
        IProvidesCreateWorkspace IFunctionalitySubscriber<IProvidesCreateWorkspace>.provider { get; set; }
        IProvidesRequestFeedback IFunctionalitySubscriber<IProvidesRequestFeedback>.provider { get; set; }
        IProvidesSelectTool IFunctionalitySubscriber<IProvidesSelectTool>.provider { get; set; }
        public IProvidesRayVisibilitySettings provider { get; set; }

        public Transform menuOrigin { get; set; }
        
        public Transform alternateMenuOrigin { get; set; }

        public Node node { internal get; set; }

        public Transform rayOrigin
        {
            get { return m_RayOrigin; }
            set
            {
                if (value != null)
                {
                    // arbitrarily high priority to ensure the ray/cone never appear while this menu is open
                    this.AddRayVisibilitySettings(value, this, false, false, 1000);
                    m_RayOrigin = value;
                }
            }
        }

        public void Init(Node node, Transform rayOrigin)
        {
            this.node = node;
            this.rayOrigin = rayOrigin;

            m_VRSetup = FindObjectOfType<VRSetup>();
            if (m_VRSetup != null)
            {
                m_UICanvas = m_VRSetup.SetupCanvas(m_CanvasParent);
            }

            m_RadialMenuUI = this.InstantiateUI(m_RadialMenuPrefab.gameObject, m_RadialParent, false, rayOrigin).GetComponent<RadialMenuUI>();
            m_RadialMenuUI.actions = menuActions;
            this.ConnectInterfaces(m_RadialMenuUI, rayOrigin); // Connect interfaces before performing setup on the UI
            m_RadialMenuUI.Setup();
            m_RadialMenuUI.buttonHovered += OnButtonHovered;
            m_RadialMenuUI.buttonClicked += OnButtonClicked;
            m_RadialMenuUI.buttonClickedNoHighlight += OnButtonClickedNoHighlight;

            InputUtils.GetBindingDictionaryFromActionMap(m_ActionMap, m_Controls);

            menuHideFlags = 0;

            StartCoroutine(HideDefaultMenusCR());
        }

        protected IEnumerator HideDefaultMenusCR()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            List<ToolsMenuButton> toolsMenuButtons = new List<ToolsMenuButton>();
            List<MainMenu> mainMenus = new List<MainMenu>();
            List<RadialMenu> radialMenus = new List<RadialMenu>();
            List<UndoMenu> undoMenus = new List<UndoMenu>();
            List<LocomotionTool> locomotionTools = new List<LocomotionTool>();

            GameObject[] rootObjects = SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            EditorXRMenuModule menuModule = ModuleLoaderCore.instance.GetModule<EditorXRMenuModule>();

            foreach (GameObject rootObject in rootObjects)
            {
                // hide the main menu button underneath the radial menu
                rootObject.GetComponentsInChildren(true, toolsMenuButtons);
                foreach (ToolsMenuButton toolsMenuButton in toolsMenuButtons)
                {
                    toolsMenuButton.gameObject.SetActive(false);
                }

                // disconnect the default main menu
                rootObject.GetComponentsInChildren(true, mainMenus);
                foreach (MainMenu mainMenu in mainMenus)
                {
                    this.ClearFeedbackRequests(mainMenu);
                    mainMenu.menuHideFlags = MenuHideFlags.Hidden;
                    mainMenu.gameObject.SetActive(false);
                    menuModule.DisconnectInterface(mainMenu);
                }

                // disconnect the default radial menu
                rootObject.GetComponentsInChildren(true, radialMenus);
                foreach (RadialMenu radialMenu in radialMenus)
                {
                    this.ClearFeedbackRequests(radialMenu);
                    radialMenu.menuHideFlags = MenuHideFlags.Hidden;
                    radialMenu.gameObject.SetActive(false);
                    menuModule.DisconnectInterface(radialMenu);
                }
                
                // disconnect the default undo menu
                rootObject.GetComponentsInChildren(true, undoMenus);
                foreach (UndoMenu undoMenu in undoMenus)
                {
                    this.ClearFeedbackRequests(undoMenu);
                    undoMenu.menuHideFlags = MenuHideFlags.Hidden;
                    undoMenu.gameObject.SetActive(false);
                    menuModule.DisconnectInterface(undoMenu);
                }
                
                // disable crawling, rotating and scaling since we use the MiniWorld for that instead
                rootObject.GetComponentsInChildren(true, locomotionTools);
                foreach (LocomotionTool locomotionTool in locomotionTools)
                {
                    locomotionTool.enableCrawling = false;
                    locomotionTool.enableRotating = false;
                    locomotionTool.enableScaling = false;
                    if (locomotionTool.node == Node.RightHand)
                    {
                        locomotionTool.ShowMainButtonFeedback();
                    }
                }
            }
        }

        public void ProcessInput(ActionMapInput input, ConsumeControlDelegate consumeControl)
        {
            ReflectMainMenuInput radialMenuInput = (ReflectMainMenuInput)input;
            if (radialMenuInput == null || m_MenuHideFlags == MenuHideFlags.Hidden)
            {
                this.ClearFeedbackRequests(this);
                return;
            }

            var inputDirection = radialMenuInput.navigate.vector2;

            if (inputDirection.magnitude > m_ActivationThreshold)
            {
                // Composite controls need to be consumed separately
                consumeControl(radialMenuInput.navigateX);
                consumeControl(radialMenuInput.navigateY);
                m_RadialMenuUI.buttonInputDirection = inputDirection;
            }
            else
            {
                m_RadialMenuUI.buttonInputDirection = Vector2.zero;
            }

            var selectControl = radialMenuInput.selectItem;
            m_RadialMenuUI.pressedDown = selectControl.wasJustPressed;
            if (m_RadialMenuUI.pressedDown)
            {
                consumeControl(selectControl);
            }

            if (selectControl.wasJustReleased)
            {
                this.Pulse(node, m_ReleasePulse);

                m_RadialMenuUI.SelectionOccurred();

                itemWasSelected?.Invoke(rayOrigin);

                consumeControl(selectControl);
            }

            if (radialMenuInput.blockAction2.isHeld)
            {
                consumeControl(radialMenuInput.blockAction2);
            }
            if (radialMenuInput.blockTrigger1.isHeld)
            {
                consumeControl(radialMenuInput.blockTrigger1);
            }
        }

        void OnButtonClicked()
        {
            this.Pulse(node, m_ButtonClickedPulse);
        }

        void OnButtonClickedNoHighlight()
        {
            m_VRSetup.InvokeMenuAction(-1);
        }

        void OnButtonHovered()
        {
            this.Pulse(node, m_ButtonHoverPulse);
        }
    }
}
