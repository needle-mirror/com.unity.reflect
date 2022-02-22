using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Geometry;
using UnityEngine;
using UnityEngine.Reflect;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    [Actor("0855f4d5-b9ca-4439-bfac-d050c59a8b5f", true)]
    public class BoundingBoxActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetOutput<ToggleGameObject> m_ToggleGameObjectOutput;
#pragma warning restore 649

        readonly Dictionary<DynamicGuid, BoundingBoxReference> m_IdToBoxes = new Dictionary<DynamicGuid, BoundingBoxReference>();
        readonly HashSet<DynamicGuid> m_GameObjectIds = new HashSet<DynamicGuid>();
        readonly Dictionary<StreamState, Material> m_DefaultMaterialPresets = new Dictionary<StreamState, Material>();
        readonly Dictionary<StreamState, Material> m_DebugMaterialPresets = new Dictionary<StreamState, Material>();

        int m_CurrentToggleValue;
        Transform m_BoundingBoxParent;
        List<BoundingBoxReference> m_BoundingBoxPool;

        Dictionary<StreamState, Material> CurrentMaterialPresets =>
            m_Settings == null || m_Settings.UseDebugMaterials ? m_DebugMaterialPresets : m_DefaultMaterialPresets;
        
        Dictionary<DynamicGuid, int> m_PendingToggles = new Dictionary<DynamicGuid, int>();
        HashSet<DynamicGuid> m_VisibleObjects = new HashSet<DynamicGuid>();
        List<(DynamicGuid Id, int NbEnabled)> m_VisibilityStates = new List<(DynamicGuid Id, int NbEnabled)>();

        public void Inject()
        {
            foreach (var boundingBoxMaterial in m_Settings.DefaultBoundingBoxMaterials)
                m_DefaultMaterialPresets.Add(boundingBoxMaterial.streamState, boundingBoxMaterial.material);

            foreach (var boundingBoxMaterial in m_Settings.DebugBoundingBoxMaterials)
                m_DebugMaterialPresets.Add(boundingBoxMaterial.streamState, boundingBoxMaterial.material);

            InitBoundingBoxPool();
            SetDisplayOnlyBoundingBoxes(m_Settings.DisplayOnlyBoundingBoxes);
        }

        public void Shutdown()
        {
            foreach (var kv in m_IdToBoxes)
                Object.Destroy(kv.Value.gameObject);

            foreach (var elem in m_BoundingBoxPool)
                Object.Destroy(elem.gameObject);

            m_IdToBoxes.Clear();
            m_GameObjectIds.Clear();
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            foreach (var entry in ctx.Data.Delta.Added)
                AddBoundingBox(entry.Id, entry.Data.Spatial.Box, StreamState.Asset);

            foreach (var entry in ctx.Data.Delta.Removed)
            {
                if (m_IdToBoxes.ContainsKey(entry.Id))
                    ReleaseBoundingBoxReference(m_IdToBoxes[entry.Id]);

                m_IdToBoxes.Remove(entry.Id);
                // TODO: not sure if removed from project or because of visibility (SyncTreeActor)
                // m_PendingToggles.Remove(entry.Id);
            }

            foreach (var entry in ctx.Data.Delta.Changed)
            {
                if (!m_IdToBoxes.TryGetValue(entry.Prev.Id, out var box))
                    continue;

                box.gameObject.SetActive(false);
                SetBoundingBoxValues(box, entry.Next.Data.Spatial.Box);
                m_IdToBoxes.Remove(entry.Prev.Id);
                m_PendingToggles.Remove(entry.Prev.Id);
                m_IdToBoxes.Add(entry.Next.Id, box);
            }
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                var id = go.Id;
                m_GameObjectIds.Add(id);

                if (m_PendingToggles.TryGetValue(id, out var nbEnabled))
                {
                    m_PendingToggles.Remove(id);
                    ChangeOrEnqueueVisibilityChange(id, nbEnabled);
                }
                else if (m_VisibleObjects.Contains(id))
                {
                    ChangeOrEnqueueVisibilityChange(id, 1);
                }

                if (m_Settings.DisplayOnlyBoundingBoxes)
                    ChangeOrEnqueueVisibilityChange(id, m_CurrentToggleValue);
            }
            
            SendVisibilityStates();

            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                m_GameObjectIds.Remove(go.Id);

                if (m_IdToBoxes.TryGetValue(go.Id, out var box))
                    box.gameObject.SetActive(true);
            }

            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectEnabling(PipeContext<GameObjectEnabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                go.GameObject.SetActive(true);

                if (m_IdToBoxes.TryGetValue(go.Id, out var box))
                    box.gameObject.SetActive(false);
            }

            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDisabling(PipeContext<GameObjectDisabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                go.GameObject.SetActive(false);

                if (!m_Settings.DisplayOnlyBoundingBoxes || !m_VisibleObjects.Contains(go.Id))
                    continue;
                
                if (m_IdToBoxes.TryGetValue(go.Id, out var box))
                    box.gameObject.SetActive(true);
            }

            ctx.Continue();
        }

        [NetInput]
        void OnUpdateVisibility(NetContext<UpdateVisibility> ctx)
        {
            foreach (var guid in ctx.Data.HiddenInstances)
            {
                m_VisibleObjects.Remove(guid);
                ChangeOrEnqueueVisibilityChange(guid, -1);
            }

            foreach (var guid in ctx.Data.ShownInstances)
            {
                m_VisibleObjects.Add(guid);
                ChangeOrEnqueueVisibilityChange(guid, 1);
            }
            
            SendVisibilityStates();
        }

        [NetInput]
        void OnTransformObjectBounds(NetContext<TransformObjectBounds> ctx)
        {
            for (var i = 0; i < ctx.Data.Ids.Count; ++i)
            {
                if (m_IdToBoxes.TryGetValue(ctx.Data.Ids[i], out var box))
                {
                    SetBoundingBoxValues(box, ctx.Data.TransformMatrix * new Aabb(box.OriginalMin, box.OriginalMax, new Aabb.FromMinMax()));
                }
            }
        }

        void ChangeOrEnqueueVisibilityChange(DynamicGuid id, int nbEnabled)
        {
            if (m_Settings.DisplayOnlyBoundingBoxes)
            {
                if (m_IdToBoxes.TryGetValue(id, out var box))
                    box.gameObject.SetActive(nbEnabled > 0);
            }

            if (m_GameObjectIds.Contains(id))
            {
                SetVisibilityState(id, nbEnabled);

                if (!m_Settings.DisplayOnlyBoundingBoxes && m_IdToBoxes.TryGetValue(id, out var box))
                    box.gameObject.SetActive(false);
            }
            else
            {
                if (!m_PendingToggles.TryGetValue(id, out var cur))
                    m_PendingToggles.Add(id, 0);
                var newVal = cur + nbEnabled;
                m_PendingToggles[id] = newVal;

                if (m_IdToBoxes.TryGetValue(id, out var box))
                    box.gameObject.SetActive(newVal > 0);
            }
        }

        [EventInput]
        void OnStreamingError(EventContext<StreamingError> ctx)
        {
            if (!m_IdToBoxes.TryGetValue(ctx.Data.Id, out var box))
                return;

            SetStreamState(box, StreamState.Invalid);
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

        void AddBoundingBox(DynamicGuid dynamicId, Aabb boundingBox, StreamState state)
        {
            if (!m_IdToBoxes.TryGetValue(dynamicId, out var box))
            {
                box = GetFreeBoundingBoxReference();
                box.OriginalMin = boundingBox.Min;
                box.OriginalMax = boundingBox.Max;
                
                SetBoundingBoxValues(box, boundingBox);
                m_IdToBoxes.Add(dynamicId, box);
            }

            box.gameObject.SetActive(false);
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
            boundingBoxReference.gameObject.SetActive(false);
            boundingBoxReference.streamState = StreamState.Asset;
            m_BoundingBoxPool.Add(boundingBoxReference);
        }

        void SetBoundingBoxValues(BoundingBoxReference boundingBoxReference, Aabb box)
        {
            SetBoundingBoxValues(boundingBoxReference, box.Min.ToUnity(), box.Max.ToUnity());
        }

        void SetBoundingBoxValues(BoundingBoxReference boundingBoxReference, Vector3 min, Vector3 max)
        {
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
            var overrideValue = displayOnlyBoundingBoxes ? -m_Settings.ToggleOverrideValue : m_Settings.ToggleOverrideValue;

            if (m_CurrentToggleValue + overrideValue > 0) 
                return;
            
            m_CurrentToggleValue += overrideValue;
            foreach (var id in m_GameObjectIds)
                SetVisibilityState(id, overrideValue);
                
            SendVisibilityStates();
        }

        void SetVisibilityState(DynamicGuid id, int nbEnabled)
        {
            m_VisibilityStates.Add((id, nbEnabled));
        }

        void SendVisibilityStates()
        {
            if (m_VisibilityStates.Count <= 0)
                return;
            
            m_ToggleGameObjectOutput.Send(new ToggleGameObject(m_VisibilityStates));
            m_VisibilityStates = new List<(DynamicGuid Id, int NbEnabled)>();
        }

        struct BoundingBoxReference
        {
            public GameObject gameObject;
            public MeshRenderer meshRenderer;
            public StreamState streamState;

            public System.Numerics.Vector3 OriginalMin;
            public System.Numerics.Vector3 OriginalMax;
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

            public int ToggleOverrideValue = 10000;
            public bool DisplayOnlyBoundingBoxes;
            public bool UseDebugMaterials;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }

        public enum StreamState
        {
            // Unused must stay there for asset deserialization to work...
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
