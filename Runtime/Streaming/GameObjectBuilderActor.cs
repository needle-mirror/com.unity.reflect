using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class GameObjectBuilderActor
    {
#pragma warning disable 649
        Settings m_Settings;
#pragma warning restore 649

        SyncObjectImporter m_Importer = new SyncObjectImporter();

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
                meshCache = meshCache
            };

            var gameObject = m_Importer.Import(ctx.Data.InstanceData.SourceId, ctx.Data.Object, configs);
            
            gameObject.name = ctx.Data.Instance.Name;
            gameObject.transform.SetParent(m_Settings.Root);
            ImportersUtils.SetTransform(gameObject.transform, ctx.Data.Instance.Transform);
            ImportersUtils.SetMetadata(gameObject, ctx.Data.Instance.Metadata);

            ctx.SendSuccess(gameObject);
        }

        class SyncObjectImporter
        {
            GameObject CreateNew(SyncObject syncElement)
            {
                return new GameObject(syncElement.Name);
            }

            public GameObject Import(string sourceId, SyncObject model, object settings)
            {
                var gameObject = CreateNew(model);
                Import(sourceId, model, gameObject, (SyncObjectImportConfig)settings);

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

            static void Import(string sourceId, SyncObject syncObject, GameObject gameObject, SyncObjectImportConfig config)
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

                            materials[i] = material ? material : config.settings.defaultMaterial;
                        }

                        renderer.sharedMaterials = materials;
                    }
                }

                if (config.settings.importLights && syncObject.Light != null)
                {
                    ImportLight(syncObject.Light, gameObject);
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
                        
                        Import(sourceId, child, childObject, config);
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

            static void ImportLight(SyncLight syncLight, GameObject parent)
            {
                // Conversion parameters
                const float defaultIntensity = 1.0f;
                const double intensityExponent = 0.25;
                const double intensityMultiplier = 0.2;
                const float intensityAtRange = 0.02f;

                // Convert syncLight intensity to unity light intensity
                var intensity = defaultIntensity;
                if (syncLight.Intensity > 0)
                {
                    intensity = ImportersUtils.GetCandelasIntensity(syncLight.Intensity, syncLight.IntensityUnit, syncLight.Type, syncLight.SpotAngle);
                    intensity = (float)(intensityMultiplier * Math.Pow(intensity, intensityExponent));
                }

                // Compute the range if not provided
                var range = syncLight.Range;
                if (range <= 0)
                {
                    range = (float)Math.Sqrt(intensity / intensityAtRange);
                }

                // TODO Investigate why Light.UseColorTemperature is not exposed to C# and let the light do this calculation
                var cct = ImportersUtils.ColorFromTemperature(syncLight.Temperature);

                var filter = new Color(syncLight.Color.R, syncLight.Color.G, syncLight.Color.B);
                
                var light = parent.AddComponent<Light>();
                
                light.color =  cct * filter;
                light.colorTemperature = syncLight.Temperature;
                
                switch (syncLight.Type)
                {
                    case SyncLightType.Spot:
                    {
                        light.spotAngle = syncLight.SpotAngle;
                        light.shadows = LightShadows.Hard;
                        light.range = range;
                        light.type = LightType.Spot;
                        light.intensity = intensity;
                    }
                    break;

                    case SyncLightType.Point:
                    {
                        light.type = LightType.Point;
                        light.range = range;
                        light.intensity = intensity;
                    }
                    break;

                    case SyncLightType.Directional:
                    {
                        light.type = LightType.Directional;
                        light.intensity = intensity;
                    }
                    break;
                }
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

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
