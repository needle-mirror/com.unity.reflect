using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    public class MaterialSwapper
    {
        Dictionary<Renderer, Material[]> renderers;

        public MaterialSwapper()
        {
            renderers = new Dictionary<Renderer, Material[]>();
        }

        public void AddRenderer(Renderer inRenderer)
        {
            renderers[inRenderer] = inRenderer.materials;
        }

        public void RemoveRenderer(Renderer inRenderer)
        {
            renderers.Remove(inRenderer);
        }

        public void Clear()
        {
            renderers.Clear();
        }

        public void SetMaterial(Material inMaterial, HashSet<Renderer> excludedRenderer = null)
        {
            foreach (var r in renderers)
            {
                if ((excludedRenderer == null) || !excludedRenderer.Contains(r.Key))
                {
                    SwapRenderer(r.Key, inMaterial);
                }
            }
        }

        public void SwapRenderer(Renderer renderer, Material material)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int m = 0; m < mats.Length; ++m)
            {
                mats[m] = material;
            }

            renderer.sharedMaterials = mats;
        }
        
        public void Restore()
        {
            foreach (var r in renderers)
            {
                Material[] mats = r.Value;
                r.Key.sharedMaterials = mats;
            }
        }
    }
}