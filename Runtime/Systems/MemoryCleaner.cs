using System;

namespace UnityEngine.Reflect
{
    public enum MemoryLevel
    {
        /// <summary>
        ///     Default value when the state is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        ///     Memory usage is low.
        /// </summary>
        Low,

        /// <summary>
        ///     Memory usage is in a range that is considered ideal.
        /// </summary>
        Medium,

        /// <summary>
        ///     Emitted when the memory consumption is high, but not critical yet.
        ///     This is generally a buffer so the software has more time to free memory
        ///     before reaching the critical point.
        /// </summary>
        High,

        /// <summary>
        ///     Critical signal, meaning that the application MUST free some memory
        ///     or it may get killed by the OS and/or slow the system to a crawl.
        /// </summary>
        Critical
    }

    public class MemoryLevelChanged
    {
        public MemoryLevel Level;
    }

    public class PeriodicMemoryEvent
    {
        public MemoryLevel Level;
    }

    public class MemoryThresholdChanged
    {
        public long MediumMemoryThreshold;
        public long HighMemoryThreshold;
    }

    /// <summary>
    ///     System that broadcasts different events about the state of the memory consumption
    /// </summary>
    public class MemoryCleaner
    {
        static long k_Megabytes = 1024L * 1024L;

        long[] m_CriticalSafeGuard = new long[64 * k_Megabytes];
        
        IUnityStatic m_Static;
        Clock.Proxy m_Clock;
        MemoryStats.Proxy m_Stats;
        EventHub m_Hub;

        MemoryLevel m_CurrentLevel;
        TimeSpan m_LastPeriodicTime;
        long m_SystemMemorySize;

        long m_MediumMemoryThreshold;
        long m_HighMemoryThreshold;

        public struct Proxy
        {
            MemoryCleaner m_Instance;

            public Proxy(MemoryCleaner instance)
            {
                m_Instance = instance;
            }

            public void SetMaxMemory(long maxMemorySize)
            {
                m_Instance.SetMaxMemory(true, maxMemorySize);
            }

            public void RemoveMaxMemory()
            {
                m_Instance.SetMaxMemory(false, 0);
            }

            public MemoryLevel memoryLevel => m_Instance.m_CurrentLevel;
        }

        public void Initialize(IUnityStatic unityStatic, Clock.Proxy clock, MemoryStats.Proxy stats, EventHub hub)
        {
            m_Static = unityStatic;
            m_Clock = clock;
            m_Stats = stats;
            m_Hub = hub;

            m_SystemMemorySize = m_Static.systemMemorySize;

            m_Static.lowMemory += OnCriticalMemory;
        }

        public void Shutdown()
        {
            m_Static.lowMemory -= OnCriticalMemory;
        }

        public void Start()
        {
            ComputeMemoryUsageLimit();
            m_LastPeriodicTime = m_Clock.frameTime;
        }

        public void Tick()
        {
            UpdateMemoryLevel();
            
            var currentTime = m_Clock.frameTime;
            if (currentTime - m_LastPeriodicTime > TimeSpan.FromSeconds(1))
            {
                m_LastPeriodicTime = currentTime;
                m_Hub.Broadcast(new PeriodicMemoryEvent{ Level = m_CurrentLevel });
            }
        }

        void UpdateMemoryLevel()
        {
            var currentLevel = GetCurrentLevel();

            if (currentLevel != m_CurrentLevel)
            {
                m_Hub.Broadcast(new MemoryLevelChanged{ Level = currentLevel });
                m_CurrentLevel = currentLevel;
                
                if (currentLevel >= MemoryLevel.High)
                {
                    GC.Collect();
                }
            }
        }

        void OnCriticalMemory()
        {
            m_CriticalSafeGuard = null;
            GC.Collect();
            m_Hub.Broadcast(new MemoryLevelChanged{ Level = MemoryLevel.Critical });
            m_CurrentLevel = MemoryLevel.Critical;
            GC.Collect();
            m_CriticalSafeGuard = new long[64 * k_Megabytes];
        }

        MemoryLevel GetCurrentLevel()
        {
            var usedMemory = m_Stats.frameTotalMemory;
            if (usedMemory > m_SystemMemorySize)
                return MemoryLevel.Critical;
            if (usedMemory > m_HighMemoryThreshold)
                return MemoryLevel.High;
            if (usedMemory > m_MediumMemoryThreshold)
                return MemoryLevel.Medium;
            return MemoryLevel.Low;
        }

        void SetMaxMemory(bool isOverriding, long maxMemorySize)
        {
            if (isOverriding)
            {
                m_SystemMemorySize = maxMemorySize;
                ComputeMemoryUsageLimit();
            }
            else
            {
                m_SystemMemorySize = m_Static.systemMemorySize;
                ComputeMemoryUsageLimit();
            }
        }

        /// <summary>
        ///     Computes the <see cref="MemoryLevel.High"/> value
        /// </summary>
        void ComputeMemoryUsageLimit()
        {
            m_HighMemoryThreshold = m_SystemMemorySize - GetReservedMemoryForOS(m_SystemMemorySize);
            m_MediumMemoryThreshold = (long)(m_HighMemoryThreshold * 0.8f);

            m_Hub.Broadcast(new MemoryThresholdChanged{ MediumMemoryThreshold = m_MediumMemoryThreshold, HighMemoryThreshold = m_HighMemoryThreshold });
        }

        static long GetReservedMemoryForOS(long systemMemorySize)
        {
            // Rough estimation of OS memory requirements
            var reserved = 1024;
            if (systemMemorySize <= 512 * k_Megabytes)
            {
                reserved = 256;
            }
            else if (systemMemorySize <= 1024 * k_Megabytes)
            {
                reserved = 512;
            }
            else if (systemMemorySize <= 2048 * k_Megabytes)
            {
                reserved = 832;
            }
            else if (systemMemorySize > 8192 * k_Megabytes)
            {
                reserved = 2048;
            }

            return reserved * k_Megabytes;
        }
    }
}

