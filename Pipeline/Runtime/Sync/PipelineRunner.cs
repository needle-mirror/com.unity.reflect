using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.Reflect.Pipeline
{
    public class PipelineRunner
    {
        ReflectBootstrapper m_Hook;

        IReflectRootNode m_Root;
        IList<IReflectNode> m_Nodes;
        IList<IReflectNodeProcessor> m_Processors;
        IUpdateDelegate m_UpdateDelegate;

        public IEnumerable<IReflectNodeProcessor> processors => m_Processors;

        public PipelineRunner(ReflectBootstrapper hook)
        {
            m_Hook = hook;
        }

        public void CreateProcessors(ReflectPipeline pipeline, ISyncModelProvider provider)
        {
            m_Nodes = pipeline.pipelineAsset.nodes;
            CreateProcessors(pipeline, pipeline, provider);
        }
        
        public void CreateProcessors(IEnumerable<IReflectNode> nodes, IUpdateDelegate updateDelegate, IExposedPropertyTable resolver, ISyncModelProvider provider)
        {
            m_Nodes = nodes.ToList();
            CreateProcessors(updateDelegate, resolver, provider);
        }
        
        void CreateProcessors(IUpdateDelegate updateDelegate, IExposedPropertyTable resolver, ISyncModelProvider provider)
        {
            m_Root = m_Nodes.FirstOrDefault(n => n is IReflectRootNode) as IReflectRootNode;

            if (m_Root == null)
            {
                Debug.LogError($"Cannot start pipeline without a {nameof(IReflectRootNode)}");
                return;
            }
            
            m_UpdateDelegate = updateDelegate;

            m_Processors = new List<IReflectNodeProcessor>();

            foreach (var node in m_Nodes)
            {
                var n = node.CreateProcessor(m_Hook, provider, resolver);
                
                if (n is ReflectTask task)
                {
                    task.SetUpdateDelegate(updateDelegate);
                }

                m_Processors.Add(n);
            }
        }

        public void Initialize()
        {
            if (m_Processors == null)
                return;
            
            foreach (var n in m_Processors)
            {
                n.OnPipelineInitialized();
            }
        }

        public void Refresh()
        {
            m_Root.Refresh();
        }

        public void Shutdown()
        {
            if (m_Processors == null)
                return;
            
            foreach (var n in m_Processors)
            {
                try
                {
                    n.OnPipelineShutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
                
                if (n is ReflectTask task)
                {
                    task.RemoveUpdateDelegate(m_UpdateDelegate);
                }
            }
        }
    }
}
