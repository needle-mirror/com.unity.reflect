using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface ISyncLightImporter
    {
        void Import(SyncLight syncLight, GameObject parent);
    }
    
    [Serializable]
    public class SyncLightImporter: ISyncLightImporter
    {
#pragma warning disable CS0649
        [SerializeField] float m_DefaultIntensity = 1.0f;
        [SerializeField] float m_IntensityExponent = 0.25f;
        [SerializeField] float m_IntensityMultiplier = 0.2f;
        [SerializeField] float m_IntensityAtRange = 0.02f;
#pragma warning restore CS0649
        
        public void Import(SyncLight syncLight, GameObject parent)
        {
            // Convert syncLight intensity to unity light intensity
            var intensity = m_DefaultIntensity;
            if (syncLight.Intensity > 0)
            {
                intensity = ImportersUtils.GetCandelasIntensity(syncLight.Intensity, syncLight.IntensityUnit, syncLight.Type, syncLight.SpotAngle);
                intensity = (float)(m_IntensityMultiplier * Math.Pow(intensity, m_IntensityExponent));
            }

            // Compute the range if not provided
            var range = syncLight.Range;
            if (range <= 0)
            {
                range = (float)Math.Sqrt(intensity / m_IntensityAtRange);
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
    }
}