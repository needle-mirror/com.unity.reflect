using System;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Base class to hook all modules with Unity main thread execution.
    ///     Inherits from <see cref="ReflectBootstrapperBehavior"/> and from <see cref="ReflectBootstrapper"/> to add new h.s.s modules.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class ReflectBootstrapperBehavior : MonoBehaviour, IUpdateDelegate
    {
        public virtual ReflectBootstrapper Hook { get; protected set; }
        public event Action<float> update;

        protected virtual void Awake()
        {
            Hook = new ReflectBootstrapper(this);
            Hook.Initialize();
        }

        protected virtual void OnDestroy()
        {
            Hook.Shutdown();
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
            update?.Invoke(Time.unscaledDeltaTime);
        }
    }
}
