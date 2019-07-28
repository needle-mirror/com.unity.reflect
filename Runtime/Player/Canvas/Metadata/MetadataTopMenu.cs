using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Reflect.Services;

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
            syncManager.onSyncUpdateEnd += OnSyncUpdateEnd;
        }

        public static void HideAllRenderers(HashSet<Renderer> excludedRenderers)
        {
            sMaterialSwapper.SetMaterial(sTransparentMaterial, excludedRenderers);
        }
        
        public static void ShowAllRenderers()
        {
            sMaterialSwapper.Restore();
        }

        public void OnProjectOpened()
        {
            ParseScene();
            button.interactable = (menus.Count > 0);
        }

        public void OnProjectClosed()
        {
            Clear();
            button.interactable = false;
        }

        public void OnSyncUpdateEnd()
        {
            ShowAllRenderers();
            Clear();
            ParseScene();
        }

        public override void OnClick()
        {
            foreach (Menu m in menus)
            {
                m.gameObject.SetActive(!m.gameObject.activeSelf);
            }

            //  open parameter automatically if there is only one
            if (menus.Count == 1)
            {
                menus[0].Expand();
            }
        }

        void ParseScene()
        {
            foreach (string key in keys)
            {
                GameObject panel = Instantiate(menuTemplate, transform);
                MetadataMenu p = panel.GetComponent<MetadataMenu>();
                if (p != null)
                {
                    string[] path = key.Split('/');
                    if (path.Length == 1)
                    {
                        p.Initialize(path[0]);
                    }
                    else
                    {
                        p.Initialize(path[1], path[0]);
                    }

                    AddMenu(p);
                }
            }

            //  parse root nodes
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                AddChildren(root.transform);
            }

            //  remove empty menus
            for (int f = menus.Count - 1; f >= 0; --f)
            {
                Menu m = menus[f];
                if (m.GetItemCount() < 2)
                {
                    menus.RemoveAt(f);
                    Destroy(m.gameObject);
                }
            }

            for (int f = 0; f < menus.Count; ++f)
            {
                menus[f].SetIndex(f);
            }
        }

        void AddChildren(Transform node)
        {
            AddNode(node);

            Renderer rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                sMaterialSwapper.AddRenderer(rend);
            }

            //  parse children recursively
            for (int c = 0; c < node.childCount; ++c)
            {
                AddChildren(node.GetChild(c));
            }
        }

        void AddNode(Transform node)
        {
            foreach (Menu m in menus)
            {
                m.AddNode(node);
            }
        }

        public void AddMenu(Menu inMenu)
        {
            menus.Add(inMenu);
        }

        public override void Activate()
        {
            base.Activate();

            foreach (Menu m in menus)
            {
                m.gameObject.SetActive(true);
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            foreach (Menu m in menus)
            {
                m.gameObject.SetActive(false);
            }
        }

        public void Clear()
        {
            foreach (Menu m in menus)
            {
                Destroy(m.gameObject);
            }
            menus.Clear();
            sMaterialSwapper.Clear();
        }
    }
}
