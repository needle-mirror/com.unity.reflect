using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    public class MaterialSwapper
    {
        List<Renderer> renderers;
        List<Material[]> originalMaterials;

        public MaterialSwapper()
        {
            renderers = new List<Renderer>();
            originalMaterials = new List<Material[]>();
        }

        public void AddRenderer(Renderer inRenderer)
        {
            renderers.Add(inRenderer);
            originalMaterials.Add(inRenderer.materials);
        }

        public bool ContainsRenderer(Renderer inRenderer)
        {
            return renderers.Contains(inRenderer);
        }
        
        public int Count()
        {
            return renderers.Count;
        }
        
        public void Clear()
        {
            renderers.Clear();
            originalMaterials.Clear();
        }

        public void SetMaterial(Material inMaterial, HashSet<Renderer> excludedRenderer = null, int inStartAt = 0)
        {
            foreach (Renderer r in renderers)
            {
                if ((excludedRenderer == null) || !excludedRenderer.Contains(r))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int m = inStartAt; m < mats.Length; ++m)
                    {
                        mats[m] = inMaterial;
                    }

                    r.sharedMaterials = mats;
                }
            }
        }

        public void Restore()
        {
            for (int r = 0; r < renderers.Count; ++r)
            {
                Renderer renderer = renderers[r];
                if (renderer != null)
                {
                    Material[] mats = originalMaterials[r];
                    renderer.sharedMaterials = mats;
                }
            }
        }
    }
}