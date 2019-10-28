﻿using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ListView;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR
{
    abstract class EditorXRNestedListViewController<TData, TItem, TIndex> : NestedListViewController<TData, TItem, TIndex>,
        IInstantiateUI, IUsesConnectInterfaces, IUsesControlHaptics, IRayToNode
        where TData : class, INestedListViewItemData<TData, TIndex>
        where TItem : EditorXRListViewItem<TData, TIndex>, INestedListViewItem<TData, TIndex>
    {
#pragma warning disable 649
        [SerializeField]
        HapticPulse m_ScrollPulse;

        [Header("Unassigned haptic pulses will not be performed")]

        [SerializeField]
        HapticPulse m_ItemClickPulse;

        [SerializeField]
        HapticPulse m_ItemHoverStartPulse;

        [SerializeField]
        HapticPulse m_ItemHoverEndPulse;

        [SerializeField]
        HapticPulse m_ItemDragStartPulse;

        [SerializeField]
        HapticPulse m_ItemDraggingPulse;

        [SerializeField]
        HapticPulse m_ItemDragEndPulse;
#pragma warning restore 649

#if !FI_AUTOFILL
        IProvidesControlHaptics IFunctionalitySubscriber<IProvidesControlHaptics>.provider { get; set; }
        IProvidesConnectInterfaces IFunctionalitySubscriber<IProvidesConnectInterfaces>.provider { get; set; }
#endif

        protected override void Recycle(TIndex index)
        {
            if (m_GrabbedRows.ContainsKey(index))
                return;

            base.Recycle(index);
        }

        protected override void UpdateView()
        {
            base.UpdateView();

            if (m_Scrolling)
                this.Pulse(Node.None, m_ScrollPulse);
        }

        protected override TItem InstantiateItem(TData data)
        {
            var item = this.InstantiateUI(m_TemplateDictionary[data.template].prefab, transform, false).GetComponent<TItem>();
            this.ConnectInterfaces(item);

            // Hookup input events for new items.
            item.hoverStart += OnItemHoverStart;
            item.hoverEnd += OnItemHoverEnd;
            item.dragStart += OnItemDragStart;
            item.dragging += OnItemDragging;
            item.dragEnd += OnItemDragEnd;
            item.click += OnItemClicked;
            return item;
        }

        public void OnItemHoverStart(Node node)
        {
            if (m_ItemHoverStartPulse)
                this.Pulse(node, m_ItemHoverStartPulse);
        }

        public void OnItemHoverEnd(Node node)
        {
            if (m_ItemHoverEndPulse)
                this.Pulse(node, m_ItemHoverEndPulse);
        }

        public void OnItemDragStart(Node node)
        {
            if (m_ItemDragStartPulse)
                this.Pulse(node, m_ItemDragStartPulse);
        }

        public void OnItemDragging(Node node)
        {
            if (m_ItemDraggingPulse)
                this.Pulse(node, m_ItemDraggingPulse);
        }

        public void OnItemDragEnd(Node node)
        {
            if (m_ItemDragEndPulse)
                this.Pulse(node, m_ItemDragEndPulse);
        }

        public void OnItemClicked(Node node)
        {
            if (m_ItemClickPulse)
                this.Pulse(node, m_ItemClickPulse);
        }
    }
}
