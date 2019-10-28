﻿using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR.Handles;
using UnityEditor.Experimental.EditorVR.UI;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
    sealed class HierarchyListViewController : EditorXRNestedListViewController<HierarchyData, HierarchyListItem, int>, IUsesGameObjectLocking, IUsesSetHighlight
    {
        const float k_ClipMargin = 0.001f; // Give the cubes a margin so that their sides don't get clipped

#pragma warning disable 649
        [SerializeField]
        BaseHandle m_TopDropZone;

        [SerializeField]
        BaseHandle m_BottomDropZone;

        [SerializeField]
        Material m_TextMaterial;

        [SerializeField]
        Material m_ExpandArrowMaterial;

        [SerializeField]
        Material m_LockIconMaterial;

        [SerializeField]
        Material m_UnlockIconMaterial;

        [SerializeField]
        Material m_SceneIconDarkMaterial;

        [SerializeField]
        Material m_SceneIconWhiteMaterial;
#pragma warning restore 649

        Material m_TopDropZoneMaterial;
        Material m_BottomDropZoneMaterial;
        float m_DropZoneAlpha;
        float m_BottomDropZoneStartHeight;
        float m_VisibleItemHeight;

        int m_SelectedRow;

        string m_LastSearchQuery;
        bool m_HasLockedQuery;
        bool m_HasFilterQuery;

        readonly List<KeyValuePair<Transform, GameObject>> m_HoveredGameObjects = new List<KeyValuePair<Transform, GameObject>>();

        public override List<HierarchyData> data
        {
            set
            {
                base.data = value;

                if (m_Data != null && m_Data.Count > 0)
                {
                    // Remove any objects that don't exist any more
                    var missingKeys = m_Data.Select(d => d.index).Except(m_ExpandStates.Keys);
                    foreach (var key in missingKeys)
                    {
                        m_ExpandStates.Remove(key);
                    }

                    // Expand the scenes by default
                    foreach (var scene in m_Data)
                    {
                        var instanceID = scene.index;
                        if (!m_ExpandStates.ContainsKey(instanceID))
                            m_ExpandStates[instanceID] = true;
                    }

                    foreach (var d in m_Data)
                    {
                        if (!m_ExpandStates.ContainsKey(d.index))
                            m_ExpandStates[d.index] = false;
                    }
                }
            }
        }

        public string lockedQueryString { private get; set; }

        public Action<int> selectRow { private get; set; }

        public Func<string, bool> matchesFilter { private get; set; }
        public Func<string> getSearchQuery { private get; set; }

#if !FI_AUTOFILL
        IProvidesGameObjectLocking IFunctionalitySubscriber<IProvidesGameObjectLocking>.provider { get; set; }
        IProvidesSetHighlight IFunctionalitySubscriber<IProvidesSetHighlight>.provider { get; set; }
#endif

        protected override void Start()
        {
            base.Start();

            m_TextMaterial = Instantiate(m_TextMaterial);
            m_SceneIconDarkMaterial = Instantiate(m_SceneIconDarkMaterial);
            m_SceneIconWhiteMaterial = Instantiate(m_SceneIconWhiteMaterial);
            m_ExpandArrowMaterial = Instantiate(m_ExpandArrowMaterial);
            m_LockIconMaterial = Instantiate(m_LockIconMaterial);
            m_UnlockIconMaterial = Instantiate(m_UnlockIconMaterial);

            m_BottomDropZoneMaterial = MaterialUtils.GetMaterialClone(m_BottomDropZone.GetComponent<Renderer>());
            m_BottomDropZoneStartHeight = m_BottomDropZone.transform.localScale.z;
            m_TopDropZoneMaterial = MaterialUtils.GetMaterialClone(m_TopDropZone.GetComponent<Renderer>());
            var color = m_TopDropZoneMaterial.color;
            m_DropZoneAlpha = color.a;
            color.a = 0;
            m_TopDropZoneMaterial.color = color;
            m_BottomDropZoneMaterial.color = color;

            var dropZones = new[] { m_BottomDropZone, m_TopDropZone };
            foreach (var dropZone in dropZones)
            {
                dropZone.canDrop = CanDrop;
                dropZone.receiveDrop = ReceiveDrop;
                dropZone.dropHoverStarted += DropHoverStarted;
                dropZone.dropHoverEnded += DropHoverEnded;
            }

            m_BottomDropZone.gameObject.SetActive(false); // Don't block scroll interaction
        }

        protected override void UpdateItems()
        {
            var parentMatrix = transform.worldToLocalMatrix;
            ClipText.SetMaterialClip(m_TextMaterial, parentMatrix, m_Extents);
            ClipText.SetMaterialClip(m_SceneIconDarkMaterial, parentMatrix, m_Extents);
            ClipText.SetMaterialClip(m_SceneIconWhiteMaterial, parentMatrix, m_Extents);
            ClipText.SetMaterialClip(m_ExpandArrowMaterial, parentMatrix, m_Extents);
            ClipText.SetMaterialClip(m_LockIconMaterial, parentMatrix, m_Extents);
            ClipText.SetMaterialClip(m_UnlockIconMaterial, parentMatrix, m_Extents);

            m_VisibleItemHeight = 0;

            var searchQuery = getSearchQuery();
            if (searchQuery != null && searchQuery.CompareTo(m_LastSearchQuery) != 0)
            {
                m_LastSearchQuery = searchQuery;
                m_HasLockedQuery = searchQuery.Contains(lockedQueryString);
                if (m_HasLockedQuery)
                    searchQuery = searchQuery.Replace(lockedQueryString, string.Empty).Trim();

                m_HasFilterQuery = !string.IsNullOrEmpty(searchQuery);
            }

            base.UpdateItems();

            UpdateDropZones();
        }

        void UpdateDropZones()
        {
            var width = m_Size.x - k_ClipMargin;
            var topDropZoneTransform = m_TopDropZone.transform;
            var dropZoneScale = topDropZoneTransform.localScale;
            dropZoneScale.x = width;
            topDropZoneTransform.localScale = dropZoneScale;

            var extentsZ = m_Extents.z;
            var dropZonePosition = topDropZoneTransform.localPosition;
            dropZonePosition.z = extentsZ + dropZoneScale.z * 0.5f;
            topDropZoneTransform.localPosition = dropZonePosition;

            var bottomDropZoneTransform = m_BottomDropZone.transform;
            dropZoneScale = bottomDropZoneTransform.localScale;
            dropZoneScale.x = width;
            var itemSize = m_ItemSize.z;
            var extraSpace = extentsZ - m_VisibleItemHeight - scrollOffset % itemSize;
            dropZoneScale.z = extraSpace;

            bottomDropZoneTransform.localScale = dropZoneScale;

            dropZonePosition = bottomDropZoneTransform.localPosition;
            dropZonePosition.z = dropZoneScale.z * 0.5f - extentsZ;
            bottomDropZoneTransform.localPosition = dropZonePosition;

            if (extraSpace < m_BottomDropZoneStartHeight)
            {
                dropZoneScale.z = m_BottomDropZoneStartHeight;
                bottomDropZoneTransform.localScale = dropZoneScale;
                dropZonePosition.z = -dropZoneScale.z * 0.5f - extentsZ;
            }
        }

        void UpdateHierarchyItem(HierarchyData data, int order, ref float offset, int depth, bool? expanded, ref bool doneSettling)
        {
            var index = data.index;
            HierarchyListItem item;
            if (!m_ListItems.TryGetValue(index, out item))
                GetNewItem(data, out item);

            var go = data.gameObject;
            var kvp = new KeyValuePair<Transform, GameObject>(item.hoveringRayOrigin, go);

            // Multiple rays can hover and unhover, so it's necessary to keep track of when hover state changes, so that
            // highlights can be turned on or off
            if (item.hovering || m_HoveredGameObjects.Remove(kvp))
            {
                this.SetHighlight(go, item.hovering, item.hoveringRayOrigin, force: item.hovering);

                if (item.hovering)
                    m_HoveredGameObjects.Add(kvp);
            }

            var width = m_Size.x - k_ClipMargin;
            var locked = this.IsLocked(data.gameObject);
            item.UpdateSelf(width, depth, expanded, index == m_SelectedRow, locked);

            var worldToLocalMatrix = transform.worldToLocalMatrix;
            ClipText.SetMaterialClip(item.cubeMaterial, worldToLocalMatrix, m_Extents);
            ClipText.SetMaterialClip(item.dropZoneMaterial, worldToLocalMatrix, m_Extents);

            m_VisibleItemHeight += itemSize.z;
            UpdateItem(item, order, offset + m_ScrollOffset, ref doneSettling);

            var extraSpace = item.extraSpace * itemSize.z;
            offset += extraSpace;
            m_VisibleItemHeight += extraSpace;
        }

        protected override void UpdateNestedItems(ref int order, ref float offset, ref bool doneSettling, int depth = 0)
        {
            m_UpdateStack.Push(new UpdateData
            {
                data = m_Data,
                depth = depth
            });

            while (m_UpdateStack.Count > 0)
            {
                var stackData = m_UpdateStack.Pop();
                var nestedData = stackData.data;
                depth = stackData.depth;

                var i = stackData.index;
                for (; i < nestedData.Count; i++)
                {
                    var datum = nestedData[i];
                    var index = datum.index;
                    bool expanded;
                    m_ExpandStates.TryGetValue(index, out expanded);

                    var grabbed = m_GrabbedRows.ContainsKey(index);

                    if (grabbed)
                    {
                        var item = GetListItem(index);
                        if (item && item.isStillSettling) // "Hang on" to settle state until grabbed object is settled in the list
                            doneSettling = false;

                        continue;
                    }

                    var hasChildren = datum.children != null;

                    var localOffset = offset + m_ScrollOffset;
                    var shouldRecycle = localOffset + itemSize.z < 0 || localOffset > m_Size.z;

                    if (m_HasLockedQuery || m_HasFilterQuery)
                    {
                        var filterTestPass = true;

                        if (m_HasLockedQuery)
                            filterTestPass = this.IsLocked(datum.gameObject);

                        if (m_HasFilterQuery)
                            filterTestPass &= datum.types.Any(type => matchesFilter(type));

                        if (!filterTestPass) // If this item doesn't match, then move on to the next item; do not count
                        {
                            Recycle(index);
                        }
                        else
                        {
                            if (shouldRecycle)
                                Recycle(index);
                            else
                                UpdateHierarchyItem(datum, order++, ref offset, 0, null, ref doneSettling);

                            offset += itemSize.z;
                        }

                        if (hasChildren)
                        {
                            m_UpdateStack.Push(new UpdateData
                            {
                                data = nestedData,
                                index = i + 1
                            });

                            m_UpdateStack.Push(new UpdateData
                            {
                                data = datum.children
                            });
                            break;
                        }
                    }
                    else
                    {
                        if (shouldRecycle)
                            Recycle(index);
                        else
                            UpdateHierarchyItem(datum, order++, ref offset, depth, expanded, ref doneSettling);

                        offset += itemSize.z;

                        if (hasChildren)
                        {
                            if (expanded)
                            {
                                m_UpdateStack.Push(new UpdateData
                                {
                                    data = nestedData,
                                    depth = depth,

                                    index = i + 1
                                });

                                m_UpdateStack.Push(new UpdateData
                                {
                                    data = datum.children,
                                    depth = depth + 1
                                });
                                break;
                            }

                            RecycleChildren(datum);
                        }
                        else
                        {
                            m_ExpandStates[index] = false;
                        }
                    }
                }
            }
        }

        protected override bool GetNewItem(HierarchyData data, out HierarchyListItem item)
        {
            var instantiated = base.GetNewItem(data, out item);

            if (instantiated)
            {
                item.SetMaterials(m_TextMaterial, m_ExpandArrowMaterial, m_LockIconMaterial, m_UnlockIconMaterial,
                    m_SceneIconDarkMaterial, m_SceneIconWhiteMaterial);
                item.selectRow = SelectRow;

                item.setRowGrabbed = SetRowGrabbed;
                item.getGrabbedRow = GetGrabbedRow;

                item.toggleLock = ToggleLock;

                item.setExpanded = SetExpanded;
                item.isExpanded = GetExpanded;

                item.getListItem = GetListItem;
            }

            item.UpdateArrow(GetExpanded(data.index), true);

            return instantiated;
        }

        protected override void SetRowGrabbed(int index, Transform rayOrigin, bool grabbed)
        {
            base.SetRowGrabbed(index, rayOrigin, grabbed);
            m_BottomDropZone.gameObject.SetActive(m_GrabbedRows.Count > 0); // Don't block scroll interaction
        }

        void ToggleLock(int index)
        {
            HierarchyListItem listItem;
            if (m_ListItems.TryGetValue(index, out listItem))
            {
                var data = listItem.data;
                var go = data.gameObject;
                this.SetLocked(go, !this.IsLocked(go));
            }
        }

        public void SelectRow(int index)
        {
            if (data == null)
                return;

            m_SelectedRow = index;

            foreach (var datum in data)
            {
                ExpandToRow(datum, index);
            }

            selectRow(index);

            var scrollHeight = 0f;
            foreach (var datum in data)
            {
                ScrollToIndex(datum, index, ref scrollHeight);
            }
        }

        bool ExpandToRow(HierarchyData container, int rowID)
        {
            var index = container.index;
            if (index == rowID)
            {
                return true;
            }

            var found = false;
            if (container.children != null)
            {
                foreach (var child in container.children)
                {
                    if (ExpandToRow(child, rowID))
                        found = true;
                }
            }

            if (found)
                m_ExpandStates[index] = true;

            return found;
        }

        static bool CanDrop(BaseHandle handle, object dropObject)
        {
            return dropObject is HierarchyData;
        }

        void ReceiveDrop(BaseHandle handle, object dropObject)
        {
            if (handle == m_TopDropZone)
            {
                var hierarchyData = dropObject as HierarchyData;
                if (hierarchyData != null)
                {
                    var gameObject = hierarchyData.gameObject;
                    gameObject.transform.SetParent(null);
                    gameObject.transform.SetAsFirstSibling();
                }
            }

            if (handle == m_BottomDropZone)
            {
                var hierarchyData = dropObject as HierarchyData;
                if (hierarchyData != null)
                {
                    var gameObject = hierarchyData.gameObject;
                    gameObject.transform.SetParent(null);
                    gameObject.transform.SetAsLastSibling();
                }
            }
        }

        void DropHoverStarted(BaseHandle handle)
        {
            var material = handle == m_TopDropZone ? m_TopDropZoneMaterial : m_BottomDropZoneMaterial;
            var color = material.color;
            color.a = m_DropZoneAlpha;
            material.color = color;
        }

        void DropHoverEnded(BaseHandle handle)
        {
            var material = handle == m_TopDropZone ? m_TopDropZoneMaterial : m_BottomDropZoneMaterial;
            var color = material.color;
            color.a = 0;
            material.color = color;
        }

        public void OnScroll(float delta)
        {
            if (m_Settling)
                return;

            scrollOffset += delta;
        }

        void OnDestroy()
        {
            UnityObjectUtils.Destroy(m_TextMaterial);
            UnityObjectUtils.Destroy(m_ExpandArrowMaterial);
            UnityObjectUtils.Destroy(m_LockIconMaterial);
            UnityObjectUtils.Destroy(m_UnlockIconMaterial);
        }
    }
}
