using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncObjectImportSettings
    {
        public bool importLights;
        public Material defaultMaterial;
        public IMeshCache meshCache;
        public IMaterialCache materialCache;
    }
    
    public class SyncObjectImporter : RuntimeImporter<SyncObject, GameObject>
    {
        public override GameObject CreateNew(SyncObject syncElement)
        {
            return new GameObject(syncElement.Name);
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

        protected override void ImportInternal(SyncObject syncObject, GameObject gameObject, object settings)
        {
            Import(syncObject, gameObject, (SyncObjectImportSettings)settings);
        }

        static void Import(SyncObject syncObject, GameObject gameObject, SyncObjectImportSettings settings)
        {            
            if (syncObject.Metadata != null && syncObject.Metadata.Count() > 0)
            {
                var model = gameObject.AddComponent<Metadata>();
                foreach (var parameter in syncObject.Metadata.Parameters)
                {
                    var uParameter = parameter.Value;
                    model.parameters.dictionary.Add(parameter.Key, new Metadata.Parameter
                    {
                        group = uParameter.ParameterGroup, value = uParameter.Value, visible = uParameter.Visible
                    });
                }
            }

            if (syncObject.MeshId != SyncId.None)
            {
                var mesh = settings.meshCache.GetMesh(syncObject.MeshId.Value);

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
                                material = settings.materialCache.GetMaterial(materialId.Value);
                            }
                        }
                        
                        materials[i] = material ? material : settings.defaultMaterial;
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            if (settings.importLights && syncObject.Light != null)
            {
                ImportLight(syncObject.Light, gameObject);
            }
            
            if (syncObject.Rpc != null)
            {
                ImportRPC(syncObject.Rpc, gameObject);
            }
            
            if (syncObject.Camera != null)
            {
                ImportCamera(syncObject.Camera, gameObject);
            }

            if (syncObject.Children != null)
            {
                foreach (var child in syncObject.Children)
                {
                    if (IsEmpty(child, settings))
                        continue;
                    
                    var childObject = new GameObject(child.Name);
                                
                    childObject.transform.parent = gameObject.transform;
                    ImportersUtils.SetTransform(childObject.transform, child.Transform);
                    
                    Import(child, childObject, settings);
                }
            }
        }

        static bool IsEmpty(SyncObject syncObject, SyncObjectImportSettings settings)
        {
            if (syncObject.Children != null && syncObject.Children.Count > 0)
                return false;

            if (syncObject.MeshId != SyncId.None)
                return false;

            if (settings.importLights && syncObject.Light != null)
                return false;
            
            if (syncObject.Rpc != null)
                return false;
            
            if (syncObject.Camera != null)
                return false;

            return true;
        }

        static void ImportLight(SyncLight syncLight, GameObject parent)
        {
            // TODO adjust based on the intensityUnit
            // This fit the Candelas unit
            const float rangeMultiplier = 0.5f;

            var intensity = ImportersUtils.GetCandelasIntensity(syncLight.Intensity, syncLight.IntensityUnit, syncLight.Type, syncLight.SpotAngle);

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
                    light.range = (syncLight.Range * rangeMultiplier);
                    light.type = LightType.Spot;
                    light.intensity = intensity * 0.1f;
                }
                break;

                case SyncLightType.Point:
                {
                    light.type = LightType.Point;
                    light.range = syncLight.Range.Equals(0.0f) ? 1.0f : syncLight.Range * rangeMultiplier;
                    light.intensity = intensity * 0.0001f;
                }
                break;

                case SyncLightType.Directional:
                {
                    light.type = LightType.Directional;
                    light.intensity = intensity * 0.0001f;
                }
                break;
            }
        }

        static void ImportRPC(SyncRPC syncRPC, GameObject parent)
        {
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
