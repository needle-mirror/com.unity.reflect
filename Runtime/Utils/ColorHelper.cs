using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Reflect
{
    public static class ColorHelper
    {
        private const float ByteMaxValueF = byte.MaxValue;
        private static readonly int MaterialTintPropertyId = Shader.PropertyToID("_Tint");

        private static readonly List<Material> SharedMaterials = new List<Material>();
        private static readonly List<Color> MaterialColors = new List<Color>();
        private static readonly object CalculateAverageColorLock = new object();
        
        private static Texture2D _texture;
        
        public static void ReadColor(NativeArray<byte> bytes, TextureFormat format, int colorIndex, ref Color color)
        {
            var index = colorIndex * (format == TextureFormat.RGB24 ? 3 : 4);
            switch (format)
            {
                case TextureFormat.ARGB32:
                    color.a = bytes[index] / ByteMaxValueF;
                    color.r = bytes[index + 1] / ByteMaxValueF;
                    color.g = bytes[index + 2] / ByteMaxValueF;
                    color.b = bytes[index + 3] / ByteMaxValueF;
                    break;
                case TextureFormat.RGB24:
                    color.r = bytes[index] / ByteMaxValueF;
                    color.g = bytes[index + 1] / ByteMaxValueF;
                    color.b = bytes[index + 2] / ByteMaxValueF;
                    color.a = 1f;
                    break;
                case TextureFormat.RGBA32:
                    color.r = bytes[index] / ByteMaxValueF;
                    color.g = bytes[index + 1] / ByteMaxValueF;
                    color.b = bytes[index + 2] / ByteMaxValueF;
                    color.a = bytes[index + 3] / ByteMaxValueF;
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
                    bytes[index] = (byte)(color.a * ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.r * ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.g * ByteMaxValueF);
                    bytes[index + 3] = (byte)(color.b * ByteMaxValueF);
                    break;
                case TextureFormat.RGB24:
                    bytes[index] = (byte)(color.r * ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.g * ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.b * ByteMaxValueF);
                    break;
                case TextureFormat.RGBA32:
                    bytes[index] = (byte)(color.r * ByteMaxValueF);
                    bytes[index + 1] = (byte)(color.g * ByteMaxValueF);
                    bytes[index + 2] = (byte)(color.b * ByteMaxValueF);
                    bytes[index + 3] = (byte)(color.a * ByteMaxValueF);
                    break;
                default:
                    Debug.LogError($"SyncTextureImporter.GetColor: TextureFormat {format.ToString()} is not supported!");
                    return;
            }
        }

        public static Color CalculateAverageColor(Renderer renderer, int precision = 1, int materialWithTextureBias = 1)
        {
            lock (CalculateAverageColorLock)
            {
                SharedMaterials.Clear();
                MaterialColors.Clear();
                
                renderer.GetSharedMaterials(SharedMaterials);
                foreach (var material in SharedMaterials)
                {
                    if (material == null)
                        continue;

                    var materialTint = material.HasProperty(MaterialTintPropertyId) ? material.GetColor(MaterialTintPropertyId) : Color.white;
                    
                    
                    _texture = material.mainTexture != null ? material.mainTexture as Texture2D : null;
                    if (_texture == null)
                    {
                        MaterialColors.Add(materialTint);
                        continue;
                    }
                    
                    var totalColor = Color.black;
                    var pixelColor = default(Color);
                    
                    var textureColors = _texture.GetRawTextureData<byte>();
                    var format = _texture.format;
                    // TODO: use highest mipmap level (currently bugged)
                    // var totalLength = textureColors.Length / (format == TextureFormat.RGB24 ? 3 : 4);
                    // var mipmapCount = texture2D.mipmapCount;
                    // var startIndex = mipmapCount > 1 
                    //     ? totalLength - texture2D.width * texture2D.height / ((1 << mipmapCount) * (1 << mipmapCount)) 
                    //     : 0;
                    var totalLength = _texture.width * _texture.height;
                    const int startIndex = 0;
                    var count = 0;
                    
                    for (var i = startIndex; i < totalLength; i += precision)
                    {
                        ReadColor(textureColors, format, i, ref pixelColor);
                        if (pixelColor.a <= 0f)
                            continue;
                        
                        totalColor.r += pixelColor.r;
                        totalColor.g += pixelColor.g;
                        totalColor.b += pixelColor.b;
                        ++count;
                    }

                    if (count > 0)
                    {
                        totalColor.r *= materialTint.r / count;
                        totalColor.g *= materialTint.g / count;
                        totalColor.b *= materialTint.b / count;
                        for (var i = 0; i < materialWithTextureBias; ++i)
                            MaterialColors.Add(totalColor);
                    }
                    else
                        MaterialColors.Add(materialTint);
                    
                }

                var averageColor = Color.black;
                foreach (var color in MaterialColors)
                {
                    averageColor.r += color.r;
                    averageColor.g += color.g;
                    averageColor.b += color.b;
                }

                averageColor.r /= MaterialColors.Count;
                averageColor.g /= MaterialColors.Count;
                averageColor.b /= MaterialColors.Count;
                averageColor.a = 1f;
                
                return averageColor;
            }
        }
    }
}
