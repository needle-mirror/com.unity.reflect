using UnityEngine.Profiling;

namespace UnityEngine.Reflect
{
    public class MemoryStats
    {
        long m_FrameTotalMemory;
        long m_FrameUsedMemory;

        public void Tick()
        {
            m_FrameTotalMemory = GetTotalMemory();
            m_FrameUsedMemory = m_FrameTotalMemory - GetReservedMemory();
        }

        static long GetTotalMemory()
        {
            // add 30% overhead. This is close to the worst case we have seen up
            // to now when looking at PrivateWorkingSet64 vs. Profiler values
            // This is not accurate and should be replaced by the PrivateWorkingSet64 computed
            // with ProfilerRecorder API in Unity 2020.2. May be used as a fallback for older (<2020.2) unity versions
            return (long)((Profiler.GetMonoHeapSizeLong() + Profiler.GetTotalReservedMemoryLong()) * 1.3);
        }

        static long GetReservedMemory()
        {
            return Profiler.GetTotalUnusedReservedMemoryLong();
        }

        public struct Proxy
        {
            MemoryStats m_Impl;

            public Proxy(MemoryStats memoryStats)
            {
                m_Impl = memoryStats;
            }

            /// <summary>
            ///     Gives the PrivateWorkingSet64 value. This may or may not be accurate.
            /// </summary>
            public long frameTotalMemory => m_Impl.m_FrameTotalMemory;

            /// <summary>
            ///     An estimation or real memory usage, without free memory in pools
            /// </summary>
            public long frameUsedMemory => m_Impl.m_FrameUsedMemory;
        }
    }
}
