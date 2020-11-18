using System;
using System.Collections.Generic;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public class MemoryTrackerCacheCreatedEvent<TObject>
    {
        public MemoryTracker.Handle<SyncId, TObject> handle;

        public MemoryTrackerCacheCreatedEvent(MemoryTracker.Handle<SyncId, TObject> handle)
        {
            this.handle = handle;
        }
    }

    public abstract class SyncModelConverter<TModel, TObject> : IReflectNodeProcessor where TModel : ISyncModel where TObject : Object
    {
        readonly EventHub m_Hub;
        readonly MemoryTracker m_MemTracker;

        MemoryTracker.Handle<SyncId, TObject> m_ObjectHandle;
        readonly IOutput<SyncedData<TObject>> m_Output;

        public SyncModelConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<TObject>> output)
        {
            m_Hub = hub;
            m_MemTracker = memTracker;

            m_Output = output;
        }
        
        public TObject GetFromCache(SyncId id)
        {
            if (!m_MemTracker.TryGetValue(m_ObjectHandle, id, out var obj))
            {
                Debug.LogWarning($"Key not found {id} {typeof(TObject)}");
                return null;
            }

            return obj;
        }
        
        public void OnStreamEvent(SyncedData<TModel> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                var obj = Import(stream.data);

                if (m_MemTracker.ContainsKey(m_ObjectHandle, stream.data.Id))
                {
                    Debug.Log("Duplicate " + obj.name);
                }

                m_MemTracker.Set(m_ObjectHandle, stream.data.Id, obj);

                m_Output.SendStreamAdded(new SyncedData<TObject>(stream.key, obj));
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                if (m_MemTracker.TryGetValue(m_ObjectHandle, stream.data.Id, out var obj))
                {
                    ReImport(stream.data, obj);
                }
                else
                {
                    // We may receive "Changed" for items that were
                    // loaded at some point, but are not in the cache anymore
                    obj = Import(stream.data);
                    m_MemTracker.Set(m_ObjectHandle, stream.data.Id, obj);
                }
                
                m_Output.SendStreamChanged(new SyncedData<TObject>(stream.key, obj));
            }
        }

        protected abstract TObject Import(TModel model);
        
        protected abstract void ReImport(TModel model, TObject obj);

        protected abstract Action<TObject> GetDestructor();

        public void OnPipelineInitialized()
        {
            m_ObjectHandle = m_MemTracker.CreateCache<SyncId, TObject>(GetDestructor());
            m_Hub.Broadcast(new MemoryTrackerCacheCreatedEvent<TObject>(m_ObjectHandle));
        }

        public void OnPipelineShutdown()
        {
            m_MemTracker.DestroyCache(m_ObjectHandle);
        }
    }
    
    [Serializable]
    public class MeshConverterNode : ReflectNode<MeshConverter>, IMeshCache
    {
        public SyncMeshInput input = new SyncMeshInput();
        public MeshOutput output = new MeshOutput();

        protected override MeshConverter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new MeshConverter(hook.services.eventHub, hook.services.memoryTracker, output);
            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public Mesh GetMesh(SyncId id)
        {
            return processor.GetFromCache(id);
        }
    }

    public class MeshConverter : SyncModelConverter<SyncMesh, Mesh>
    {
        readonly SyncMeshImporter m_MeshImporter;

        public MeshConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<Mesh>> output) : base(hub, memTracker, output)
        {
            m_MeshImporter = new SyncMeshImporter();
        }

        protected override Mesh Import(SyncMesh syncMesh)
        {
            return m_MeshImporter.Import(syncMesh, null);
        }

        protected override void ReImport(SyncMesh model, Mesh obj)
        {
            m_MeshImporter.Reimport(model, obj, null);
        }

        protected override Action<Mesh> GetDestructor()
        {
            return Object.Destroy;
        }
    }
    
    [Serializable]
    public class TextureConverterNode : ReflectNode<TextureConverter>, ITextureCache
    {
        public SyncTextureInput input = new SyncTextureInput();
        public Texture2DOutput output = new Texture2DOutput();

        protected override TextureConverter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new TextureConverter(hook.services.eventHub, hook.services.memoryTracker, output);

            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public virtual Texture2D GetTexture(SyncId id)
        {
            return processor.GetFromCache(id);
        }
    }

    public class TextureConverter : SyncModelConverter<SyncTexture, Texture2D>
    {
        readonly SyncTextureImporter m_TextureImporter;

        public TextureConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<Texture2D>> output) : base(hub, memTracker, output)
        {
            m_TextureImporter = new SyncTextureImporter();
        }

        protected override Texture2D Import(SyncTexture syncTexture)
        {
            return m_TextureImporter.Import(syncTexture, null);
        }

        protected override void ReImport(SyncTexture model, Texture2D obj)
        {
            m_TextureImporter.Reimport(model, obj, null);
        }

        protected override Action<Texture2D> GetDestructor()
        {
            return Object.Destroy;
        }
    }
    
    [Serializable]
    public class TextureCacheParam : Param<ITextureCache> { }

    [Serializable]
    public class MaterialConverterNode : ReflectNode<MaterialConverter>, IMaterialCache
    {
        public TextureCacheParam textureCacheParam = new TextureCacheParam();
        
        public SyncMaterialInput input = new SyncMaterialInput();
        public MaterialOutput output = new MaterialOutput();

        protected override MaterialConverter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new MaterialConverter(hook.services.eventHub, hook.services.memoryTracker, textureCacheParam.value, output);

            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public virtual Material GetMaterial(SyncId id)
        {
            return processor.GetFromCache(id);
        }
    }

    [Serializable]
    public class MaterialConverter : SyncModelConverter<SyncMaterial, Material>
    {
        protected readonly ITextureCache m_TextureCache;
        readonly SyncMaterialImporter m_Importer;

        public MaterialConverter(EventHub hub, MemoryTracker memTracker, ITextureCache textureCache, IOutput<SyncedData<Material>> output)
            : base(hub, memTracker, output)
        {
            m_Importer = new SyncMaterialImporter();
            m_TextureCache = textureCache;
        }

        protected override Material Import(SyncMaterial syncMaterial)
        {
            return m_Importer.Import(syncMaterial, m_TextureCache);
        }

        protected override void ReImport(SyncMaterial model, Material obj)
        {
            m_Importer.Reimport(model, obj, m_TextureCache);
        }

        protected override Action<Material> GetDestructor()
        {
            return Object.Destroy;
        }
    }

    public struct StreamInstanceData
    {
        public readonly StreamInstance instance;
        public readonly SyncObject syncObject;
        public StreamInstanceData(StreamInstance instance, SyncObject syncObject)
        {
            this.instance = instance;
            this.syncObject = syncObject;
        }
    }
    
    [Serializable]
    public class MaterialCacheParam : Param<IMaterialCache> { }
    
    [Serializable]
    public class MeshCacheParam : Param<IMeshCache> { }
    
    [Serializable]
    public class InstanceConverterNode : ReflectNode<InstanceConverter>
    {
        public MaterialCacheParam materialCacheParam = new MaterialCacheParam();
        public MeshCacheParam meshCacheParam = new MeshCacheParam();
        
        public StreamInstanceDataInput input = new StreamInstanceDataInput();
        public GameObjectOutput output = new GameObjectOutput();

        [SerializeField]
        ExposedReference<Transform> m_Root;

        public void SetRoot(Transform root, IExposedPropertyTable resolver)
        {
            resolver.SetReferenceValue(m_Root.exposedName, root);
        }

        protected override InstanceConverter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var root = m_Root.Resolve(resolver);
            if (root == null)
            {
                root = new GameObject("root").transform;
            }
            
            var node = new InstanceConverter(hook.services.eventHub, hook.services.memoryTracker, root, materialCacheParam.value, meshCacheParam.value, output);

            input.streamBegin = output.SendBegin;
            input.streamEvent = node.OnStreamEvent;
            input.streamEnd = output.SendEnd;

            return node;
        }
    }

    public class InstanceConverter : IReflectNodeProcessor, IObjectCache
    {
        struct OriginalInstance
        {
            public int NbActiveInstances;
            public SyncObjectBinding ObjectBinding;
            public ObjectDependencies Dependencies;

            public OriginalInstance(int nbActiveInstances, SyncObjectBinding objectBinding, ObjectDependencies dependencies)
            {
                NbActiveInstances = nbActiveInstances;
                ObjectBinding = objectBinding;
                Dependencies = dependencies;
            }
        }

        readonly EventHub m_Hub;
        readonly MemoryTracker m_MemTracker;
        readonly IMaterialCache m_MaterialCache;
        readonly IMeshCache m_MeshCache;

        readonly Transform m_Root;

        readonly Dictionary<string, OriginalInstance> m_Originals;
        readonly Dictionary<string, SyncObject> m_SyncObjects;
        readonly Dictionary<StreamKey, SyncObjectBinding> m_Instances;

        DataOutput<GameObject> m_Output;

        EventHub.Group m_HubGroup;
        MemoryTracker.Handle<SyncId, Mesh> m_MeshesHandle;

        public InstanceConverter(EventHub hub, MemoryTracker memTracker, Transform root, IMaterialCache materialCache, IMeshCache meshCache, DataOutput<GameObject> output)
        {
            m_Hub = hub;
            m_MemTracker = memTracker;
            m_MaterialCache = materialCache;
            m_MeshCache = meshCache;
            m_Output = output;

            m_Originals = new Dictionary<string, OriginalInstance>();

            m_Root = root;

            m_SyncObjects = new Dictionary<string, SyncObject>();
            m_Instances = new Dictionary<StreamKey, SyncObjectBinding>();

            m_HubGroup = m_Hub.CreateGroup();
            m_Hub.Subscribe<PeriodicMemoryEvent>(m_HubGroup, OnPeriodicMemoryEvent);
            m_Hub.Subscribe<MemoryTrackerCacheCreatedEvent<Mesh>>(m_HubGroup, e => m_MeshesHandle = e.handle);
        }
        
        protected virtual SyncObjectBinding ImportInstance(StreamInstance stream)
        {
            var syncObjectBinding = SyncPrefabImporter.CreateInstance(m_Root, stream.instance, this);
#if UNITY_EDITOR
            var box = stream.boundingBox;
            var min = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            syncObjectBinding.bounds = new Bounds(min, Vector3.zero);
            syncObjectBinding.bounds.Encapsulate(new Vector3(box.Max.X, box.Max.Y, box.Max.Z));
#endif
            return syncObjectBinding;
        }

        public SyncObjectBinding CreateInstance(string objectId)
        {
            // Did we already instantiate an instance for the same?
            if (m_Originals.TryGetValue(objectId, out var original) &&
                original.ObjectBinding != null)
            {
                ++original.NbActiveInstances;
                m_Originals[objectId] = original;
                TrackExistingDependencies(original.Dependencies);
                return Object.Instantiate(original.ObjectBinding);
            }
            
            // Did we receive the SyncObject?
            var syncObject = m_SyncObjects[objectId];

            var configs = new SyncObjectImportConfig
            {
                settings = new SyncObjectImportSettings
                {
                    defaultMaterial = ReflectMaterialManager.defaultMaterial, importLights = true
                },
                materialCache = m_MaterialCache, meshCache = m_MeshCache
            };

            var importer = new SyncObjectImporter();
            var (dependencies, gameObject) = importer.ImportAndGetDependencies(syncObject, configs);

            var comp = gameObject.AddComponent<SyncObjectBinding>();

            TrackDependencies(dependencies);
            m_Originals[objectId] = new OriginalInstance(1, comp, dependencies);

            return comp;
        }

        public void OnStreamEvent(SyncedData<StreamInstanceData> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                m_SyncObjects[stream.data.syncObject.Id.Value] = stream.data.syncObject;
                var syncObjectBinding = ImportInstance(stream.data.instance);

                var key = stream.data.instance.key;
                m_Instances[key] = syncObjectBinding;
                
                m_Output.SendStreamAdded(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                var key = stream.data.instance.key;
                // The instance either moved, or the metadata changed.
                var syncObjectBinding = m_Instances[key];
                
                ImportersUtils.SetTransform(syncObjectBinding.transform, stream.data.instance.instance.Transform);
                ImportersUtils.SetMetadata(syncObjectBinding.gameObject, stream.data.instance.instance.Metadata);

                m_Output.SendStreamChanged(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));
            }
            else if (streamEvent == StreamEvent.Removed)
            {
                var key = stream.data.instance.key;
                // The instance either moved, or the metadata changed.
                var syncObjectBinding = m_Instances[key];

                m_Instances.Remove(key);
                
                m_Output.SendStreamRemoved(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));

                if (m_Originals.TryGetValue(stream.data.syncObject.Id.Value, out var original))
                {
                    UntrackDependencies(original.Dependencies);

                    if (original.NbActiveInstances == 1)
                    {
                        m_Originals.Remove(stream.data.syncObject.Id.Value);
                    }
                    else
                    {
                        --original.NbActiveInstances;
                        m_Originals[stream.data.syncObject.Id.Value] = original;
                    }
                }

                Object.Destroy(syncObjectBinding.gameObject);
            }
        }

        public void OnPipelineInitialized()
        {
        }

        public void OnPipelineShutdown()
        {
            m_Hub.DestroyGroup(m_HubGroup);

            if (m_Root == null || m_Root.transform == null)
                return;
            
            foreach (Transform child in m_Root.transform)
            {
                Object.Destroy(child.gameObject);
            }
        }

        void TrackExistingDependencies(ObjectDependencies dependencies)
        {
            foreach (var item in dependencies.meshes)
            {
                m_MemTracker.Acquire(m_MeshesHandle, item.Id);
            }
        }

        void TrackDependencies(ObjectDependencies dependencies)
        {
            foreach (var item in dependencies.meshes)
            {
                m_MemTracker.SetAndAcquire(m_MeshesHandle, item.Id, item.Mesh);
            }
        }

        void UntrackDependencies(ObjectDependencies dependencies)
        {
            foreach (var item in dependencies.meshes)
            {
                m_MemTracker.TryRelease(m_MeshesHandle, item.Id);
            }
        }

        void OnPeriodicMemoryEvent(PeriodicMemoryEvent e)
        {
            if (e.Level >= MemoryLevel.High)
            {
                m_MemTracker.ClearInactiveItems(m_MeshesHandle);
            }
            else if (e.Level == MemoryLevel.Medium)
            {
                m_MemTracker.ClearInactiveItemsOlderThan(m_MeshesHandle, TimeSpan.FromSeconds(5));
            }
        }
    }
}