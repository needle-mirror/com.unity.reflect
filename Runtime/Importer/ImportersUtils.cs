using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public static class ImportersUtils
    {
        public static float GetCandelasIntensity(float lightIntensity, SyncLight.Types.IntensityUnit intensityUnit, SyncLight.Types.Type lightType, float apexConeAngle = 0.0f)
        {
            switch(intensityUnit)
            {
                case SyncLight.Types.IntensityUnit.Candela:
                    return lightIntensity;
                case SyncLight.Types.IntensityUnit.Lumen:
                    if(apexConeAngle.Equals(0.0f))
                    {
                        if (lightType == SyncLight.Types.Type.PointType) apexConeAngle = 360.0f;
                    }
                    return LumenToCandelas(lightIntensity, apexConeAngle);
                // TODO other conversions
                case SyncLight.Types.IntensityUnit.Lux:
                case SyncLight.Types.IntensityUnit.Watt:
                case SyncLight.Types.IntensityUnit.CandelaPerSquareMeter:
                case SyncLight.Types.IntensityUnit.Unknown:
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
            //  caching the members of the sync transform locally doubles the speed
            //  by reducing the numbed of times we call the protobuf accessors
            SyncFloat3 pos = uTransform.Position;
            transform.localPosition = new Vector3(pos.X, pos.Y, pos.Z);

            SyncQuaternion rot = uTransform.Rotation;
            transform.localRotation = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);

            SyncFloat3 scale = uTransform.Scale;
            transform.localScale = new Vector3(scale.X, scale.Y, scale.Z);
        }
    }
}
