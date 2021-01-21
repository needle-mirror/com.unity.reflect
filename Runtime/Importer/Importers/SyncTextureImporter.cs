using System;
using Unity.Collections;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{    
    public class SyncTextureImporter : RuntimeImporter<SyncTexture, Texture2D>
    {
		static readonly float k_NormalMapIntensity = 2.0f;

        private static float[] _grayscaleBuffer;

        public override Texture2D CreateNew(SyncTexture syncTexture, object settings)
        {
            var linear = syncTexture.ConvertToNormalMap || QualitySettings.activeColorSpace == ColorSpace.Linear;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, Texture.GenerateAllMips, linear)
            {
                name = syncTexture.Name,
                anisoLevel = 4,
                wrapMode = TextureWrapMode.Repeat
            };

            return texture;
        }

        protected override void Clear(Texture2D texture)
        {
            // Nothing.
        }

        protected override void ImportInternal(SyncedData<SyncTexture> syncTexture, Texture2D texture, object settings)
        {
            var data = syncTexture.data.Source;

            texture.LoadImage(data);

            if (syncTexture.data.ConvertToNormalMap)
            {
                ConvertToNormalMap(texture, k_NormalMapIntensity, false);
            }
        }
        
        static void ConvertToNormalMap(Texture2D image, float intensity, bool inverse)
        {
            var format = image.format;
            var bytes = image.GetRawTextureData<byte>();
            var width = image.width;
            var height = image.height;

            CreateGrayscaleBuffer(bytes, format, width, height);

            var color = Color.white;
            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var left = GetValue(x - 1, y, width, height);
                    var right = GetValue(x + 1, y, width, height);
                    var up = GetValue(x, y + 1, width, height);
                    var down = GetValue(x, y - 1, width, height);
                    
                    var dx = (left - right + 1) * 0.5f;
                    var dy = (up - down + 1) * 0.5f;

                    var index = x + y * width;
                    color.r = Mathf.Clamp01(ApplySCurve(dx, intensity));
                    color.g = Mathf.Clamp01(ApplySCurve(inverse ? 1.0f - dy : dy, intensity));
                    color.b = 1f;
                    color.a = 1f;
                    ColorHelper.WriteColor(bytes, format, index, color);
                }
            }
            
            image.LoadRawTextureData(bytes);
            image.Apply();
        }

        static void CreateGrayscaleBuffer(NativeArray<byte> bytes, TextureFormat format, int width, int height)
        {
            if (_grayscaleBuffer == null || _grayscaleBuffer.Length < width * height)
                _grayscaleBuffer = new float[width * height];

            var color = Color.white;
            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var index = x + y * width;
                    ColorHelper.ReadColor(bytes, format, index, ref color);
                    _grayscaleBuffer[index] = color.grayscale;
                }
            }

        }
        
        static float GetValue(int x, int y, int width, int height)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            return _grayscaleBuffer[x + y * width];
        }
        
        static float ApplySCurve(float value, float intensity)
        {
            return 1.0f / (1.0f + Mathf.Exp(-intensity * (value - 0.5f)));
        }
    }
}
