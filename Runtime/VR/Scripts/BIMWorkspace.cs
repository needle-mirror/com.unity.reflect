using System.Collections.Generic;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Handles;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEditor.Experimental.EditorVR.Workspaces;
using UnityEngine;
using Selection = UnityEditor.Experimental.EditorVR.Selection;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Reflect
{
    [MainMenuItem("BIM", "Workspaces", "View all BIM data in your model")]
    [SpatialMenuItem("BIM", "Workspaces", "View all BIM data in your model")]
    class BIMWorkspace : Workspace, 
        ISelectionChanged,
        IUsesMoveCameraRig
    {
        //protected const int RENDER_QUEUE = 9500;

        [SerializeField] protected GameObject m_BIMViewerUIPrefab = null;

        protected BIMViewerUI m_BIMViewerUI;

#if !FI_AUTOFILL
        IProvidesMoveCameraRig IFunctionalitySubscriber<IProvidesMoveCameraRig>.provider { get; set; }
#endif

        public override void Setup()
        {
            // Initial bounds must be set before the base.Setup() is called
            minBounds = new Vector3(1f, MinBounds.y, 0.5f);
            m_CustomStartingBounds = minBounds;

            base.Setup();

            m_BIMViewerUI = EditorXRUtils.Instantiate(m_BIMViewerUIPrefab, m_WorkspaceUI.topFaceContainer, false).GetComponent<BIMViewerUI>();
            
            m_BIMViewerUI.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            OnBoundsChanged();
            OnSelectionChanged();
        }

        protected override void OnBoundsChanged()
        {
            // Reset the topFaceContainer scale to avoid child object stretching
            m_WorkspaceUI.topFaceContainer.localScale = Vector3.one;

            Vector3 size = contentBounds.size;
            size.y = float.MaxValue; // Add height for dropdowns
            size.x -= k_DoubleFaceMargin; // Shrink the content width, so that there is space allowed to grab and scroll
            size.z -= k_DoubleFaceMargin; // Reduce the height of the inspector contents as to fit within the bounds of the workspace

            if (m_BIMViewerUI.transform is RectTransform rectTransform)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * 1000f);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.z * 1000f);
            }
        }

        public void OnSelectionChanged()
        {
            if (m_BIMViewerUI != null)
            {
                m_BIMViewerUI.RefreshMetaData(Selection.activeGameObject?.GetComponent<Metadata>());
            }
        }
    }
}
