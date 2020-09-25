using System;
using System.Collections.Generic;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    
    public abstract class SyncModelConverter<TModel, TObject> : IReflectNodeProcessor where TModel : ISyncModel where TObject : Object
    {
        readonly IOutput<SyncedData<TObject>> m_Output;
        readonly Dictionary<SyncId, TObject> m_Caches;

        public SyncModelConverter(IOutput<SyncedData<TObject>> output)
        {
            m_Output = output;
            m_Caches = new Dictionary<SyncId, TObject>();
        }
        
        public TObject GetFromCache(SyncId id)
        {
            if (!m_Caches.ContainsKey(id))
            {
                Debug.LogWarning($"Key not found {id} {typeof(TObject)}");
                return null;
            }

            return m_Caches[id];
        }
        
        public void OnStreamEvent(SyncedData<TModel> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                var obj = Import(stream.data);

                if (m_Caches.ContainsKey(stream.data.Id))
                {
                    Debug.Log("Duplicate " + obj.name);
                }

                m_Caches[stream.data.Id] = obj;
                
                m_Output.SendStreamAdded(new SyncedData<TObject>(stream.key, obj));
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                var obj = m_Caches[stream.data.Id];
                
                ReImport(stream.data, obj);
                
                m_Output.SendStreamChanged(new SyncedData<TObject>(stream.key, obj));
                
            }
        }

        protected abstract TObject Import(TModel model);
        
        protected abstract void ReImport(TModel model, TObject obj);
        
        public void OnPipelineInitialized()
        {
        }

        public void OnPipelineShutdown()
        {
            foreach (var obj in m_Caches.Values)
            {
                Object.Destroy(obj);
            }
            
            m_Caches.Clear();
        }
    }
    
    [Serializable]
    public class MeshConverterNode : ReflectNode<MeshConverter>, IMeshCache
    {
        public SyncMeshInput input = new SyncMeshInput();
        public MeshOutput output = new MeshOutput();

        protected override MeshConverter Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new MeshConverter(output);
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

        public MeshConverter(IOutput<SyncedData<Mesh>> output) : base(output)
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
    }
    
    [Serializable]
    public class TextureConverterNode : ReflectNode<TextureConverter>, ITextureCache
    {
        public SyncTextureInput input = new SyncTextureInput();
        public Texture2DOutput output = new Texture2DOutput();

        protected override TextureConverter Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new TextureConverter(output);

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

        public TextureConverter(IOutput<SyncedData<Texture2D>> output) : base(output)
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
    }
    
    [Serializable]
    public class TextureCacheParam : Param<ITextureCache> { }

    [Serializable]
    public class MaterialConverterNode : ReflectNode<MaterialConverter>, IMaterialCache
    {
        public TextureCacheParam textureCacheParam = new TextureCacheParam();
        
        public SyncMaterialInput input = new SyncMaterialInput();
        public MaterialOutput output = new MaterialOutput();

        protected override MaterialConverter Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new MaterialConverter(textureCacheParam.value, output);

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

        public MaterialConverter(ITextureCache textureCache, IOutput<SyncedData<Material>> output) : base(output)
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

        protected override InstanceConverter Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var root = m_Root.Resolve(resolver);
            if (root == null)
            {
                root = new GameObject("root").transform;
            }
            
            var node = new InstanceConverter(root, materialCacheParam.value, meshCacheParam.value, output);

            input.streamBegin = output.SendBegin;
            input.streamEvent = node.OnStreamEvent;
            input.streamEnd = output.SendEnd;

            return node;
        }
    }

    public class InstanceConverter : IReflectNodeProcessor, IObjectCache
    {
        readonly IMaterialCache m_MaterialCache;
        readonly IMeshCache m_MeshCache;
        
        readonly Transform m_Root;

        readonly Dictionary<string, SyncObjectBinding> m_Originals;
        readonly Dictionary<string, SyncObject> m_SyncObjects;
        readonly Dictionary<StreamKey, SyncObjectBinding> m_Instances;

        DataOutput<GameObject> m_Output;

        public InstanceConverter(Transform root, IMaterialCache materialCache, IMeshCache meshCache, DataOutput<GameObject> output)
        {
            m_MaterialCache = materialCache;
            m_MeshCache = meshCache;
            m_Output = output;

            m_Originals = new Dictionary<string, SyncObjectBinding>();

            m_Root = root;

            m_SyncObjects = new Dictionary<string, SyncObject>();
            m_Instances = new Dictionary<StreamKey, SyncObjectBinding>();
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

        public SyncObjectBinding CreateInstance(string key)
        {
            // Did we already instantiated an instance for the same?
            if (m_Originals.TryGetValue(key, out var original))
            {
                if (original == null)
                {
                    m_Originals.Remove(key);
                }
                else
                {
                    return Object.Instantiate(original);
                }
            }
            
            // Did we received the SyncObject?
            var syncObject = m_SyncObjects[key];

            var configs = new SyncObjectImportConfig
            {
                settings = new SyncObjectImportSettings
                {
                    defaultMaterial = ReflectMaterialManager.defaultMaterial, importLights = true
                },
                materialCache = m_MaterialCache, meshCache = m_MeshCache
            };

            var importer = new SyncObjectImporter();
            var gameObject = importer.Import(syncObject, configs);
            
            var comp = gameObject.AddComponent<SyncObjectBinding>();

            // Save it for eventual reuse
            m_Originals[key] = comp;

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

                Object.Destroy(syncObjectBinding.gameObject);
            }
        }

        public void OnPipelineInitialized()
        {
            // TODO
        }

        public void OnPipelineShutdown()
        {
            if (m_Root == null || m_Root.transform == null)
                return;
            
            foreach (Transform child in m_Root.transform)
            {
                Object.Destroy(child.gameObject);
            }
        }
    }
}