using System;
using Unity.Reflect;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Actors;
using UnityEngine.Reflect.Pipeline;

namespace UnityEngine.Reflect
{
    /// <summary>
    /// Base class to hook all modules with Unity main thread execution.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class ReflectActorSystem : MonoBehaviour, IExposedPropertyTable
    {
        bool m_IsShutdown;
        
        public ActorSystemSetup Asset;

        public BridgeActor.Proxy Bridge { get; protected set; }
        public ActorRunner.Proxy Runner { get; protected set; }
        
        [SerializeField, HideInInspector]
        ExposedReferenceLookUp m_ExposedReferenceLookUp = new ExposedReferenceLookUp();

        public virtual ReflectBootstrapper Hook { get; protected set; }

        protected virtual void Awake()
        {
            Hook = new ReflectBootstrapper();
            Hook.Initialize();
        }

        protected virtual void OnApplicationQuit()
        {
            TryShutdown();
        }

        protected virtual void OnDestroy()
        {
            TryShutdown();
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

        public void Instantiate(Project project = null, UnityUser user = null, AccessToken accessToken = null,
            Action<BridgeActor.Proxy> settingsOverrideAction = null, Action<ActorRunner.Proxy> postInstantiationAction = null)
        {
            if (Asset == null)
            {
                Debug.LogError($"A {nameof(ActorSystemSetup)} asset is required.");
                return;
            }
            
            Runner = Hook.Systems.ActorRunner;
            Runner.Instantiate(Asset, project, this, user, accessToken, bridge =>
            {
                Bridge = bridge;
                settingsOverrideAction?.Invoke(bridge);
            }, runner =>
            {
                postInstantiationAction?.Invoke(runner);
            });
        }

        public void Restart()
        {
            // TODO Make sure Instantiate has been called
            Runner.Restart();
        }

        public void StartActorSystem()
        {
            // TODO Make sure Instantiate has been called
            Runner.StartActorSystem();
        }

        public T GetActor<T>() where T : class
        {
            return Hook.GetActor<T>();
        }
        
        public bool TryGetActor<T>(out T actor) where T : class
        {
            return Runner.TryGetActor(out actor);
        }
        
        public void ForwardNet<T>(object data) where T : class
        {
            Bridge.ForwardNet(Hook.Systems.ActorRunner.GetActorHandle<T>(), data);
        }

        public void ForwardRpc<T, TData, TSuccess>(TData data) where T : class where TData : class where TSuccess : class
        {
            ForwardRpc<T, TData, TSuccess>(data, _ => { }, ex => { Debug.LogError($"Forward RPC call failed: {ex}"); });
        }
        
        public void ForwardRpc<T, TData, TSuccess>(TData data, Action<TSuccess> onSuccess, Action<Exception> onFailure) where T : class where TData : class where TSuccess : class
        {
            if (Hook.Systems.ActorRunner.TryGetActorHandle<T>(out var actorSetup))
            {
                Bridge.ForwardRpc<TData, TSuccess>(actorSetup, data, onSuccess, onFailure);
            }
            else
            {
                Debug.LogError($"Unable To find Actor of type '{typeof(T).FullName}'");
            }
        }

        void TryShutdown()
        {
            if (!m_IsShutdown)
            {
                Hook.Shutdown();
                m_IsShutdown = true;
            }
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
    }
}
