using System.Collections;
using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    public class DisplayModeTopMenu : TopMenu
    {
        public ListControl m_ListControl;

        readonly ListControlDataSource source = new ListControlDataSource();
        readonly List<IDisplayMode> displayModes = new List<IDisplayMode>();
        IDisplayMode currentDisplayMode = null;

        protected override void Start()
        {
            base.Start();

            StartCoroutine(DetectDisplayModes());
            
            m_ListControl.SetDataSource(source);
            m_ListControl.onOpen += OnModeChanged;
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

            foreach (IDisplayMode displayMode in displayModes)
            {
                displayMode.RefreshStatus();
                if (displayMode.IsAvailable)
                {
                    source.AddItem(displayMode.ListControlItemData);
                }
            }
        }

        protected IEnumerator DetectDisplayModes()
        {
            GameObject[] rootObjects = SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObject in rootObjects)
            {
                displayModes.AddRange(rootObject.GetComponentsInChildren<IDisplayMode>(true));
            }
            displayModes.Sort((a, b) => a.MenuOrderPriority - b.MenuOrderPriority);

            for (int i = displayModes.Count - 1; i >= 0; --i)
            {
                yield return displayModes[i].CheckAvailability();
                if (!displayModes[i].IsAvailable)
                {
                    displayModes.RemoveAt(i);
                }
            }

            if (currentDisplayMode == null)
            {
                OnModeChanged(displayModes[0].ListControlItemData);
            }
        }
        
        public void OnModeChanged(ListControlItemData data)
        {
            button.image.sprite = data.image;
            Deactivate();

            if (currentDisplayMode != null)
            {
                currentDisplayMode.OnModeEnabled(false, source);
            }
            foreach (IDisplayMode displayMode in displayModes)
            {
                if (displayMode.ListControlItemData.id == data.id)
                {
                    displayMode.OnModeEnabled(true, source);
                    currentDisplayMode = displayMode;
                    break;
                }
            }
        }

        public void OnCancel()
        {
            Deactivate();
        }
    }
}
