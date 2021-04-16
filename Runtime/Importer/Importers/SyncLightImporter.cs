using System;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public interface ISyncLightImporter
    {
        void Import(SyncLight syncLight, GameObject parent);
    }
    
    public class SyncLightImporter: ISyncLightImporter
    {
        const float k_DefaultIntensity = 1.0f;
        const double k_IntensityExponent = 0.25;
        const double k_IntensityMultiplier = 0.2;
        const float k_IntensityAtRange = 0.02f;
        
        public void Import(SyncLight syncLight, GameObject parent)
        {
            // Convert syncLight intensity to unity light intensity
            var intensity = k_DefaultIntensity;
            if (syncLight.Intensity > 0)
            {
                intensity = ImportersUtils.GetCandelasIntensity(syncLight.Intensity, syncLight.IntensityUnit, syncLight.Type, syncLight.SpotAngle);
                intensity = (float)(k_IntensityMultiplier * Math.Pow(intensity, k_IntensityExponent));
            }

            // Compute the range if not provided
            var range = syncLight.Range;
            if (range <= 0)
            {
                range = (float)Math.Sqrt(intensity / k_IntensityAtRange);
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