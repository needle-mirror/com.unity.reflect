/*
using System;
using System.Collections.Generic;

namespace UnityEngine.Reflect.Pipeline
{
    public class MemoryCleaner : IStreamInput<GameObject>
    {
        readonly Dictionary<Mesh, int> m_MeshReferences;
        readonly Dictionary<Material, int> m_MaterialReferences;
        readonly Dictionary<Texture, int> m_TextureReferences;

        public MemoryCleaner()
        {
            m_MeshReferences = new Dictionary<Mesh, int>();
            m_MaterialReferences = new Dictionary<Material, int>();
            m_TextureReferences = new Dictionary<Texture, int>();
        }

        static void Hold<T>(IDictionary<T, int> cache, T asset)
        {
            if (cache.TryGetValue(asset, out var count))
            {
                count += 1;
            }
            else
            {
                count = 1;
            }
            cache[asset] = count;
        }
        
        static void Release<T>(IDictionary<T, int> cache, T asset) where T : Object
        {
            var count = cache[asset]; // TODO What if it's not in cache?

            count -= 1;

            if (count <= 0)
            {
                cache.Remove(asset);
                //Debug.Log("Destroying unreferenced asset : " + asset.name);
                Object.Destroy(asset);
            }
            else
            {
                cache[asset] = count;
            }
        }

        public void OnBegin(IStreamOutput<GameObject> output)
        {
            // Nothing
        }

        public void OnStreamAdded(IStreamOutput<GameObject> output, GameObject gameObject)
        {
            //Debug.Log(">> ADDING : " + gameObject.gameObject.name);

            foreach (var filter in gameObject.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                
                Hold(m_MeshReferences, filter.sharedMesh);
            }
            
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                        continue;
                    
                    foreach (var name in mat.GetTexturePropertyNames())
                    {
                        var texture = mat.GetTexture(name) as Texture2D;
                        
                        if (texture == null)
                            continue;
                        
                        Hold(m_TextureReferences, texture);
                    }
                        
                    Hold(m_MaterialReferences, mat);
                }
            }
        }
        
        public void OnStreamChanged(IStreamOutput<GameObject> output, GameObject gameObject)
        {
            //Debug.Log(">> MODIFYING : " + gameObject.gameObject.name);

        }
        
        public void OnStreamRemoved(IStreamOutput<GameObject> output, GameObject gameObject)
        {
            //Debug.Log(">> Destroying : " + syncObject.gameObject.name);

            foreach (var filter in gameObject.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                
                Release(m_MeshReferences, filter.sharedMesh);
            }
            
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                        continue;
                    
                    foreach (var name in mat.GetTexturePropertyNames())
                    {
                        var texture = mat.GetTexture(name) as Texture2D;
                        
                        if (texture == null)
                            continue;
                        
                        Release(m_TextureReferences, texture);
                    }
                        
                    Release(m_MaterialReferences, mat);
                }
            }
        }

        public void OnEnd(IStreamOutput<GameObject> output)
        {
            // Nothing
        }
    }
}
*/