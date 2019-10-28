using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEngine.Reflect
{
    public class MetadataTopMenu : TopMenu
    {
        public string[] keys =
        {
            "Category", "Category/Family", "Document", "System Classification", "Type", "Manufacturer", "Phase Created",
            "Phase Demolished"
        };
        public Material m_TransparentMaterial;
        public SyncManager m_SyncManager;

        List<Menu> m_Menus = new List<Menu>();
        protected MaterialSwapper m_MaterialSwapper = new MaterialSwapper();
        bool m_MenuOpen = false;

        public GameObject menuTemplate;

        protected override void Awake()
        {
            base.Awake();

            menuTemplate.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();

            m_SyncManager.onProjectOpened += OnProjectOpened;
            m_SyncManager.onProjectClosed += OnProjectClosed;

            foreach (var instance in m_SyncManager.syncInstances)
            {
                OnInstanceAdded(instance.Value);
            }

            m_SyncManager.onInstanceAdded += OnInstanceAdded;
            
            CreateMenu();
        }

        public void HideAllRenderers(HashSet<Renderer> excludedRenderers)
        {
            m_MaterialSwapper.SetMaterial(m_TransparentMaterial, excludedRenderers);
        }
        
        public void ShowAllRenderers()
        {
            m_MaterialSwapper.Restore();
        }

        void OnProjectOpened()
        {
            ParseRoot();
            button.interactable = true;
        }

        void OnProjectClosed()
        {
            Clear();
            button.interactable = false;
        }

        void OnInstanceAdded(SyncInstance instance)
        {
            instance.onObjectCreated += OnObjectCreated;
            instance.onObjectDestroyed += OnObjectDestroyed;
        }
        
        public override void OnClick()
        {
            if (m_MenuOpen)
            {
                m_MenuOpen = false;
                foreach (var menu in m_Menus)
                {
                    menu.gameObject.SetActive(false);
                }
            }
            else
            {
                ParseRoot();
                m_MenuOpen = true;

                int index = 0;
                foreach (var menu in m_Menus)
                {
                    if (menu.GetItemCount() > 1)
                    {
                        menu.gameObject.SetActive(true);
                        menu.SetIndex(index++);
                    }
                    else
                    {
                        menu.gameObject.SetActive(false);
                    }
                }
                
                //  open parameter automatically if there is only one
                if (index == 1)
                {
                    m_Menus[0].Expand();
                }
            }
        }

        void CreateMenu()
        {
            if (m_Menus.Count == 0)
            {
                foreach (var key in keys)
                {
                    var panel = Instantiate(menuTemplate, transform);
                    var p = panel.GetComponent<MetadataMenu>();
                    if (p != null)
                    {
                        var path = key.Split('/');
                        if (path.Length == 1)
                        {
                            p.Initialize(path[0]);
                        }
                        else
                        {
                            p.Initialize(path[1], path[0]);
                        }

                        m_Menus.Add(p);
                    }
                }
            }
        }

        void ParseRoot()
        {
            string key = null;
            string value = null;
            var item = MenuItem.GetActiveMenuItem();
            if (item != null)
            {
                key = item.GetMenu().GetKey();
                value = item.GetValue();
            }
            
            Clear();
            CreateMenu();

            if (m_SyncManager.syncRoot != null)
            {
                AddChildren(m_SyncManager.syncRoot);
            }

            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                ActivateMenuItem(key, value);
            }
        }

        void ActivateMenuItem(string key, string value)
        {
            foreach (var m in m_Menus)
            {
                if (m.GetKey() == key)
                {
                    var item = m.GetMenuItem(value);
                    if (item != null)
                    {
                        item.Activate();
                        break;
                    }
                }
            }
        }
        
        void OnObjectCreated(SyncObjectBinding obj)
        {
            AddChildren(obj.transform);
        }
        
        void OnObjectDestroyed(SyncObjectBinding obj)
        {
            RemoveChildren(obj.transform);
        }
        
        void AddChildren(Transform node)
        {
            var rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                m_MaterialSwapper.AddRenderer(rend);
                
                bool selected = AddNode(node);
                if (!selected && MenuItem.GetActiveMenuItem() != null)
                {
                    m_MaterialSwapper.SwapRenderer(rend, m_TransparentMaterial);
                }
            }

            //  parse children recursively
            for (var c = 0; c < node.childCount; ++c)
            {
                AddChildren(node.GetChild(c));
            }
        }

        bool AddNode(Transform node)
        {
            var ret = false;
            foreach (var m in m_Menus)
            {
                if (m.AddNode(node))
                {
                    ret = true;
                }
            }

            return ret;
        }

        void RemoveChildren(Transform node)
        {
            RemoveNode(node);

            var rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                m_MaterialSwapper.RemoveRenderer(rend);
            }

            //  parse children recursively
            for (var c = 0; c < node.childCount; ++c)
            {
                AddChildren(node.GetChild(c));
            }
        }

        void RemoveNode(Transform node)
        {
            foreach (Menu m in m_Menus)
            {
                m.RemoveNode(node);
            }
        }

        public override void Activate()
        {
            base.Activate();

            foreach (var m in m_Menus)
            {
                m.gameObject.SetActive(true);
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            foreach (var m in m_Menus)
            {
                m.gameObject.SetActive(false);
            }
        }

        void Clear()
        {
            foreach (var m in m_Menus)
            {
                Destroy(m.gameObject);
            }
            m_Menus.Clear();
            m_MaterialSwapper.Clear();
        }
    }
}
