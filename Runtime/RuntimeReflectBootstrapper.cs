using System;
using Unity.Reflect;
using Unity.Reflect.Actor;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Base class to hook all modules with Unity main thread execution.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class RuntimeReflectBootstrapper : MonoBehaviour, IExposedPropertyTable
    {
        [SerializeField, HideInInspector]
        ExposedReferenceLookUp m_ExposedReferenceLookUp = new ExposedReferenceLookUp();

        public bool EnableExperimentalActorSystem;
        public ActorSystemSetup Asset;

        public virtual ReflectBootstrapper Hook { get; protected set; }

        protected virtual void Awake()
        {
            Hook = new ReflectBootstrapper();
            Hook.Initialize();
        }

        protected virtual void OnDestroy()
        {
            Hook.Shutdown();
            // HACK: this is required because a client is always created and therefore needs to be disposed of properly
            ProjectServer.Cleanup();
        }

        protected virtual void OnEnable()
        {
            Hook.Start();
        }

        protected virtual void OnDisable()
        {
            Hook.Stop();
        }

        protected virtual void Update()
        {
            Hook.Tick();
        }
        
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

        public void InstantiateAndStart(ActorSystemSetup actorSystemSetup, 
            IExposedPropertyTable resolver = null, 
            UnityProject unityProject = null, 
            UnityUser unityUser = null)
        {
            Hook.InstantiateAndStart(actorSystemSetup, resolver, unityProject, unityUser);
        }

        public T FindActor<T>() where T : class
        {
            return Hook.FindActor<T>();
        }

        [Serializable]
        class ExposedReferenceLookUp : SerializedDictionary<PropertyName, Object> { }
    }
}
