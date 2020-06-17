using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MenuItem : MonoBehaviour
    {
        public Text m_Text;
        public Material m_SelectedMaterial;
        public MetadataTopMenu m_MetadataTopMenu;

        Menu m_Menu;
        protected string m_Value;
        protected HashSet<Renderer> m_Nodes = new HashSet<Renderer>();
        FilterView m_FilterView = new FilterView();

        static MenuItem s_ActiveMenuItem;

        public static MenuItem GetActiveMenuItem()
        {
            return s_ActiveMenuItem;
        }

        void Start()
        {
            m_Text.text = m_Value;
        }

        public void Initialize(Menu menu, string inValue)
        {
            m_Menu = menu;
            m_Value = inValue;
        }

        private void OnDestroy()
        {
            if (GetActiveMenuItem() == this)
            {
                Deactivate();
            }
        }

        public Menu GetMenu()
        {
            return m_Menu;
        }
        
        public string GetValue()
        {
            return m_Value;
        }
        
        public FilterView GetFilterView()
        {
            return m_FilterView;
        }

        public void SetPosition(float inY)
        {
            GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, inY);
        }

        public virtual bool AddNode(Transform node)
        {
            bool ret = false;
            var renderers = node.GetComponentsInChildren<Renderer>().ToList();
            if (renderers.Count > 0) 
            {
                int presize = m_Nodes.Count;
                m_Nodes.UnionWith(renderers);
                renderers.ForEach(x=>m_FilterView.AddRenderer(x));
                ret = m_Nodes.Count > presize;
            }

            return ret;
        }

        public virtual void RemoveNode(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>().ToList();
            if (renderers.Count > 0)
            {
                renderers.ForEach (x => m_Nodes.Remove(x));
            }
        }

        public virtual void OnNameClicked()
        {
        }

        public virtual void Activate(bool hide = true)
        {
            m_Text.fontStyle = FontStyle.Bold;
            if (m_SelectedMaterial != null)
            {
                m_Text.color = m_SelectedMaterial.color;
            }

            m_FilterView.Aim(true);

            s_ActiveMenuItem = this;

            if (hide)
            {
                m_MetadataTopMenu.HideAllRenderers(m_Nodes);
            }
        }

        public virtual void Deactivate(bool show = true)
        {
            m_Text.fontStyle = FontStyle.Normal;
            m_Text.color = Color.white;

            m_FilterView.Aim(false);

            s_ActiveMenuItem = null;
            
            if (show)
            {
                m_MetadataTopMenu.ShowAllRenderers();
            }
        }
    }
}
