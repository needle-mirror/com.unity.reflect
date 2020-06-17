using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public abstract class RuntimeImporter<TModel, TAsset> where TModel : ISyncModel
    {       
        public TAsset Import(TModel model, object settings)
        {
            var asset = CreateNew(model, settings);
            
            ImportInternal(model, asset, settings);
            
            return asset;
        }

        public void Reimport(TModel model, TAsset asset, object settings)
        {
            Clear(asset);
            ImportInternal(model, asset, settings);
        }
        
        public abstract TAsset CreateNew(TModel model, object settings = null);
        
        protected abstract void Clear(TAsset asset);

        protected abstract void ImportInternal(TModel model, TAsset texture, object settings);
    }
}
