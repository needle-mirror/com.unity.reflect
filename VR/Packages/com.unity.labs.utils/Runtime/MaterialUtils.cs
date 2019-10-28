using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Runtime Material utilities
    /// </summary>
    public static class MaterialUtils
    {
        /// <summary>
        /// Get a material clone; IMPORTANT: Make sure to call UnityObjectUtils.Destroy() on this material when done!
        /// </summary>
        /// <param name="renderer">Renderer that will have its material clone and replaced</param>
        /// <returns>Cloned material</returns>
        public static Material GetMaterialClone(Renderer renderer)
        {
            // The following is equivalent to renderer.material, but gets rid of the error messages in edit mode
            return renderer.material = UnityObject.Instantiate(renderer.sharedMaterial);
        }

        /// <summary>
        /// Get a material clone; IMPORTANT: Make sure to call UnityObjectUtils.Destroy() on this material when done!
        /// </summary>
        /// <param name="graphic">Graphic that will have its material cloned and replaced</param>
        /// <returns>Cloned material</returns>
        public static Material GetMaterialClone(Graphic graphic)
        {
            // The following is equivalent to graphic.material, but gets rid of the error messages in edit mode
            return graphic.material = UnityObject.Instantiate(graphic.material);
        }

        /// <summary>
        /// Clone all materials within a renderer; IMPORTANT: Make sure to call UnityObjectUtils.Destroy() on this material when done!
        /// </summary>
        /// <param name="renderer">Renderer that will have its materials cloned and replaced</param>
        /// <returns>Cloned materials</returns>
        public static Material[] CloneMaterials(Renderer renderer)
        {
            var sharedMaterials = renderer.sharedMaterials;
            for (var i = 0; i < sharedMaterials.Length; i++)
            {
                sharedMaterials[i] = UnityObject.Instantiate(sharedMaterials[i]);
            }

            renderer.sharedMaterials = sharedMaterials;
            return sharedMaterials;
        }

        public static Color HexToColor(string hex)
        {
            hex = hex.Replace("0x", "").Replace("#", "");
            var r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            var a = hex.Length == 8 ? byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) : (byte)255;

            return new Color32(r, g, b, a);
        }

        public static Color HueShift(Color color, float shift)
        {
            Vector3 hsv;
            Color.RGBToHSV(color, out hsv.x, out hsv.y, out hsv.z);
            hsv.x = Mathf.Repeat(hsv.x + shift, 1f);
            return Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
        }

        /// <summary>
        /// Add a material to this renderer's shared materials
        /// </summary>
        /// <param name="renderer">The renderer on which to add the material</param>
        /// <param name="material">The material to be added</param>
        public static void AddMaterial(this Renderer renderer, Material material)
        {
            var materials = renderer.sharedMaterials;
            var length = materials.Length;
            var newMaterials = new Material[length + 1];
            Array.Copy(materials, newMaterials, length);
            newMaterials[length] = material;
            renderer.sharedMaterials = newMaterials;
        }
    }
}
