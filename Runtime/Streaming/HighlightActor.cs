using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Actor;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class HighlightActor
    {
        public static readonly int k_SelectedLayer = LayerMask.NameToLayer("BimFilterSelect");
        public static readonly int k_OtherLayer = LayerMask.NameToLayer("BimFilterOthers");

#pragma warning disable 649
        Settings m_Settings;
        EventOutput<MetadataCategoriesChanged> m_MetadataCategoriesChangedOutput;
        EventOutput<MetadataGroupsChanged> m_MetadataGroupsChangedOutput;
#pragma warning restore 649

        FilterData m_HighlightedFilterData;

        HashSet<GameObject> m_GameObjects = new HashSet<GameObject>();

        Dictionary<string, Dictionary<string, FilterData>> m_FilterGroups =
            new Dictionary<string, Dictionary<string, FilterData>>();

        [NetInput]
        void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            var gameObject = ctx.Data.GameObject;
            
            if (!gameObject.TryGetComponent<Metadata>(out var metadata))
                return;

            m_GameObjects.Add(gameObject);

            bool setHighlight = false;

            foreach (var groupKey in m_Settings.Safelist)
            {
                if (metadata.GetParameters().TryGetValue(groupKey, out Metadata.Parameter category))
                {
                    if (string.IsNullOrEmpty(category.value))
                        continue;
        
                    // check categories
                    if (!m_FilterGroups.ContainsKey(groupKey))
                    {
                        m_FilterGroups[groupKey] = new Dictionary<string, FilterData>();
                        m_MetadataGroupsChangedOutput.Broadcast(new MetadataGroupsChanged(m_FilterGroups.Keys.OrderBy(e => e).ToList()));
                    }
        
                    var dicFilterData = m_FilterGroups[groupKey];
                    if (!dicFilterData.ContainsKey(category.value))
                    {
                        dicFilterData[category.value] = new FilterData();
                        m_MetadataCategoriesChangedOutput.Broadcast(new MetadataCategoriesChanged(groupKey, dicFilterData.Keys.OrderBy(e => e).ToList()));
                    }
                    
                    // set filters
                    if (!GetFilterData(groupKey, category.value, out var filterData))
                        return;

                    filterData.Instances.Add(gameObject);

                    if (!filterData.Visible)
                        gameObject.SetActive(false);

                    if (m_HighlightedFilterData == filterData)
                    {
                        setHighlight = true;
                    }
                }
            }
            if (m_HighlightedFilterData != null)
            {
                gameObject.SetLayerRecursively(setHighlight ? k_SelectedLayer : k_OtherLayer);
            }
        }

        // TODO
        // void OnGameObjectDestroyed(NetContext<GameObjectDestroyed> ctx)
        // {
        //     var gameObject = ctx.Data.GameObject;
        //     
        //     if (!gameObject.TryGetComponent<Metadata>(out var metadata))
        //         return;
        //
        //     m_GameObjects.Remove(gameObject);
        //
        //     foreach (var groupKey in m_Settings.Safelist)
        //     {
        //         if (metadata.GetParameters().TryGetValue(groupKey, out Metadata.Parameter category))
        //         {
        //             if (!GetFilterData(groupKey, category.value, out var filterData))
        //                 return;
        //
        //             filterData.Instances.Remove(gameObject);
        //         }
        //     }
        // }

        [NetInput]
        void OnSetHighlightVisibility(NetContext<SetHighlightVisibility> ctx)
        {
            SetVisibility(ctx.Data.GroupKey, ctx.Data.FilterKey, ctx.Data.IsVisible);
        }

        [NetInput]
        void OnSetHighlightFilter(NetContext<SetHighlightFilter> ctx)
        {
            if (!GetFilterData(ctx.Data.GroupKey, ctx.Data.FilterKey, out var filterData))
                return;

            if (m_HighlightedFilterData == filterData)
            {
                m_HighlightedFilterData = null;
                RestoreHighlight();
                return;
            }

            m_HighlightedFilterData = filterData;

            foreach (var obj in m_GameObjects)
            {
                obj.SetLayerRecursively(filterData.Instances.Contains(obj) ? k_SelectedLayer : k_OtherLayer);
            }

            m_HighlightedFilterData = filterData;
        }

        [RpcInput]
        void OnGetFilterStates(RpcContext<GetFilterStates> ctx)
        {
            var groupKey = ctx.Data.GroupKey;

            var keys = GetFilterKeys(groupKey);
            var state = new FilterState();

            var result = new List<FilterState>();
            foreach (var filterKey in keys)
            {
                state.Key = filterKey;
                state.IsVisible = IsVisible(groupKey, filterKey);
                state.isHighlighted = IsHighlighted(groupKey, filterKey);
                result.Add(state);
            }

            ctx.SendSuccess(result);
        }

        IEnumerable<string> GetFilterKeys(string groupKey)
        {
            if (m_FilterGroups.TryGetValue(groupKey, out var dicFilterData))
                return dicFilterData.Keys.OrderBy(e => e);

            return null;
        }

        bool IsVisible(string groupKey, string filterKey)
        {
            if (!GetFilterData(groupKey, filterKey, out var filterData))
                return true;

            return filterData.Visible;
        }


        void SetVisibility(string groupKey, string filterKey, bool visible)
        {
            if (!GetFilterData(groupKey, filterKey, out var filterData))
                return;

            if (filterData.Visible == visible)
                return;

            filterData.Visible = visible;

            foreach (var instance in filterData.Instances)
            {
                instance.SetActive(visible);
            }
        }

        bool IsHighlighted(string groupKey, string filterKey)
        {
            if (!GetFilterData(groupKey, filterKey, out var filterData))
                return true;

            return filterData == m_HighlightedFilterData;
        }

        bool GetFilterData(string groupKey, string filterKey, out FilterData filterData)
        {
            filterData = null;
            return m_FilterGroups.TryGetValue(groupKey, out var dicFilterData) &&
                dicFilterData.TryGetValue(filterKey, out filterData);
        }

        void RestoreHighlight()
        {
            var defaultLayer = LayerMask.NameToLayer("Default");
            foreach (var obj in m_GameObjects)
            {
                obj.SetLayerRecursively(defaultLayer);
            }
        }

        class FilterData
        {
            public bool Visible = true;
            public HashSet<GameObject> Instances = new HashSet<GameObject>();
        }

        public class Settings : ActorSettings
        {
            public string[] Safelist =
            {
                "Category", "Family", "Document", "System Classification", "Type", "Manufacturer", "Phase Created",
                "Phase Demolished", "Layer"
            };

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
