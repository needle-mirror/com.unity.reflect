using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MenuItem : MonoBehaviour
    {
        public Text text;
        public Material selectedMaterial;

        protected string value;
        protected HashSet<Renderer> nodes = new HashSet<Renderer>();
        FilterView filterView = new FilterView();

        static MenuItem sActiveMenuItem;

        protected static MenuItem GetActiveMenuItem()
        {
            return sActiveMenuItem;
        }

        void Start()
        {
            text.text = value;
        }

        public void Initialize(string inValue)
        {
            value = inValue;
        }

        public FilterView GetFilterView()
        {
            return filterView;
        }

        public void SetPosition(float inY)
        {
            GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, inY);
        }

        public virtual void AddNode(Transform node)
        {
            Renderer rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                nodes.Add(rend);
                filterView.AddRenderer(rend);
            }
        }

        public virtual void OnNameClicked()
        {
        }

        protected void Activate(bool hide = true)
        {
            text.fontStyle = FontStyle.Bold;
            if (selectedMaterial != null)
            {
                text.color = selectedMaterial.color;
            }

            filterView.Aim(true);

            sActiveMenuItem = this;

            if (hide)
            {
                MetadataTopMenu.HideAllRenderers(nodes);
            }
        }

        public virtual void Deactivate(bool show = true)
        {
            text.fontStyle = FontStyle.Normal;
            text.color = Color.white;

            filterView.Aim(false);

            if (show)
            {
                MetadataTopMenu.ShowAllRenderers();
            }
        }
    }
}
