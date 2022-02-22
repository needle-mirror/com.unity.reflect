using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public class MemoryTrackerCacheCreatedEvent<TObject>
    {
        public MemoryTracker.Handle<StreamKey, TObject> handle;

        public MemoryTrackerCacheCreatedEvent(MemoryTracker.Handle<StreamKey, TObject> handle)
        {
            this.handle = handle;
        }
    }

    public abstract class SyncModelConverter<TModel, TObject> : IReflectNodeProcessor where TModel : ISyncModel where TObject : Object
    {
        readonly EventHub m_Hub;
        readonly MemoryTracker m_MemTracker;

        MemoryTracker.Handle<StreamKey, TObject> m_ObjectHandle;
        readonly IOutput<SyncedData<TObject>> m_Output;

        public SyncModelConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<TObject>> output)
        {
            m_Hub = hub;
            m_MemTracker = memTracker;

            m_Output = output;
        }
        
        public TObject GetFromCache(StreamKey key)
        {
            if (!m_MemTracker.TryGetValue(m_ObjectHandle, key, out var obj))
            {
                Debug.LogWarning($"Key not found {key} {typeof(TObject)}");
                return null;
            }

            return obj;
        }
        
        public void OnStreamEvent(SyncedData<TModel> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                var obj = Import(stream);

                if (m_MemTracker.ContainsKey(m_ObjectHandle, stream.key))
                {
                    Debug.Log("Duplicate " + obj.name);
                }

                m_MemTracker.Set(m_ObjectHandle, stream.key, obj);

                m_Output.SendStreamAdded(new SyncedData<TObject>(stream.key, obj));
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                if (m_MemTracker.TryGetValue(m_ObjectHandle, stream.key, out var obj))
                {
                    ReImport(stream, obj);
                }
                else
                {
                    // We may receive "Changed" for items that were
                    // loaded at some point, but are not in the cache anymore
                    obj = Import(stream);
                    m_MemTracker.Set(m_ObjectHandle, stream.key, obj);
                }
                
                m_Output.SendStreamChanged(new SyncedData<TObject>(stream.key, obj));
            }
        }

        protected abstract TObject Import(SyncedData<TModel> model);
        
        protected abstract void ReImport(SyncedData<TModel> model, TObject obj);

        protected abstract Action<TObject> GetDestructor();

        public void OnPipelineInitialized()
        {
            m_ObjectHandle = m_MemTracker.CreateCache<StreamKey, TObject>(GetDestructor());
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
            var p = new MeshConverter(hook.Services.EventHub, hook.Services.MemoryTracker, output);
            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public Mesh GetMesh(StreamKey key)
        {
            return processor.GetFromCache(key);
        }
    }

    public class MeshConverter : SyncModelConverter<SyncMesh, Mesh>
    {
        readonly SyncMeshImporter m_MeshImporter;

        public MeshConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<Mesh>> output) : base(hub, memTracker, output)
        {
            m_MeshImporter = new SyncMeshImporter();
        }

        protected override Mesh Import(SyncedData<SyncMesh> syncMesh)
        {
            return m_MeshImporter.Import(syncMesh, null);
        }

        protected override void ReImport(SyncedData<SyncMesh> model, Mesh obj)
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
            var p = new TextureConverter(hook.Services.EventHub, hook.Services.MemoryTracker, output);

            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public virtual Texture2D GetTexture(StreamKey key)
        {
            return processor.GetFromCache(key);
        }
    }

    public class TextureConverter : SyncModelConverter<SyncTexture, Texture2D>
    {
        readonly SyncTextureImporter m_TextureImporter;

        public TextureConverter(EventHub hub, MemoryTracker memTracker, IOutput<SyncedData<Texture2D>> output) : base(hub, memTracker, output)
        {
            m_TextureImporter = new SyncTextureImporter();
        }

        protected override Texture2D Import(SyncedData<SyncTexture> syncTexture)
        {
            return m_TextureImporter.Import(syncTexture, null);
        }

        protected override void ReImport(SyncedData<SyncTexture>  model, Texture2D obj)
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
            var p = new MaterialConverter(hook.Services.EventHub, hook.Services.MemoryTracker, textureCacheParam.value, output);

            input.streamEvent = p.OnStreamEvent;

            return p;
        }

        public virtual Material GetMaterial(StreamKey key)
        {
            return processor.GetFromCache(key);
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

        protected override Material Import(SyncedData<SyncMaterial> syncMaterial)
        {
            return m_Importer.Import(syncMaterial, m_TextureCache);
        }

        protected override void ReImport(SyncedData<SyncMaterial> syncMaterial, Material obj)
        {
            m_Importer.Reimport(syncMaterial, obj, m_TextureCache);
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

        [SerializeField, Tooltip("If true, will create a parent GameObject for each Reflect model source.")]
        bool m_GenerateSourceRoots = true;

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
            
            var node = new InstanceConverter(hook.Services.EventHub, hook.Services.MemoryTracker, root, m_GenerateSourceRoots,
                materialCacheParam.value, meshCacheParam.value, output);

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
        readonly ISyncLightImporter m_SyncLightImporter;

        readonly Transform m_Root;
        readonly Dictionary<string, Transform> m_SourceRoots;

        readonly Dictionary<StreamKey, OriginalInstance> m_Originals;
        readonly Dictionary<StreamKey, SyncObject> m_SyncObjects;
        readonly Dictionary<StreamKey, SyncObjectBinding> m_Instances;

        DataOutput<GameObject> m_Output;

        EventHub.Group m_HubGroup;
        MemoryTracker.Handle<StreamKey, Mesh> m_MeshesHandle;

        public InstanceConverter(EventHub hub, MemoryTracker memTracker, Transform root, bool generateSourceRoots,
            IMaterialCache materialCache, IMeshCache meshCache, DataOutput<GameObject> output)
        : this(hub, memTracker, root, generateSourceRoots, materialCache, meshCache, null, output)
        {
        }
        
        public InstanceConverter(EventHub hub, MemoryTracker memTracker, Transform root, bool generateSourceRoots,
            IMaterialCache materialCache, IMeshCache meshCache, ISyncLightImporter lightImporter, DataOutput<GameObject> output)
        {
            m_Hub = hub;
            m_MemTracker = memTracker;
            m_MaterialCache = materialCache;
            m_MeshCache = meshCache;
            m_Output = output;
            m_SyncLightImporter = lightImporter;

            m_Originals = new Dictionary<StreamKey, OriginalInstance>();

            m_Root = root;
            m_SourceRoots = generateSourceRoots ? new Dictionary<string, Transform>() : null;

            m_SyncObjects = new Dictionary<StreamKey, SyncObject>();
            m_Instances = new Dictionary<StreamKey, SyncObjectBinding>();

            m_HubGroup = m_Hub.CreateGroup();
            m_Hub.Subscribe<PeriodicMemoryEvent>(m_HubGroup, OnPeriodicMemoryEvent);
            m_Hub.Subscribe<MemoryTrackerCacheCreatedEvent<Mesh>>(m_HubGroup, e => m_MeshesHandle = e.handle);
        }

        public bool TryGetInstance(StreamKey key, out SyncObjectBinding value) => m_Instances.TryGetValue(key, out value);

        protected virtual SyncObjectBinding ImportInstance(StreamInstance stream)
        {
            var syncObjectBinding = SyncPrefabImporter.CreateInstance(GetInstanceRoot(stream.key.source), stream.key.source, stream.instance, this);
#if UNITY_EDITOR
            var box = stream.boundingBox;
            var min = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            syncObjectBinding.bounds = new Bounds(min, Vector3.zero);
            syncObjectBinding.bounds.Encapsulate(new Vector3(box.Max.X, box.Max.Y, box.Max.Z));
#endif
            return syncObjectBinding;
        }

        Transform GetInstanceRoot(string source)
        {
            if (m_SourceRoots == null)
                return m_Root;

            if (!m_SourceRoots.TryGetValue(source, out var root))
            {
                root = new GameObject(source).transform;
                root.parent = m_Root;
                root.position = Vector3.zero;
                root.rotation = Quaternion.identity;
                root.localScale = Vector3.one;

                m_SourceRoots[source] = root;
            }

            return root;
        }

        public SyncObjectBinding CreateInstance(StreamKey objectKey)
        {
            // Did we already instantiate an instance for the same?
            if (m_Originals.TryGetValue(objectKey, out var original) &&
                original.ObjectBinding != null)
            {
                ++original.NbActiveInstances;
                m_Originals[objectKey] = original;
                TrackExistingDependencies(original.Dependencies);
                return Object.Instantiate(original.ObjectBinding);
            }
            
            // Did we receive the SyncObject?
            var syncObject = m_SyncObjects[objectKey];

            var configs = new SyncObjectImportConfig
            {
                settings = new SyncObjectImportSettings
                {
                    defaultMaterial = ReflectMaterialManager.defaultMaterial, importLights = true
                },
                materialCache = m_MaterialCache, meshCache = m_MeshCache, lightImport = m_SyncLightImporter
            };

            var importer = new SyncObjectImporter();
            var (dependencies, gameObject) = importer.ImportAndGetDependencies(objectKey.source, syncObject, configs);

            var comp = gameObject.AddComponent<SyncObjectBinding>();

            TrackDependencies(dependencies);
            m_Originals[objectKey] = new OriginalInstance(1, comp, dependencies);

            return comp;
        }

        public void OnStreamEvent(SyncedData<StreamInstanceData> stream, StreamEvent streamEvent)
        {
            var objectKey = new StreamKey(stream.key.source, PersistentKey.GetKey<SyncObject>(stream.data.syncObject.Id));
            
            if (streamEvent == StreamEvent.Added)
            {
                m_SyncObjects[objectKey] = stream.data.syncObject;
                var syncObjectBinding = ImportInstance(stream.data.instance);

                var key = stream.data.instance.key;
                m_Instances[key] = syncObjectBinding;
                syncObjectBinding.streamKey = key;

                m_Output.SendStreamAdded(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                var key = stream.data.instance.key;
                // The instance moved, or the name/metadata changed.
                var syncObjectBinding = m_Instances[key];
                
                ImportersUtils.SetTransform(syncObjectBinding.transform, stream.data.instance.instance.Transform);
                ImportersUtils.SetMetadata(syncObjectBinding.gameObject, stream.data.instance.instance.Metadata);
                syncObjectBinding.gameObject.name = stream.data.instance.instance.Name;

                m_Output.SendStreamChanged(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));
            }
            else if (streamEvent == StreamEvent.Removed)
            {
                var key = stream.data.instance.key;
                // The instance either moved, or the metadata changed.
                var syncObjectBinding = m_Instances[key];

                m_Instances.Remove(key);
                
                m_Output.SendStreamRemoved(new SyncedData<GameObject>(key, syncObjectBinding.gameObject));

                if (m_Originals.TryGetValue(objectKey, out var original))
                {
                    UntrackDependencies(original.Dependencies);

                    if (original.NbActiveInstances == 1)
                    {
                        m_Originals.Remove(objectKey);
                    }
                    else
                    {
                        --original.NbActiveInstances;
                        m_Originals[objectKey] = original;
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
                m_MemTracker.Acquire(m_MeshesHandle, item.key);
            }
        }

        void TrackDependencies(ObjectDependencies dependencies)
        {
            foreach (var item in dependencies.meshes)
            {
                m_MemTracker.SetAndAcquire(m_MeshesHandle, item.key, item.Mesh);
            }
        }

        void UntrackDependencies(ObjectDependencies dependencies)
        {
            foreach (var item in dependencies.meshes)
            {
                m_MemTracker.TryRelease(m_MeshesHandle, item.key);
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
