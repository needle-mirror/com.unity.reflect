using System;
using Unity.Reflect.Utils;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    class ExposedReferenceLookUp : SerializableDictionary<PropertyName, Object> { }
    
    public class ReflectPipeline : MonoBehaviour, IUpdateDelegate, IExposedPropertyTable, ILogReceiver
    {
#pragma warning disable CS0649
        [SerializeField, HideInInspector]
        ReflectBootstrapperBehavior m_Hook;
#pragma warning restore CS0649

        public PipelineAsset pipelineAsset;

        public event Action<float> update;

        // Use this event to do any change on the pipeline, like adding node or change connections.
        // Any change done on the pipeline after this callback is not legal and can have unexpected effects.
        public event Action beforeInitialize;

        // Use this event to access newly created node processors. Do not use this callback to change the pipeline. 
        public event Action afterInitialize;
        
        [SerializeField, HideInInspector]
        ExposedReferenceLookUp m_ExposedReferenceLookUp = new ExposedReferenceLookUp();

        PipelineRunner m_PipelineRunner;

        void Awake()
        {
            if (m_Hook == null)
            {
                m_Hook = gameObject.AddComponent<ReflectBootstrapperBehavior>();
            }
            
            // Register to Logs coming from the Reflect core assembly
            Unity.Reflect.Utils.Logger.AddReceiver(this);
        }

        void OnDestroy()
        {
            Unity.Reflect.Utils.Logger.RemoveReceiver(this);
        }

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

            beforeInitialize?.Invoke();

            m_PipelineRunner = new PipelineRunner(m_Hook.Hook);
            
            m_PipelineRunner.CreateProcessors(this, provider);
            
            m_PipelineRunner.Initialize();
            
            afterInitialize?.Invoke();
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
        
        void ILogReceiver.LogReceived(Unity.Reflect.Utils.Logger.Level level, string msg)
        {
            switch (level)
            {
                case Unity.Reflect.Utils.Logger.Level.Debug:
                case Unity.Reflect.Utils.Logger.Level.Info:
                    Debug.Log(msg);
                    break;

                case Unity.Reflect.Utils.Logger.Level.Warn:
                    Debug.LogWarning(msg);
                    break;

                case Unity.Reflect.Utils.Logger.Level.Error:
                case Unity.Reflect.Utils.Logger.Level.Fatal:
                    Debug.LogError(msg);
                    break;

                default:
                    Debug.Log(msg);
                    break;
            }
        }
    }
}
