using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncElementSettings
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
                if (!(component is Transform) && !(component is SyncObjectBinding))
                    Object.DestroyImmediate(component);
            }
        }

        protected override void ImportInternal(SyncObject syncElement, GameObject gameObject, object settings)
        {
            Import(syncElement, gameObject, (SyncElementSettings)settings);
        }

        static void Import(SyncObject syncElement, GameObject gameObject, SyncElementSettings settings)
        {            
            if (syncElement.Metadata != null && syncElement.Metadata.Count > 0)
            {
                var model = gameObject.AddComponent<Metadata>();
                foreach (var parameter in syncElement.Metadata)
                {
                    var uParameter = parameter.Value;
                    model.parameters.dictionary.Add(parameter.Key, new Metadata.Parameter
                    {
                        group = uParameter.ParameterGroup, value = uParameter.Value, visible = uParameter.Visible
                    });
                }
            }

            if (!string.IsNullOrEmpty(syncElement.Mesh))
            {
                var mesh = settings.meshCache.GetMesh(syncElement.Mesh);

                if (mesh != null)
                {
                    var meshFilter = gameObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = settings.meshCache.GetMesh(syncElement.Mesh);
                    
                    var renderer = gameObject.AddComponent<MeshRenderer>();

                    var materials = new Material[meshFilter.sharedMesh.subMeshCount];
                    for (int i = 0; i < materials.Length; ++i)
                    {
                        var materialName = i < syncElement.Materials.Count ? syncElement.Materials[i] : null;
                        materials[i] = settings.materialCache.GetMaterial(materialName) ?? settings.defaultMaterial;
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            if (settings.importLights && syncElement.Lights != null)
            {
                foreach (var syncLight in syncElement.Lights)
                {
                    ImportLight(syncLight, gameObject);
                }
            }
            
            if (syncElement.Rpcs != null)
            {
                foreach (var syncRPC in syncElement.Rpcs)
                {
                    ImportRPC(syncRPC, gameObject);
                }
            }
            
            if (syncElement.Cameras != null)
            {
                foreach (var syncCamera in syncElement.Cameras)
                {
                    ImportCamera(syncCamera, gameObject);
                }
            }

            if (syncElement.Children != null)
            {
                foreach (var child in syncElement.Children)
                {
                    var childObject = new GameObject(child.Name);
                                
                    childObject.transform.parent = gameObject.transform;
                    ImportersUtils.SetTransform(childObject.transform, child.Transform);
                    
                    Import(child, childObject, settings);
                }
            }
        }

        static void ImportLight(SyncLight syncLight, GameObject parent)
        {
            var lightGameObject = new GameObject(syncLight.Name);
            lightGameObject.transform.parent = parent.transform;

            var light = lightGameObject.AddComponent<Light>();
            
            // TODO adjust based on the intensityUnit
            // This fit the Candela unit
            var rangeMultiplier = 0.5f;
            var intensityMultiplier = 0.0001f;

            light.name = syncLight.Name;
            light.intensity = ImportersUtils.GetCandelasIntensity(syncLight.Intensity, syncLight.IntensityUnit, syncLight.Type, syncLight.SpotAngle);
            light.intensity = light.intensity * intensityMultiplier;

            // TODO Investigate why Light.UseColorTemperature is not exposed to C# and let the light do this calculation
            var cct = ImportersUtils.ColorFromTemperature(syncLight.Temperature);


            var filter = new Color(syncLight.Color.R, syncLight.Color.G, syncLight.Color.B);
            
            light.color =  cct * filter;
            light.colorTemperature = syncLight.Temperature;
            
            switch (syncLight.Type)
            {
                case SyncLight.Types.Type.Spot:
                    light.spotAngle = syncLight.SpotAngle;
                    light.shadows = LightShadows.Hard;
                    light.range = (syncLight.Range * rangeMultiplier);
                    light.type = LightType.Spot;
                    break;
                case SyncLight.Types.Type.PointType:
                    light.type = LightType.Point;
                    light.range = syncLight.Range == 0.0f ? 1.0f:(syncLight.Range  * rangeMultiplier);
                    break;
                case SyncLight.Types.Type.Directional:
                    light.type = LightType.Directional;
                    break;
            }
            
            ImportersUtils.SetTransform(lightGameObject.transform, syncLight.Transform);
        }

        static void ImportRPC(SyncRPC syncRPC, GameObject parent)
        {
            var rpcGameObject = new GameObject(syncRPC.Name);
            rpcGameObject.transform.parent = parent.transform;

            ImportersUtils.SetTransform(rpcGameObject.transform, syncRPC.Transform);
        }

        static void ImportCamera(SyncCamera syncCamera, GameObject parent)
        {
            parent.name = "[POI] " + syncCamera.Name; 
            ImportersUtils.SetTransform(parent.transform, syncCamera.Transform);
            
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
