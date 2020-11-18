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
        IReflectNodeProcessor CreateProcessor(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver);
    }

    [Serializable]
    public abstract class ReflectNode<T> : IReflectNode where T : class, IReflectNodeProcessor
    {
        public T processor { get; private set; }


        public IReflectNodeProcessor CreateProcessor(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            processor = Create(hook, provider, resolver);
            return processor;
        }

        protected abstract T Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver);
    }
}
