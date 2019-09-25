
using System.Collections;
using UnityEngine.XR.ARFoundation;

namespace UnityEngine.Reflect
{
    public class ARModeTopMenu : TopMenu
    {
        public ListControl listControl;
        public TableTopTopMenu tableTopMenu;

        ListControlDataSource source = new ListControlDataSource();

        protected override void Start()
        {
            base.Start();

            StartCoroutine(CheckARAvailability());
            
            listControl.SetDataSource(source);
            listControl.onOpen += OnModeChanged;
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
            Vector2 windowpos = listControl.GetComponent<RectTransform>().offsetMin;
            windowpos.x = buttonBackground.GetComponent<RectTransform>().offsetMin.x;
            listControl.GetComponent<RectTransform>().offsetMin = windowpos;

            base.OnClick();
        }

        void FillMenu()
        {
            if (source.GetItemCount() == 0)
            {
                bool aravailable = (ARSession.state != ARSessionState.Unsupported);

                ListControlItemData onscreen = new ListControlItemData();
                onscreen.id = "onscreen";
                onscreen.title = "On Screen";
                onscreen.description = "View model on device screen";
                onscreen.image = Resources.Load<Sprite>("Textures/onscreen");
                onscreen.options = ListControlItemData.Option.Open;
                onscreen.enabled = aravailable;
                source.AddItem(onscreen);

                ListControlItemData tabletop = new ListControlItemData();
                tabletop.id = "tabletop";
                tabletop.title = "Tabletop AR";
                tabletop.description = "Walk around a small-scale model in augmented reality";
                tabletop.image = Resources.Load<Sprite>("Textures/tabletop-ar");
                tabletop.options = ListControlItemData.Option.Open;
                tabletop.enabled = aravailable;
                source.AddItem(tabletop);

                #if IMMERSIVE_AR
                    
                ListControlItemData immersive = new ListControlItemData();
                immersive.id = "immersive";
                immersive.title = "Immersive AR";
                immersive.description = "Walk inside a large-scale model in augmented reality";
                immersive.image = Resources.Load<Sprite>("Textures/immersive-ar");
                immersive.options = ListControlItemData.Option.Open;
                immersive.enabled = false;
                source.AddItem(immersive);
                
                #endif
            }
        }
        
        public void OnModeChanged(ListControlItemData data)
        {
            button.image.sprite = data.image;
            Deactivate();

            if (data.id == "onscreen")
            {
                ShowButtons();
                tableTopMenu.Deactivate();
                tableTopMenu.LeaveAR();
            }
            else if (data.id == "tabletop")
            {
                tableTopMenu.Activate();
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
    }
}
