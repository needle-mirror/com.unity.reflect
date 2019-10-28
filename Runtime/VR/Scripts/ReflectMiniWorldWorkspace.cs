using System;
using UnityEngine;
using UnityEditor.Experimental.EditorVR.Workspaces;

namespace UnityEngine.Reflect
{
    public class ReflectMiniWorldWorkspace : MiniWorldWorkspace
    {
        protected override void SetupInitialBounds()
        {
            minBounds = new Vector3(MinBounds.x, 0.5f, 0.25f);
            m_CustomStartingBounds = new Vector3(1f, 0.5f, 0.5f);
        }

        public override void Setup()
        {
            base.Setup();

            m_ZoomSliderUI.zoomSlider.value = Mathf.Log10(k_ZoomSliderMax);
        }

        protected override void ResetMiniWorldLocalPosition()
        {
            // multiply by -1 so the mini world is behind the window instead of in front of it
            m_MiniWorld.transform.localPosition = Vector3.up * contentBounds.extents.y * -1f;
        }
    }
}
