using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public static class ImportersUtils
    {
        public static float GetCandelasIntensity(float lightIntensity, SyncLightIntensityUnit intensityUnit, SyncLightType lightType, float apexConeAngle = 0.0f)
        {
            switch(intensityUnit)
            {
                case SyncLightIntensityUnit.Candela:
                    return lightIntensity;
                case SyncLightIntensityUnit.Lumen:
                    if(apexConeAngle.Equals(0.0f))
                    {
                        if (lightType == SyncLightType.Point) apexConeAngle = 360.0f;
                    }
                    return LumenToCandelas(lightIntensity, apexConeAngle);
                // TODO other conversions
                case SyncLightIntensityUnit.Lux:
                case SyncLightIntensityUnit.Watt:
                case SyncLightIntensityUnit.CandelaPerSquareMeter:
                case SyncLightIntensityUnit.Unknown:
                default:
                    break;
            }
            return lightIntensity;
        }

        static float LumenToCandelas(float lumenValue, float apexConeDegreeAngle = 360.0f)
        {
            // candelas = lumen / ( 2π(1 - cos(degrees/2)) )
            return lumenValue / ( (2.0f * (float)Math.PI) * (1.0f - (float)Math.Cos(ToRadian(apexConeDegreeAngle)/2.0f)) );
        }

        static float ToRadian(float degreeAngle)
        {
           return (float)Math.PI * degreeAngle / 180.0f;
        }

        public static Color ColorFromTemperature(float temperature)
        {
            return temperature > 0.0f
                ? Mathf.CorrelatedColorTemperatureToRGB(temperature).gamma
                : Color.white;
        }

        public static Color GetUnityColor(SyncColor color, bool keepAlpha = true)
        {
            return new Color(color.R, color.G, color.B, keepAlpha ? color.A : 1.0f);
        }
        
        public static void SetTransform(Transform transform, SyncTransform uTransform)
        {
            transform.localPosition = uTransform.Position;
            transform.localRotation = uTransform.Rotation;
            transform.localScale = uTransform.Scale;
        }
    }
}
