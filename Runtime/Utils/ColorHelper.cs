using Unity.Collections;

namespace UnityEngine.Reflect
{
    public static class ColorHelper
    {
        const float k_ByteMaxValueF = byte.MaxValue;

        public static void ReadColor(NativeArray<byte> bytes, TextureFormat format, int colorIndex, ref Color color)
        {
            var index = colorIndex * (format == TextureFormat.RGB24 ? 3 : 4);
            switch (format)
            {
                case TextureFormat.ARGB32:
                    color.a = bytes[index] / k_ByteMaxValueF;
                    color.r = bytes[index + 1] / k_ByteMaxValueF;
                    color.g = bytes[index + 2] / k_ByteMaxValueF;
                    color.b = bytes[index + 3] / k_ByteMaxValueF;
                    break;
                case TextureFormat.RGB24:
                    color.r = bytes[index] / k_ByteMaxValueF;
                    color.g = bytes[index + 1] / k_ByteMaxValueF;
                    color.b = bytes[index + 2] / k_ByteMaxValueF;
                    color.a = 1f;
                    break;
                case TextureFormat.RGBA32:
                    color.r = bytes[index] / k_ByteMaxValueF;
                    color.g = bytes[index + 1] / k_ByteMaxValueF;
                    color.b = bytes[index + 2] / k_ByteMaxValueF;
                    color.a = bytes[index + 3] / k_ByteMaxValueF;
                    break;
                default:
                    Debug.LogError($"SyncTextureImporter.GetColor: TextureFormat {format.ToString()} is not supported!");
                    break;
            }
        }

        public static void WriteColor(NativeArray<byte> bytes, TextureFormat format, int colorIndex, Color color)
        {
            var index = colorIndex * (format == TextureFormat.RGB24 ? 3 : 4);
            switch (format)
            {
                case TextureFormat.ARGB32:
                    bytes[index] = (byte)(color.a * k_ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.r * k_ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.g * k_ByteMaxValueF);
                    bytes[index + 3] = (byte)(color.b * k_ByteMaxValueF);
                    break;
                case TextureFormat.RGB24:
                    bytes[index] = (byte)(color.r * k_ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.g * k_ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.b * k_ByteMaxValueF);
                    break;
                case TextureFormat.RGBA32:
                    bytes[index] = (byte)(color.r * k_ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.g * k_ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.b * k_ByteMaxValueF);
                    bytes[index + 3] = (byte)(color.a * k_ByteMaxValueF);
                    break;
                default:
                    Debug.LogError($"SyncTextureImporter.GetColor: TextureFormat {format.ToString()} is not supported!");
                    return;
            }
        }
    }
}
