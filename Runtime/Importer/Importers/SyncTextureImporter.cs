using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{    
    public class SyncTextureImporter : RuntimeImporter<SyncTexture, Texture2D>
    {
		static readonly float k_NormalMapIntensity = 2.0f;

        public override Texture2D CreateNew(SyncTexture syncTexture)
        {
            var texture = new Texture2D(1, 1)
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

        protected override void ImportInternal(SyncTexture syncTexture, Texture2D texture, object settings)
        {           
            var data = syncTexture.Source.ToByteArray();

            texture.LoadImage(data);

            if (syncTexture.ConvertToNormalMap)
            {
                ConvertToNormalMap(texture, k_NormalMapIntensity, false);
            }
        }
        
        static void ConvertToNormalMap(Texture2D image, float intensity, bool inverse)
        {
            var pixels = image.GetPixels();
            
            for (int x = 0; x < image.width; ++x)
            {
                for (int y = 0; y < image.height; ++y)
                {
                    var left = GetValue(pixels, x - 1, y, image.width, image.height);
                    var right = GetValue(pixels, x + 1, y, image.width, image.height);
                    var up = GetValue(pixels, x, y + 1, image.width, image.height);
                    var down = GetValue(pixels, x, y - 1, image.width, image.height);
                    
                    var dx = (left - right + 1) * 0.5f;
                    var dy = (up - down + 1) * 0.5f;
                    
                    image.SetPixel(x, y, new Color(
                        Mathf.Clamp01(ApplySCurve(dx, intensity)),
                        Mathf.Clamp01(ApplySCurve(inverse ? 1.0f - dy : dy, intensity)),
                        1.0f, 1.0f));
                }
            }
            
            image.Apply();
        }
        
        static float GetValue(Color[] pixels, int x, int y, int width, int height)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            return pixels[x + y * width].grayscale;
        }
        
        static float ApplySCurve(float value, float intensity)
        {
            return 1.0f / (1.0f + Mathf.Exp(-intensity * (value - 0.5f)));
        }
    }
}
