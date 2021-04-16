using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncObjectImportConfig
    {
        public IMeshCache meshCache;
        public IMaterialCache materialCache;
        public SyncObjectImportSettings settings;
        public ISyncLightImporter lightImport;
    }

    public class ObjectDependencies
    {
        public List<(StreamKey key, Mesh Mesh)> meshes;

        public ObjectDependencies(List<(StreamKey, Mesh)> meshes)
        {
            this.meshes = meshes;
        }
    }

    [Serializable]
    public class SyncObjectImportSettings
    {
        public bool importLights;
        public Material defaultMaterial;
    }
    
    public class SyncObjectImporter : RuntimeImporter<SyncObject, GameObject>
    {
        public override GameObject CreateNew(SyncObject syncElement, object settings)
        {
            return new GameObject(syncElement.Name);
        }

        public (ObjectDependencies Dependencies, GameObject GameObject) ImportAndGetDependencies(string sourceId, SyncObject model, object settings)
        {
            var asset = CreateNew(model, settings);

            var dependencies = Import(sourceId, model, asset, (SyncObjectImportConfig)settings);

            return (dependencies, asset);
        }

        protected override void Clear(GameObject gameObject)
        {
            foreach (Transform child in gameObject.transform)
            {
                if (child != null && child.gameObject != null)
                    Object.Destroy(child.gameObject); // Avoid DestroyImmediate or iteration will not work properly
            }

            var components = gameObject.GetComponents<Component>();

            foreach (var component in components)
            {
                if (!(component is Transform) && !(component is SyncObjectBinding) && !(component is Renderer))
                    Object.DestroyImmediate(component);
            }
        }

        protected override void ImportInternal(SyncedData<SyncObject> syncObject, GameObject gameObject, object settings)
        {
            Import(syncObject.key.source, syncObject.data, gameObject, (SyncObjectImportConfig)settings);
        }

        static ObjectDependencies Import(string sourceId, SyncObject syncObject, GameObject gameObject, SyncObjectImportConfig config)
        {
            var meshes = new List<(StreamKey, Mesh)>();

            if (syncObject.MeshId != SyncId.None)
            {
                var meshKey = StreamKey.FromSyncId<SyncMesh>(sourceId, syncObject.MeshId);
                var mesh = config.meshCache.GetMesh(meshKey);

                if (mesh != null)
                {
                    meshes.Add((meshKey, mesh));

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
                (config.lightImport ?? new SyncLightImporter()).Import(syncObject.Light, gameObject); 
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

            return new ObjectDependencies(meshes);
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
}
