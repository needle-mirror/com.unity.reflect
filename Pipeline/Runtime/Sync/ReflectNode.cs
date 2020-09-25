using System;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IReflectNodeProcessor
    {
        void OnPipelineInitialized();
        void OnPipelineShutdown();
    }

    public abstract class ReflectTaskNodeProcessor : ReflectTask, IReflectNodeProcessor
    {
        public virtual void OnPipelineInitialized()
        {
            
        }

        public virtual void OnPipelineShutdown()
        {
            Dispose();
        }
    }

    public interface IReflectNode
    {
        IReflectNodeProcessor CreateProcessor(ISyncModelProvider provider, IExposedPropertyTable resolver);
    }

    [Serializable]
    public abstract class ReflectNode<T> : IReflectNode where T : class, IReflectNodeProcessor
    {
        public T processor { get; private set; }


        public IReflectNodeProcessor CreateProcessor(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            processor = Create(provider, resolver);
            return processor;
        }

        protected abstract T Create(ISyncModelProvider provider, IExposedPropertyTable resolver);
    }
}
