using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actors
{
    [Actor("7f3a5a99-39ad-439f-b831-ea3191c42faf", true)]
    public class GameObjectBuilderActor
    {
#pragma warning disable 649
        Settings m_Settings;
#pragma warning restore 649

        SyncObjectImporter m_Importer = new SyncObjectImporter();

        Dictionary<string, Transform> m_SourceRoots;
        
        public void Inject()
        {
            m_SourceRoots = m_Settings.GenerateSourceRoots ? new Dictionary<string, Transform>() : null;
        }

        [RpcInput]
        void OnBuildGameObject(RpcContext<BuildGameObject> ctx)
        {
            var materialCache = new MaterialCache { Materials = ctx.Data.Materials };
            var meshCache = new MeshCache { Meshes = ctx.Data.Meshes };
            var configs = new SyncObjectImportConfig
            {
                settings = new SyncObjectImportSettings
                {
                    defaultMaterial = ReflectMaterialManager.defaultMaterial,
                    importLights = true
                },
                materialCache = materialCache,
                meshCache = meshCache,
                lightImport = m_Settings.LightImporter
            };

            var gameObject = m_Importer.Import(ctx.Data.InstanceData.SourceId, ctx.Data.Object, configs, ctx.Data.DefaultMaterial, 
                m_Settings.EnableGameObjects);
            
            gameObject.name = ctx.Data.Instance.Name;
            gameObject.transform.SetParent(GetInstanceRoot(ctx.Data.InstanceData.SourceId));
            ImportersUtils.SetTransform(gameObject.transform, ctx.Data.Instance.Transform);
            ImportersUtils.SetMetadata(gameObject, ctx.Data.Instance.Metadata);
            
            AddSyncObjectBinding(gameObject, ctx.Data.InstanceData);

            ctx.SendSuccess(gameObject);
        }

        static void AddSyncObjectBinding(GameObject gameObject, EntryData entryData)
        {
            var syncObjectBinding = gameObject.AddComponent<SyncObjectBinding>();
            syncObjectBinding.streamKey = new StreamKey(entryData.SourceId, entryData.IdInSource);
            syncObjectBinding.stableId = entryData.StableId;
#if UNITY_EDITOR
            var box = entryData.Spatial.Box;
            syncObjectBinding.bounds = new Bounds(box.Min.ToUnity(), Vector3.zero);
            syncObjectBinding.bounds.Encapsulate(box.Max.ToUnity());
#endif
        }

        Transform GetInstanceRoot(string source)
        {
            if (m_SourceRoots == null)
                return m_Settings.Root;

            if (m_SourceRoots.TryGetValue(source, out var root)) 
                return root;
            
            root = new GameObject(source).transform;
            root.parent = m_Settings.Root;
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;

            m_SourceRoots[source] = root;

            return root;
        }

        class SyncObjectImporter
        {
            GameObject CreateNew(SyncObject syncElement, bool enabled)
            {
                var go = new GameObject(syncElement.Name);
                go.SetActive(enabled);
                return go;
            }

            public GameObject Import(string sourceId, SyncObject model, SyncObjectImportConfig settings, Material defaultMaterial, bool enabled)
            {
                var gameObject = CreateNew(model, enabled);
                Import(sourceId, model, gameObject, settings, defaultMaterial);

                return gameObject;
            }

            public void Clear(GameObject gameObject)
            {
                foreach (Transform child in gameObject.transform)
                {
                    if (child != null && child.gameObject != null)
                        UnityEngine.Object.Destroy(child.gameObject); // Avoid DestroyImmediate or iteration will not work properly
                }

                var components = gameObject.GetComponents<Component>();

                foreach (var component in components)
                {
                    if (!(component is Transform) && !(component is SyncObjectBinding) && !(component is Renderer))
                        UnityEngine.Object.DestroyImmediate(component);
                }
            }

            static void Import(string sourceId, SyncObject syncObject, GameObject gameObject, SyncObjectImportConfig config, Material defaultMaterial)
            {
                if (syncObject.MeshId != SyncId.None)
                {
                    var meshKey = StreamKey.FromSyncId<SyncMesh>(sourceId, syncObject.MeshId);
                    var mesh = config.meshCache.GetMesh(meshKey);

                    if (mesh != null)
                    {
                        var meshFilter = gameObject.AddComponent<MeshFilter>();
                        meshFilter.sharedMesh = mesh;

                        var renderer = gameObject.GetComponent<MeshRenderer>();
                        if (renderer == null)
                        {
                            renderer = gameObject.AddComponent<MeshRenderer>();
                        }

                        var materials = new Material[meshFilter.sharedMesh.subMeshCount];
                        for (int i = 0; i < materials.Length; ++i)
                        {
                            Material material = null;
                            if (i < syncObject.MaterialIds.Count)
                            {
                                var materialId = syncObject.MaterialIds[i];

                                if (materialId != SyncId.None)
                                {
                                    var materialKey = StreamKey.FromSyncId<SyncMaterial>(sourceId, materialId);
                                    material = config.materialCache.GetMaterial(materialKey);
                                }
                            }

                            materials[i] = material ? material : defaultMaterial;
                        }

                        renderer.sharedMaterials = materials;
                    }
                }

                if (config.settings.importLights && syncObject.Light != null)
                {
                    config.lightImport?.Import(syncObject.Light, gameObject);
                }
                
                if (syncObject.Rpc != null)
                {
                    // TODO
                }
                
                if (syncObject.Camera != null)
                {
                    ImportCamera(syncObject.Camera, gameObject);
                }

                if (syncObject.Children != null)
                {
                    foreach (var child in syncObject.Children)
                    {
                        if (IsEmpty(child, config))
                            continue;
                        
                        var childObject = new GameObject(child.Name);
                                    
                        childObject.transform.parent = gameObject.transform;
                        ImportersUtils.SetTransform(childObject.transform, child.Transform);
                        
                        Import(sourceId, child, childObject, config, defaultMaterial);
                    }
                }
            }

            static bool IsEmpty(SyncObject syncObject, SyncObjectImportConfig config)
            {
                if (syncObject.Children != null && syncObject.Children.Count > 0)
                    return false;

                if (syncObject.MeshId != SyncId.None)
                    return false;

                if (config.settings.importLights && syncObject.Light != null)
                    return false;
                
                if (syncObject.Rpc != null)
                    return false;
                
                if (syncObject.Camera != null)
                    return false;

                return true;
            }

            static void ImportCamera(SyncCamera syncCamera, GameObject parent)
            {
                parent.name = "[POI] " + syncCamera.Name; 
                
                var poi = parent.AddComponent<POI>();

                poi.label = syncCamera.Name;
        
                poi.orthographic = syncCamera.Orthographic;
                poi.aspect = syncCamera.Aspect;
                poi.size = syncCamera.Size;
                poi.fov = syncCamera.Fov;
                
                poi.near = syncCamera.Near;
                poi.far = syncCamera.Far;
                poi.top = syncCamera.Top;
                poi.left = syncCamera.Left;
                poi.bottom = syncCamera.Bottom;
                poi.right = syncCamera.Right;
            }
        }

        class MaterialCache : IMaterialCache
        {
            public Dictionary<StreamKey, Material> Materials;
            public Material GetMaterial(StreamKey id)
            {
                return Materials[id];
            }
        }

        class MeshCache : IMeshCache
        {
            public Dictionary<StreamKey, Mesh> Meshes;
            public Mesh GetMesh(StreamKey id)
            {
                return Meshes[id];
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            [SerializeField]
            [Transient(nameof(Root))]
            ExposedReference<Transform> m_Root;

            [HideInInspector]
            public Transform Root;

            public bool EnableGameObjects = true;

            [Tooltip("If true, will create a parent GameObject for each Reflect model source.")]
            public bool GenerateSourceRoots = true;
            
            [ImplPicker]
            [SerializeReference]
            public ISyncLightImporter LightImporter;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
