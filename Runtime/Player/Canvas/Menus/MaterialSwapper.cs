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
            renderers[inRenderer] = inRenderer.sharedMaterials;
        }

        public void AddRenderers(IEnumerable<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                AddRenderer(renderer);
            }
        }

        public void RemoveRenderer(Renderer inRenderer)
        {
            renderers.Remove(inRenderer);
        }

        public void Clear()
        {
            Restore();
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

        public void SwapRenderers(IEnumerable<Renderer> renderers, Material material)
        {
            foreach (var renderer in renderers)
            {
                SwapRenderer(renderer, material);
            }
        }

        public void SwapRenderer(Renderer renderer, Material material)
        {
            var mats = renderer.sharedMaterials;
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
                r.Key.sharedMaterials = r.Value;
            }
        }
    }
}