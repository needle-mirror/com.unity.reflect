using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MetadataMenuItem : MenuItem
    {
        public Button visibleButton;
        public Button invisibleButton;
        public Camera freeCamera;

        bool visible;

        MaterialSwapper materialSwapper = new MaterialSwapper();

        private void Start()
        {
            text.text = value;
            visible = true;
            UpdateButton();
        }

        public override void AddNode(Transform node)
        {
            base.AddNode(node);

            Renderer rend = node.GetComponent<Renderer>();
            if (rend != null)
            {
                materialSwapper.AddRenderer(rend);
            }
        }

        public void OnMakeVisible()
        {
            visible = true;

            UpdateSceneVisibility();
            UpdateButton();
        }

        public void OnMakeInvisible()
        {
            visible = false;

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
                Vector3 pos;
                Quaternion rot;
                MenuItem menuitem = GetActiveMenuItem();
                if (menuitem != null)
                {
                    pos = menuitem.GetFilterView().GetCameraOriginalPosition();
                    rot = menuitem.GetFilterView().GetCameraOriginalRotation();
                    menuitem.Deactivate();
                    menuitem.GetFilterView().SetCamera(null, Vector3.zero, Quaternion.identity);
                }
                else
                {
                    pos = freeCamera.transform.position;
                    rot = freeCamera.transform.rotation;
                }

                Activate(freeCamera, pos, rot);
            }
        }

        void Activate(Camera inCamera, Vector3 inOriginalPosition, Quaternion inOriginalRotation)
        {
            GetFilterView().SetCamera(inCamera, inOriginalPosition, inOriginalRotation);

            materialSwapper.SetMaterial(selectedMaterial);

            base.Activate();
        }

        public override void Deactivate(bool show = true)
        {
            base.Deactivate(show);

            materialSwapper.Restore();
        }

        void UpdateButton()
        {
            visibleButton.gameObject.SetActive(visible);
            invisibleButton.gameObject.SetActive(!visible);
        }

        void UpdateSceneVisibility()
        {
            foreach (Renderer node in nodes)
            {
                node.gameObject.SetActive(visible);
            }
        }

    }
}