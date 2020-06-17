using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class TopMenu : MonoBehaviour
    {
        public Button button;
        public Transform buttonBackground;
        public Transform ui;
        public Button cancelScreenButton;

        public event Action<bool> OnVisiblityChanged;
        const float stepX = 80f;
        int index;

        public static bool s_CanShowButtons = true;

        static List<TopMenu> sTopMenus = new List<TopMenu>();

        protected virtual void Awake()
        {
            if (button != null)
            {
                index = transform.GetSiblingIndex();
                sTopMenus.Add(this); // Fix Me. This will keep adding Menus if the Scene is reloaded
                sTopMenus.Sort((a, b) => a.index - b.index);
            }
        }

        protected virtual void Start()
        {

            if (button != null)
            {
                //  layout buttons horizontally
                float posx = stepX * (index - (sTopMenus.Count * 0.5f) + 0.5f);

                RectTransform rect = button.GetComponent<RectTransform>();
                Vector2 offset = rect.offsetMin;
                offset.x += posx;
                rect.offsetMin = offset;
                offset = rect.offsetMax;
                offset.x += posx;
                rect.offsetMax = offset;

                rect = buttonBackground.GetComponent<RectTransform>();
                offset = rect.offsetMin;
                offset.x += posx;
                rect.offsetMin = offset;
                offset = rect.offsetMax;
                offset.x += posx;
                rect.offsetMax = offset;
            }
            Deactivate();
        }

        public virtual void OnClick()
        {
            Activate();
        }

        public virtual void Activate()
        {
            HideButtons();

            if (ui != null)
            {
                ui.gameObject.SetActive(true);
            }
            
            if (button != null)
            {
                button.gameObject.SetActive(true);
                buttonBackground.gameObject.SetActive(true);
            }
            
            OnVisiblityChanged?.Invoke(true);
        }

        public virtual void Deactivate()
        {
            ShowButtons();

            if (ui != null)
            {
                ui.gameObject.SetActive(false);
            }
            
            OnVisiblityChanged?.Invoke(false);
        }

        public static void ShowButtons()
        {
            if (!s_CanShowButtons)
            {
                HideButtons();
                return;
            }

            foreach (var m in sTopMenus)
            {
                if (m.button != null)
                {
                    m.button.gameObject.SetActive(true);
                    m.buttonBackground.gameObject.SetActive(true);
                }
            }
        }

        public static void HideButtons()
        {
            foreach (var m in sTopMenus)
            {
                if (m.button != null)
                {
                    m.button.gameObject.SetActive(false);
                    m.buttonBackground.gameObject.SetActive(false);
                }
            }
        }

        public static void EnableCancelScreenButtons(bool isEnabled)
        {
            foreach (var m in sTopMenus)
            {
                if (m.cancelScreenButton != null)
                {
                    m.cancelScreenButton.gameObject.SetActive(isEnabled);
                }
            }
        }

        public static void DeactivateAll()
        {
            foreach (var m in sTopMenus)
            {
                if (m.button != null)
                {
                    m.Deactivate();
                }
            }
        }

        public static void Click(int index)
        {
            if (0 <= index && index < sTopMenus.Count)
            {
                sTopMenus[index].OnClick();
            }
        }
    }
}
