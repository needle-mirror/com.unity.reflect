﻿using UnityEditor.Experimental.EditorVR.Handles;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
    sealed class HierarchyUI : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        HierarchyListViewController m_ListView;

        [SerializeField]
        BaseHandle m_ScrollHandle;
#pragma warning restore 649

        public HierarchyListViewController listView { get { return m_ListView; } }

        public BaseHandle scrollHandle { get { return m_ScrollHandle; } }
    }
}
