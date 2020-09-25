using System;
using System.Linq;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    class ExposedReferenceLookUp : SerializableDictionary<PropertyName, Object> { }

    public class ReflectPipeline : MonoBehaviour, IUpdateDelegate, IExposedPropertyTable
    {
        public PipelineAsset pipelineAsset;

        public event Action<float> update;
        
        [SerializeField, HideInInspector]
        ExposedReferenceLookUp m_ExposedReferenceLookUp = new ExposedReferenceLookUp();

        PipelineRunner m_PipelineRunner;
        
        public bool TryGetNode<T>(out T node) where T : class, IReflectNode
        {
            node = null;
            
            return pipelineAsset != null && pipelineAsset.TryGetNode(out node);
        }

        public void InitializeAndRefreshPipeline(ISyncModelProvider provider)
        {
            InitializePipeline(provider);
            RefreshPipeline();
        }

        public void InitializePipeline(ISyncModelProvider provider)
        {
            if (pipelineAsset == null)
            {
                Debug.LogError("Unable start pipeline. Please assign a Pipeline Asset.");
                return;
            }

            m_PipelineRunner = new PipelineRunner();
            
            m_PipelineRunner.CreateProcessors(this, provider);
            
            m_PipelineRunner.Initialize();
        }

        public void RefreshPipeline()
        {
            m_PipelineRunner?.Refresh();
        }
        
        public void ShutdownPipeline()
        {
            m_PipelineRunner?.Shutdown();
            m_PipelineRunner = null;
        }
        
        void Update()
        {
            update?.Invoke(Time.unscaledDeltaTime);
        }
        
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (m_PipelineRunner == null)
                return;

            foreach (var node in m_PipelineRunner.processors)
            {
                if (node is IOnDrawGizmosSelected gizmoDrawer)
                {
                    gizmoDrawer.OnDrawGizmosSelected();
                }
            }
        }
#endif
        
        public void SetReferenceValue(PropertyName id, Object value)
        {
            m_ExposedReferenceLookUp[id] = value;
        }

        public Object GetReferenceValue(PropertyName id, out bool idValid)
        {
            idValid = m_ExposedReferenceLookUp.TryGetValue(id, out var obj);
            return obj;
        }

        public void ClearReferenceValue(PropertyName id)
        {
            m_ExposedReferenceLookUp.Remove(id);
        }
    }
}
