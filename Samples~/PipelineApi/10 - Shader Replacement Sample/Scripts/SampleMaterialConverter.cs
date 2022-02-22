using System;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    class SampleMaterialConverterNode : MaterialConverterNode
    {
        public Shader opaqueShader;
        public Shader transparentShader;
        
        protected override MaterialConverter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var converter = new SampleMaterialConverter(hook.Services.EventHub, hook.Services.MemoryTracker, textureCacheParam.value, output, opaqueShader, transparentShader);

            input.streamEvent = converter.OnStreamEvent;

            return converter;
        }
    }

    class SampleMaterialConverter : MaterialConverter
    {
        readonly Shader m_OpaqueShader;
        readonly Shader m_TransparentShader;

        public SampleMaterialConverter(EventHub hub, MemoryTracker memTracker, ITextureCache textureCache, IOutput<SyncedData<Material>> output, Shader opaqueShader, Shader transparentShader)
            : base(hub, memTracker, textureCache, output)
        {
            m_OpaqueShader = opaqueShader;
            m_TransparentShader = transparentShader;
        }

        protected override Material Import(SyncedData<SyncMaterial> syncedMaterial)
        {
            Material material;

            var syncMaterial = syncedMaterial.data;
            var sourceId = syncedMaterial.key.source;
            
            if (syncMaterial.Alpha >= 1.0f)
            {
                material = new Material(m_OpaqueShader);
                
                var map = syncMaterial.AlbedoMap;
                if (map.TextureId != SyncId.None)
                {
                    var textureKey = StreamKey.FromSyncId<SyncTexture>(sourceId, map.TextureId);
                    material.SetTexture("_MainTex", m_TextureCache.GetTexture(textureKey));

                    var offset = map.Offset;
                    material.SetTextureOffset("_MainTex", new Vector2(offset.X, offset.Y));

                    var tiling = map.Tiling;
                    material.SetTextureScale("_MainTex", new Vector2(tiling.X, tiling.Y));
                }
            }
            else
            {
                material = new Material(m_TransparentShader);
                material.SetFloat("_Alpha", syncMaterial.Alpha);
            }

            return material;
        }
    }
}
