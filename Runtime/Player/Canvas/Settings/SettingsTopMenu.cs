using UnityEngine.Rendering.PostProcessing;

namespace UnityEngine.Reflect
{
    public class SettingsTopMenu : TopMenu
    {
        public enum Quality
        {
            BetterLooking,
            MoreResponsive
        }
        
        public ListControl m_ListControl;
        public Camera[] m_PostProcessCameras;
        public Quality m_Quality;
        public Sprite m_BetterLookingImage;
        public Sprite m_MoreResponsiveImage;

        ListControlDataSource m_Source = new ListControlDataSource();
        ListControlItemData m_BetterLookItem = new ListControlItemData();
        ListControlItemData m_FasterItem = new ListControlItemData();
        
        protected override void Start()
        {
            base.Start();

            m_ListControl.SetDataSource(m_Source);
            m_ListControl.onOpen += OnSettingsChanged;
            
            Application.lowMemory += OnLowMemory;
        }

        void OnLowMemory()
        {
            if (m_Quality == Quality.BetterLooking)
            {
                Debug.Log("Reducing visual quality due to memory warning.");
                SetQuality(Quality.MoreResponsive);
            }
        }
        
        public override void OnClick()
        {
            FillMenu();

            base.OnClick();
        }

        void FillMenu()
        {
            if (m_Source.GetItemCount() == 0)
            {
                m_BetterLookItem.id = "nice";
                m_BetterLookItem.title = "Better Looking";
                m_BetterLookItem.description = "Make your model shine using ambient occlusion, bloom, depth of field, and more.";
                m_BetterLookItem.image = m_BetterLookingImage;
                m_BetterLookItem.options = ListControlItemData.Option.Open;
                m_BetterLookItem.enabled = true;
                m_BetterLookItem.selected = (m_Quality == Quality.BetterLooking);
                m_Source.AddItem(m_BetterLookItem);

                m_FasterItem.id = "fast";
                m_FasterItem.title = "More Responsive";
                m_FasterItem.description = "Remove all visual effects and improve frame rate.";
                m_FasterItem.image = m_MoreResponsiveImage;
                m_FasterItem.options = ListControlItemData.Option.Open;
                m_FasterItem.enabled = true;
                m_FasterItem.selected = (m_Quality == Quality.MoreResponsive);
                m_Source.AddItem(m_FasterItem);
             
                //  align window with button
                Vector2 windowpos = m_ListControl.GetComponent<RectTransform>().offsetMin;
                windowpos.x = buttonBackground.GetComponent<RectTransform>().offsetMin.x;
                m_ListControl.GetComponent<RectTransform>().offsetMin = windowpos;
            }
        }
        
        public void OnSettingsChanged(ListControlItemData data)
        {
            Deactivate();

            if (data.id == "nice")
            {
                SetQuality(Quality.BetterLooking);
            }
            else if (data.id == "fast")
            {
                SetQuality(Quality.MoreResponsive);
            }
            else
            {
                Debug.Log("Unsupported quality setting");
            }
            ShowButtons();
        }

        public void SetQuality(Quality quality)
        {
            m_Quality = quality;
            bool better = (quality == Quality.BetterLooking);
            
            // safety check in case quality settings are changed from outside this menu before initialization
            if (m_Source != null && 
                !string.IsNullOrEmpty(m_BetterLookItem.id) && 
                !string.IsNullOrEmpty(m_FasterItem.id))
            {
                m_BetterLookItem.selected = better;
                m_Source.UpdateItem(m_BetterLookItem);
                m_FasterItem.selected = !better;
                m_Source.UpdateItem(m_FasterItem);
            }

            foreach (var cam in m_PostProcessCameras)
            {
//                cam.renderingPath = better ? RenderingPath.DeferredShading : RenderingPath.Forward;
                
                var layer = cam.GetComponent<PostProcessLayer>();
                if (layer != null)
                {
                    layer.enabled = better;
                }
                
                var volume = cam.GetComponent<PostProcessVolume>();
                if (volume != null)
                {
                    volume.enabled = better;
                }
            }
        }
        
        public void OnCancel()
        {
            Deactivate();
            ShowButtons();
        }
    }
}
