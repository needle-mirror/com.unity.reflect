using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class BoundingBoxActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetComponent m_Net;
        EventOutput<GlobalBoundsUpdated> m_GlobalBoundsUpdatedOutput;
#pragma warning restore 649

        readonly Dictionary<Guid, BoundingBoxReference> m_IdToBoxes = new Dictionary<Guid, BoundingBoxReference>();
        readonly Dictionary<Guid, GameObject> m_IdToGameObjects = new Dictionary<Guid, GameObject>();
        readonly Dictionary<StreamState, Material> m_DefaultMaterialPresets = new Dictionary<StreamState, Material>();
        readonly Dictionary<StreamState, Material> m_DebugMaterialPresets = new Dictionary<StreamState, Material>();

        Bounds m_GlobalBounds;
        Transform m_BoundingBoxParent;
        List<BoundingBoxReference> m_BoundingBoxPool;

        Dictionary<StreamState, Material> CurrentMaterialPresets =>
            m_Settings == null || m_Settings.UseDebugMaterials ? m_DebugMaterialPresets : m_DefaultMaterialPresets;

        public void Inject()
        {
            m_GlobalBounds.size = Vector3.zero;

            foreach (var boundingBoxMaterial in m_Settings.DefaultBoundingBoxMaterials)
                m_DefaultMaterialPresets.Add(boundingBoxMaterial.streamState, boundingBoxMaterial.material);

            foreach (var boundingBoxMaterial in m_Settings.DebugBoundingBoxMaterials)
                m_DebugMaterialPresets.Add(boundingBoxMaterial.streamState, boundingBoxMaterial.material);

            InitBoundingBoxPool();
			
            m_GlobalBounds.size = Vector3.zero;
        }

        public void Shutdown()
        {
            foreach (var kvp in m_IdToBoxes)
                ReleaseBoundingBoxReference(kvp.Value);

            m_IdToBoxes?.Clear();
            m_IdToGameObjects?.Clear();
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            foreach (var entry in ctx.Data.Added)
            {
                if (ShouldSkip(entry))
                    continue;
                
                AddOrEditBoundingBox(entry.Id, entry.Spatial.Box, StreamState.Asset, false);
            }

            foreach (var entry in ctx.Data.Removed)
            {
                if (ShouldSkip(entry))
                    continue;
                
                ReleaseBoundingBoxReference(m_IdToBoxes[entry.Id]);
                m_IdToBoxes.Remove(entry.Id);
            }

            foreach (var entry in ctx.Data.Changed)
            {
                if (ShouldSkip(entry.NewInfo))
                    continue;
                
                if (!m_IdToBoxes.TryGetValue(entry.OldInfo.Id, out var box))
                    break;

                SetBoundingBoxValues(box, entry.NewInfo.Spatial.Box);
            }

            m_GlobalBoundsUpdatedOutput.Broadcast(new GlobalBoundsUpdated(m_GlobalBounds));
        }

        static bool ShouldSkip(EntryData entry)
        {
            if (entry.EntryType != typeof(SyncObjectInstance))
                return true;

            if (entry.Spatial.Box.Min == entry.Spatial.Box.Max)
            {
                Debug.LogError($"Received an empty box in {nameof(BoundingBoxActor)}");
                return true;
            }

            return false;
        }

        // public void OnFilteredAssetEvent(SyncedData<StreamAsset> streamAsset, StreamEvent eventType)
        // {
        //     switch (eventType)
        //     {
        //         case StreamEvent.Added:
        //             if (m_IdToBoxes.TryGetValue(streamAsset.key, out var box))
        //                 SetStreamState(box, StreamState.FilteredAsset);
        //             break;
        //         case StreamEvent.Removed:
        //             if (m_IdToBoxes.TryGetValue(streamAsset.key, out box))
        //             {
        //                 SetStreamState(box, StreamState.Removed);
        //                 box.gameObject.SetActive(true);
        //             }
        //             break;
        //     }
        // }

        //public void OnInstanceEvent(SyncedData<StreamInstance> streamInstance, StreamEvent eventType)
        //{
        //    switch (eventType)
        //    {
        //        case StreamEvent.Added:
        //            if (m_IdToBoxes.TryGetValue(streamInstance.key, out var box))
        //                SetStreamState(box, StreamState.Instance);
        //            break;
        //    }
        //}

        //public void OnInstanceDataEvent(SyncedData<StreamInstanceData> streamInstanceData, StreamEvent eventType)
        //{
        //    switch (eventType)
        //    {
        //        case StreamEvent.Added:
        //            if (m_IdToBoxes.TryGetValue(streamInstanceData.key, out var box))
        //                SetStreamState(box, StreamState.InstanceData);
        //            break;
        //    }
        //}

        [NetInput]
        void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            var instanceId = ctx.Data.InstanceId;
            var gameObject = ctx.Data.GameObject;

            m_IdToGameObjects[instanceId] = gameObject;
            if (m_IdToBoxes.TryGetValue(instanceId, out var box))
            {
                SetStreamState(box, StreamState.GameObject);
                // deactivate gameObject if box was inactive for loading hidden objects
                var wasActive = box.gameObject.activeSelf;
                gameObject.SetActive(wasActive);
                box.gameObject.SetActive(m_Settings.DisplayOnlyBoundingBoxes && wasActive);
            }
            if (m_Settings.DisplayOnlyBoundingBoxes)
                gameObject.SetActive(false);
            if (m_Settings.UseStaticBatching && m_IdToGameObjects.Count == m_IdToBoxes.Count)
                StaticBatchingUtility.Combine(gameObject.transform.parent.gameObject);
        }

        // TODO
        // public void OnGameObjectEvent(SyncedData<GameObject> gameObject, StreamEvent eventType)
        // {
        //     switch (eventType)
        //     {
        //         case StreamEvent.Removed:
        //             m_GameObjectsByStreamKey.Remove(gameObject.key);
        //             if (m_BoundingBoxesByStreamKey.TryGetValue(gameObject.key, out box))
        //                 box.gameObject.SetActive(true);
        //             break;
        //     }
        // }

        [NetInput]
        void UpdateVisibility(NetContext<UpdateVisibility> ctx)
        {
            foreach (var guid in ctx.Data.ShownInstances)
            {
                if (m_IdToGameObjects.TryGetValue(guid, out var obj))
                    obj.SetActive(!m_Settings.DisplayOnlyBoundingBoxes);
                if (m_IdToBoxes.TryGetValue(guid, out var box))
                    box.gameObject.SetActive(m_Settings.DisplayOnlyBoundingBoxes || obj == null);
            }

            foreach (var guid in ctx.Data.HiddenInstances)
            {
                if (m_IdToBoxes.TryGetValue(guid, out var box))
                    box.gameObject.SetActive(false);
                if (m_IdToGameObjects.TryGetValue(guid, out var obj))
                    obj.SetActive(false);
            }
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            if (fieldName == nameof(Settings.DisplayOnlyBoundingBoxes))
            {
                m_Settings.DisplayOnlyBoundingBoxes = (bool)newValue;
                SetDisplayOnlyBoundingBoxes(m_Settings.DisplayOnlyBoundingBoxes);
            }
            else if (fieldName == nameof(Settings.UseDebugMaterials))
            {
                m_Settings.UseDebugMaterials = (bool)newValue;
                foreach (var kvp in m_IdToBoxes)
                    SetStreamState(kvp.Value, kvp.Value.streamState);
            }
        }

        // void OnStreamingError(StreamingErrorEvent e)
        // {
        //     AddOrEditBoundingBox(e.Key, e.BoundingBox, StreamState.Invalid, true);
        // }

        void AddOrEditBoundingBox(Guid guid, AABB boundingBox, StreamState state, bool show)
        {
            if (!m_IdToBoxes.TryGetValue(guid, out var box))
            {
                box = GetFreeBoundingBoxReference();
                SetBoundingBoxValues(box, boundingBox);
                m_IdToBoxes.Add(guid, box);
            }

            box.gameObject.SetActive(show);
            SetStreamState(box, state);
        }

        void InitBoundingBoxPool()
        {
            // init bounding boxes
            m_BoundingBoxParent = m_Settings.BoundingBoxRoot != null
                ? m_Settings.BoundingBoxRoot
                : new GameObject("BoundingBoxRoot").transform;
            m_BoundingBoxPool = new List<BoundingBoxReference>(m_Settings.InitialBoundingBoxPoolSize);
            for (var i = 0; i < m_Settings.InitialBoundingBoxPoolSize; ++i)
                m_BoundingBoxPool.Add(CreateBoundingBoxReference());
        }

        BoundingBoxReference CreateBoundingBoxReference()
        {
            var obj = Object.Instantiate(m_Settings.BoundingBoxPrefab, m_BoundingBoxParent);
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            obj.gameObject.SetActive(false);
            return new BoundingBoxReference { gameObject = obj, meshRenderer = meshRenderer };
        }

        BoundingBoxReference GetFreeBoundingBoxReference()
        {
            if (m_BoundingBoxPool.Count <= 0)
                return CreateBoundingBoxReference();

            var lastIndex = m_BoundingBoxPool.Count - 1;
            var bb = m_BoundingBoxPool[lastIndex];
            m_BoundingBoxPool.RemoveAt(lastIndex);
            return bb;
        }

        void ReleaseBoundingBoxReference(BoundingBoxReference boundingBoxReference)
        {
            boundingBoxReference.meshRenderer.enabled = false;
            boundingBoxReference.streamState = StreamState.Asset;
            m_BoundingBoxPool.Add(boundingBoxReference);
        }

        void SetBoundingBoxValues(BoundingBoxReference boundingBoxReference, AABB box)
        {
            var min = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            var max = new Vector3(box.Max.X, box.Max.Y, box.Max.Z);
            EncapsulateGlobalBounds(min, max);
            var size = max - min;
            boundingBoxReference.gameObject.transform.localPosition = min + (size / 2);
            boundingBoxReference.gameObject.transform.localScale = size;
        }

        void SetStreamState(BoundingBoxReference boundingBoxReference, StreamState streamState)
        {
            if (boundingBoxReference.streamState == StreamState.Invalid)
                return;

            boundingBoxReference.streamState = streamState;

            if (CurrentMaterialPresets.TryGetValue(streamState, out var material))
            {
                boundingBoxReference.meshRenderer.sharedMaterial = material;
            }
        }

        void SetDisplayOnlyBoundingBoxes(bool displayOnlyBoundingBoxes)
        {
            foreach (var kvp in m_IdToGameObjects)
            {
                kvp.Value.SetActive(!displayOnlyBoundingBoxes);
                if (m_IdToBoxes.TryGetValue(kvp.Key, out var box))
                    box.meshRenderer.enabled = displayOnlyBoundingBoxes;
            }
        }

        void EncapsulateGlobalBounds(Vector3 min, Vector3 max)
        {
            if (min.Equals(max))
                return;

            if (!m_GlobalBounds.size.Equals(Vector3.zero))
            {
                m_GlobalBounds.Encapsulate(min);
                m_GlobalBounds.Encapsulate(max);
                return;
            }

            m_GlobalBounds.SetMinMax(min, max);
        }

        struct BoundingBoxReference
        {
            public GameObject gameObject;
            public MeshRenderer meshRenderer;
            public StreamState streamState;
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public int InitialBoundingBoxPoolSize = 10000;
            public GameObject BoundingBoxPrefab;
            
            [SerializeField, Transient(nameof(BoundingBoxRoot))]
            ExposedReference<Transform> m_BoundingBoxRoot;

            [HideInInspector]
            public Transform BoundingBoxRoot;

            public BoundingBoxMaterial[] DefaultBoundingBoxMaterials;
            public BoundingBoxMaterial[] DebugBoundingBoxMaterials;

            public bool DisplayOnlyBoundingBoxes;
            public bool UseDebugMaterials;
            public bool UseStaticBatching;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }

        public enum StreamState
        {
            Asset,
            FilteredAsset,
            Instance,
            InstanceData,
            GameObject,
            Removed,
            Invalid
        }

        [Serializable]
        public struct BoundingBoxMaterial
        {
            public StreamState streamState;
            public Material material;
        }
    }
}
