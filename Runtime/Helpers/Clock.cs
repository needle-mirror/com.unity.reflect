using System;
using System.Diagnostics;

namespace UnityEngine.Reflect
{
    public class Clock
    {
        TimeSpan m_FrameTime;
        TimeSpan m_LastFrameTime;
        TimeSpan m_DeltaTime;

        public void Start()
        {
            m_FrameTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            m_LastFrameTime = m_FrameTime;
            m_DeltaTime = TimeSpan.FromSeconds(0);
        }

        public void Tick()
        {
            var currentTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            m_LastFrameTime = m_FrameTime;
            m_DeltaTime = currentTime - m_FrameTime;
            m_FrameTime = currentTime;
        }

        public struct Proxy
        {
            Clock m_Clock;

            public Proxy(Clock clock)
            {
                m_Clock = clock;
            }
            
            /// <summary>
            ///     The time at the beginning of the frame
            /// </summary>
            public TimeSpan frameTime => m_Clock.m_FrameTime;

            /// <summary>
            ///     The time at the beginning of the previous frame.
            ///     This is equivalent of doing <see cref="frameTime"/> - <see cref="deltaTime"/>
            /// </summary>
            public TimeSpan lastFrameTime => m_Clock.m_LastFrameTime;

            /// <summary>
            ///     The delta time since the last frame
            /// </summary>
            public TimeSpan deltaTime => m_Clock.m_DeltaTime;
        }
    }
}
