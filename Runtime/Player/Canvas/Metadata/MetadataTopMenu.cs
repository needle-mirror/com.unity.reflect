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
        public static Material sTransparentMaterial;
        public SyncManager syncManager;

        List<Menu> menus = new List<Menu>();
        protected static MaterialSwapper sMaterialSwapper = new MaterialSwapper();
        bool m_MenuOpen = false;

        public GameObject menuTemplate;

        protected override void Awake()
        {
            base.Awake();

            if (sTransparentMaterial == null)
            {
                sTransparentMaterial = Resources.Load<Material>("Shaders/Transparent");
            }
            menuTemplate.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();

            syncManager.onProjectOpened += OnProjectOpened;
            syncManager.onProjectClosed += OnProjectClosed;

            foreach (var instance in syncManager.syncInstances)
            {
                OnInstanceAdded(instance.Value);
            }

            syncManager.onInstanceAdded += OnInstanceAdded;
            
            CreateMenu();
        }

        public static void HideAllRenderers(HashSet<Renderer> excludedRenderers)
        {
            sMaterialSwapper.SetMaterial(sTransparentMaterial, excludedRenderers);
        }
        
        public static void ShowAllRenderers()
        {
            sMaterialSwapper.Restore();
        }

        void OnProjectOpened()
        {
            ParseScene();
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
                foreach (var menu in menus)
                {
                    menu.gameObject.SetActive(false);
                }
            }
            else
            {
                ParseScene();
                m_MenuOpen = true;

                int index = 0;
                foreach (var menu in menus)
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
                    menus[0].Expand();
                }
            }
        }

        void CreateMenu()
        {
            if (menus.Count == 0)
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

                        menus.Add(p);
                    }
                }
            }
        }

        void ParseScene()
        {
            Clear();
            CreateMenu();
            
            //  parse root nodes
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                AddChildren(root.transform);
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
                sMaterialSwapper.AddRenderer(rend);
                
                bool selected = AddNode(node);
                if (!selected && MetadataMenuItem.GetActiveMenuItem() != null)
                {
                    sMaterialSwapper.SwapRenderer(rend, sTransparentMaterial);
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
            foreach (var m in menus)
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
                sMaterialSwapper.RemoveRenderer(rend);
            }

            //  parse children recursively
            for (var c = 0; c < node.childCount; ++c)
            {
                AddChildren(node.GetChild(c));
            }
        }

        void RemoveNode(Transform node)
        {
            foreach (Menu m in menus)
            {
                m.RemoveNode(node);
            }
        }

        public override void Activate()
        {
            base.Activate();

            foreach (var m in menus)
            {
                m.gameObject.SetActive(true);
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            foreach (var m in menus)
            {
                m.gameObject.SetActive(false);
            }
        }

        void Clear()
        {
            foreach (var m in menus)
            {
                Destroy(m.gameObject);
            }
            menus.Clear();
            sMaterialSwapper.Clear();
        }
    }
}
