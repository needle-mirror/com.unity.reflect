using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MetadataMenuItem : MenuItem
    {
        public Button m_VisibleButton;
        public Button m_InvisibleButton;
        public Camera m_FreeCamera;

        bool m_Visible;

        MaterialSwapper m_MaterialSwapper = new MaterialSwapper();

        public MetadataMenuItem()
        {
            m_Visible = true;
        }
        
        private void Start()
        {
            m_Text.text = m_Value;
            UpdateButton();
        }

        public override bool AddNode(Transform node)
        {
            base.AddNode(node);

            var rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                m_MaterialSwapper.AddRenderer(rend);
                
                if (!m_Visible)
                {
                    node.gameObject.SetActive(false);
                }

                if (GetActiveMenuItem() == this)
                {
                    m_MaterialSwapper.SwapRenderer(rend, m_SelectedMaterial);
                    return true;
                }
            }

            return false;
        }

        public override void RemoveNode(Transform node)
        {
            base.RemoveNode(node);
            
            var rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                m_MaterialSwapper.RemoveRenderer(rend);
            }
        }

        public void OnMakeVisible()
        {
            m_Visible = true;

            UpdateSceneVisibility();
            UpdateButton();
        }

        public void OnMakeInvisible()
        {
            m_Visible = false;

            UpdateSceneVisibility();
            UpdateButton();
        }

        public override void OnNameClicked()
        {
            if ((GetActiveMenuItem() == this) && (GetActiveMenuItem().GetFilterView().IsAiming()))
            {
                Deactivate();
            }
            else
            {
                Activate();
            }
        }

        public override void Activate(bool hide = true)
        {
            Vector3 pos;
            Quaternion rot;
            var menuitem = GetActiveMenuItem();
            if (menuitem != null)
            {
                pos = menuitem.GetFilterView().GetCameraOriginalPosition();
                rot = menuitem.GetFilterView().GetCameraOriginalRotation();
                menuitem.Deactivate();
                menuitem.GetFilterView().SetCamera(null, Vector3.zero, Quaternion.identity);
            }
            else
            {
                pos = m_FreeCamera.transform.position;
                rot = m_FreeCamera.transform.rotation;
            }

            Activate(m_FreeCamera, pos, rot);
        }

        void Activate(Camera inCamera, Vector3 inOriginalPosition, Quaternion inOriginalRotation)
        {
            GetFilterView().SetCamera(inCamera, inOriginalPosition, inOriginalRotation);

            m_MaterialSwapper.SetMaterial(m_SelectedMaterial);

            base.Activate();
        }

        public override void Deactivate(bool show = true)
        {
            base.Deactivate(show);

            m_MaterialSwapper.Restore();
        }

        void UpdateButton()
        {
            m_VisibleButton.gameObject.SetActive(m_Visible);
            m_InvisibleButton.gameObject.SetActive(!m_Visible);
        }

        void UpdateSceneVisibility()
        {
            foreach (var node in m_Nodes)
            {
                node.gameObject.SetActive(m_Visible);
            }
        }

    }
}