using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class Menu : MovableMenu
    {
        public Transform scrollContent;
        public Transform title;
        public Transform menuItem;
        public Transform closeButton;
        public Transform pinButton;
        public Transform unpinButton;

        static readonly float k_CollapsedHeight = 30f;
        static readonly float k_CollapsedMinMultiplier = 40f;

        protected SortedDictionary<string, MenuItem> nodes = new SortedDictionary<string, MenuItem>();

        protected string key;

        int index;

        float expandedHeight;
        Vector2 expandedMin;
        Vector2 expandedMax;

        float collapsedHeight;
        Vector2 collapsedMin;
        Vector2 collapsedMax;

        bool expanded;

        protected override void Start()
        {
            base.Start();

            expandedHeight = rect.rect.height;
            expandedMin = rect.offsetMin;
            expandedMax = rect.offsetMax;

            SetIndex(index);

            collapsedHeight = k_CollapsedHeight;
            collapsedMin = rect.offsetMin;
            collapsedMin.y += index * k_CollapsedMinMultiplier;
            collapsedMax = rect.offsetMax;
            collapsedMax.y -= expandedHeight - collapsedHeight - (index * 40f);

            //  skip initial animation
            rect.offsetMin = collapsedMin;
            rect.offsetMax = collapsedMax;

            if (expanded)
            {
                MoveTo(expandedMin, expandedMax);
            }
            else
            {
                MoveTo(collapsedMin, collapsedMax);
            }
        }

        public void Initialize(string inKey, string inParent = null)
        {
            key = inKey;
        }

        public string GetKey()
        {
            return key;
        }

        public MenuItem GetMenuItem(string value)
        {
            nodes.TryGetValue(value, out var item);
            return item;
        }
        
        public void SetIndex(int inIndex)
        {
            index = inIndex;
        }

        void Collapse()
        {
            MoveTo(collapsedMin, collapsedMax);
            expanded = false;
            closeButton.gameObject.SetActive(false);

            //  show sibblings
            Transform parent = transform.parent;
            for (int c = 0; c < parent.childCount; ++c)
            {
                Transform child = parent.GetChild(c);
                if (child != transform)
                {
                    Menu filter = child.GetComponent<Menu>();
                    if (filter != null)
                    {
                        filter.MoveDeltaY((filter.index < index) ? expandedHeight : -expandedHeight);
                    }
                }
            }
        }

        public void Expand()
        {
            MoveTo(expandedMin, expandedMax);
            expanded = true;
            closeButton.gameObject.SetActive(true);

            //  hide sibblings
            Transform parent = transform.parent;
            for (int c = 0; c < parent.childCount; ++c)
            {
                Transform child = parent.GetChild(c);
                if (child != transform)
                {
                    Menu filter = child.GetComponent<Menu>();
                    if (filter != null)
                    {
                        filter.MoveDeltaY((filter.index > index) ? expandedHeight : -expandedHeight);
                    }
                }
            }
        }

        void Push()
        {

        }

        public GameObject NewMenuItem()
        {
            return Instantiate(menuItem.gameObject, scrollContent);
        }

        public int GetItemCount()
        {
            return nodes.Count;
        }

        public virtual bool AddNode(Transform node)
        {
            return false;
        }

        public virtual void RemoveNode(Transform node)
        {
        }

        public void OnPin()
        {
            pinButton.gameObject.SetActive(false);
            unpinButton.gameObject.SetActive(true);
        }
        
        public void OnUnpin()
        {
            pinButton.gameObject.SetActive(true);
            unpinButton.gameObject.SetActive(false);
        }
        
        public void OnClick()
        {
            if (expanded)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }
    }
}