using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.ActorFramework
{
    public class TimerComponent : IAsyncComponent
    {
        CancellationToken m_GlobalToken;
        
        MpscSynchronizer m_Synchronizer = new MpscSynchronizer();
        volatile bool m_WorkIsWaiting;
        object m_Lock = new object();
        int m_NextId;

        Dictionary<int, Wrapper> m_Timers = new Dictionary<int, Wrapper>();
        List<Wrapper> m_ReadyTimers = new List<Wrapper>();
        List<Wrapper> m_ReadyCached = new List<Wrapper>();
        
        public TimerComponent(CancellationToken globalToken)
        {
            m_GlobalToken = globalToken;
        }

        public TickResult Tick(TimeSpan endTime)
        {
            // Useful just to discard lock for all actors not using timers
            if (!m_WorkIsWaiting)
                return TickResult.Wait;

            lock (m_Lock)
            {
                m_ReadyCached.AddRange(m_ReadyTimers);
                m_ReadyTimers.Clear();
            }

            foreach (var ready in m_ReadyCached)
            {
                ready.Timer.Dispose();
                ready.Callback();
            }
            m_ReadyCached.Clear();

            return TickResult.Wait;
        }

        public async Task<WaitResult> WaitAsync(CancellationToken token)
        {
            try
            {
                await m_Synchronizer.WaitAsync(token).ConfigureAwait(false);
            }
            catch
            {
                lock (m_Lock)
                {
                    if (m_Timers.Count == 0)
                        return WaitResult.Completed;
                }

                await m_Synchronizer.WaitAsync(default).ConfigureAwait(false);
            }

            return WaitResult.Continuing;
        }

        public void DelayedExecute(TimeSpan delay, Action callback)
        {
            if (m_GlobalToken.IsCancellationRequested)
                return;

            Action<object> timerCallback = id =>
            {
                var timerId = (int)id;
                lock (m_Lock)
                {
                    var wrapper = m_Timers[timerId];
                    m_Timers.Remove(timerId);
                    m_ReadyTimers.Add(wrapper);
                }

                m_WorkIsWaiting = true;
                m_Synchronizer.Set();
            };

            ++m_NextId;
            
            lock (m_Lock)
            {
                var timer = new Timer(new TimerCallback(timerCallback), m_NextId, delay, TimeSpan.FromMilliseconds(-1));
                m_Timers.Add(m_NextId, new Wrapper(timer, callback));
            }
        }

        struct Wrapper
        {
            public Timer Timer;
            public Action Callback;

            public Wrapper(Timer timer, Action callback)
            {
                Timer = timer;
                Callback = callback;
            }
        }
    }
}
