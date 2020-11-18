using System;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Access to Unity static event and properties through an instance.
    ///     All += or -= for these events are not expected to be called from multiple threads at the same time.
    ///     This mainly exists for testing purpose.
    /// </summary>
    public interface IUnityStatic
    {
        event Action lowMemory;
        long systemMemorySize { get; }
    }

    /// <summary>
    ///     <inheritdoc cref="IUnityStatic"/>
    /// </summary>
    public class UnityStatic : IUnityStatic
    {
        Action m_LowMemory;
        public event Action lowMemory
        {
            add
            {
                m_LowMemory += value;
                if (m_LowMemory.GetInvocationList().Length == 1)
                {
                    Application.lowMemory += OnLowMemory;
                }
            }
            remove
            {
                m_LowMemory -= value;
                if (m_LowMemory == null)
                {
                    Application.lowMemory -= OnLowMemory;
                }
            }
        }

        public long systemMemorySize => SystemInfo.systemMemorySize * 1024L * 1024L;

        void OnLowMemory()
        {
            m_LowMemory();
        }
    }
}
