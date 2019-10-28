using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Use this interface if you have a component that contains many instances of something you want discoverable
    /// by the cached component filter.  Make sure the THostType matches up with the TFilterType in the CachedComponent filter
    /// </summary>
    /// <typeparam name="THostType">The type of object the host component contains.</typeparam>
    public interface IComponentHost<THostType> where THostType : class
    {
        THostType[] HostedComponents { get; }
    }

    /// <summary>
    /// Describes where the initial list of components should be built from
    /// </summary>
    [Flags]
    public enum CachedSearchType
    {
        Children = 1,
        Self = 2,
        Parents = 4
    }

    /// <summary>
    /// Class that allows for cached retrieval/filtering of multiple types of components into lists
    /// Proper usage of this class is:
    /// <![CDATA[
    /// using (var componentFilter = new CachedComponentFilter<typeToFind,componentTypeThatContains>(instanceOfComponent))
    /// {
    ///
    /// }
    /// ]]>
    /// </summary>
    public class CachedComponentFilter<TFilterType, TRootType> : IDisposable where TRootType : Component where TFilterType : class
    {
        readonly List<TFilterType> m_MasterComponentStorage;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<TFilterType> k_TempComponentList = new List<TFilterType>();
        static readonly List<IComponentHost<TFilterType>> k_TempHostComponentList = new List<IComponentHost<TFilterType>>();

        bool m_DisposedValue; // To detect redundant calls

        public CachedComponentFilter(TRootType componentRoot, CachedSearchType cachedSearchType = CachedSearchType.Self | CachedSearchType.Children, bool includeDisabled = true)
        {
            m_MasterComponentStorage = CollectionPool<List<TFilterType>, TFilterType>.GetCollection();

            k_TempComponentList.Clear();
            k_TempHostComponentList.Clear();

            // Components on the root get added first
            if ((cachedSearchType & CachedSearchType.Self) == CachedSearchType.Self)
            {
                componentRoot.GetComponents(k_TempComponentList);
                componentRoot.GetComponents(k_TempHostComponentList);
                FilteredCopyToMaster(includeDisabled);
            }

            // Then parents, until/unless we hit an end cap node
            if ((cachedSearchType & CachedSearchType.Parents) == CachedSearchType.Parents)
            {
                var searchRoot = componentRoot.transform.parent;
                while (searchRoot != null)
                {
                    if (searchRoot.GetComponent<TRootType>() != null)
                        break;

                    searchRoot.GetComponents(k_TempComponentList);
                    searchRoot.GetComponents(k_TempHostComponentList);
                    FilteredCopyToMaster(includeDisabled);

                    searchRoot = searchRoot.transform.parent;
                }
            }

            // Then children, until/unless we hit an end cap node
            if ((cachedSearchType & CachedSearchType.Children) == CachedSearchType.Children)
            {
                // It's not as graceful going down the hierarchy, so we just use the built-in functions and filter afterwards
                foreach (Transform child in componentRoot.transform)
                {
                    child.GetComponentsInChildren(k_TempComponentList);
                    child.GetComponentsInChildren(k_TempHostComponentList);
                    FilteredCopyToMaster(includeDisabled, componentRoot);
                }
            }
        }

        public CachedComponentFilter(TFilterType[] componentList, bool includeDisabled = true)
        {
            if (componentList == null)
                return;

            m_MasterComponentStorage = CollectionPool<List<TFilterType>, TFilterType>.GetCollection();

            k_TempComponentList.Clear();
            k_TempComponentList.AddRange(componentList);
            FilteredCopyToMaster(includeDisabled);
        }

        public void StoreMatchingComponents<TChildType>(List<TChildType> outputList) where TChildType : class, TFilterType
        {
            foreach (var currentComponent in m_MasterComponentStorage)
            {
                var asChildType = currentComponent as TChildType;
                if (asChildType != null)
                    outputList.Add(asChildType);
            }
        }

        public TChildType[] GetMatchingComponents<TChildType>() where TChildType : class, TFilterType
        {
            var componentCount = 0;
            foreach (var currentComponent in m_MasterComponentStorage)
            {
                var asChildType = currentComponent as TChildType;
                if (asChildType != null)
                    componentCount++;
            }

            var outputArray = new TChildType[componentCount];
            componentCount = 0;
            foreach (var currentComponent in m_MasterComponentStorage)
            {
                var asChildType = currentComponent as TChildType;
                if (asChildType == null)
                    continue;

                outputArray[componentCount] = asChildType;
                componentCount++;
            }

            return outputArray;
        }

        void FilteredCopyToMaster(bool includeDisabled)
        {
            if (includeDisabled)
            {
                m_MasterComponentStorage.AddRange(k_TempComponentList);
                foreach (var currentEntry in k_TempHostComponentList)
                {
                    m_MasterComponentStorage.AddRange(currentEntry.HostedComponents);
                }
            }
            else
            {
                foreach (var currentEntry in k_TempComponentList)
                {
                    var currentBehaviour = currentEntry as Behaviour;
                    if (currentBehaviour != null && !currentBehaviour.enabled)
                        continue;

                    m_MasterComponentStorage.Add(currentEntry);
                }

                foreach (var currentEntry in k_TempHostComponentList)
                {
                    var currentBehaviour = currentEntry as Behaviour;
                    if (currentBehaviour != null && !currentBehaviour.enabled)
                        continue;

                    m_MasterComponentStorage.AddRange(currentEntry.HostedComponents);
                }
            }
        }

        void FilteredCopyToMaster(bool includeDisabled, TRootType requiredRoot)
        {
            // Here, we want every entry that isn't on the same gameobject as the required root
            // Additionally, any GameObjects that are between this object and the root (children of the root, parent of a component)
            // cannot have a component of the root type, or it is part of a different collection of objects and should be skipped
            var rootTransform = requiredRoot;
            if (includeDisabled)
            {
                foreach (var currentEntry in k_TempComponentList)
                {
                    var currentComponent = currentEntry as Component;

                    if (currentComponent.transform == rootTransform)
                        continue;

                    if (currentComponent.GetComponentInParent<TRootType>() != requiredRoot)
                        continue;

                    m_MasterComponentStorage.Add(currentEntry);
                }

                foreach (var currentEntry in k_TempHostComponentList)
                {
                    var currentComponent = currentEntry as Component;

                    if (currentComponent.transform == rootTransform)
                        continue;

                    if (currentComponent.GetComponentInParent<TRootType>() != requiredRoot)
                        continue;

                    m_MasterComponentStorage.AddRange(currentEntry.HostedComponents);
                }
            }
            else
            {
                foreach (var currentEntry in k_TempComponentList)
                {
                    var currentBehaviour = currentEntry as Behaviour;

                    if (!currentBehaviour.enabled)
                        continue;

                    if (currentBehaviour.transform == rootTransform)
                        continue;

                    if (currentBehaviour.GetComponentInParent<TRootType>() != requiredRoot)
                        continue;

                    m_MasterComponentStorage.Add(currentEntry);
                }

                foreach (var currentEntry in k_TempHostComponentList)
                {
                    var currentBehaviour = currentEntry as Behaviour;

                    if (!currentBehaviour.enabled)
                        continue;

                    if (currentBehaviour.transform == rootTransform)
                        continue;

                    if (currentBehaviour.GetComponentInParent<TRootType>() != requiredRoot)
                        continue;

                    m_MasterComponentStorage.AddRange(currentEntry.HostedComponents);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_DisposedValue)
                return;

            if (disposing)
                CollectionPool<List<TFilterType>, TFilterType>.RecycleCollection(m_MasterComponentStorage);

            m_DisposedValue = true;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
    }
}
