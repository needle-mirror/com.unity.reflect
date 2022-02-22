using System.Threading;
using System.Threading.Tasks;

namespace Unity.Reflect.ActorFramework
{
    /// <summary>
    ///     Multiple Producer Single Consumer Synchronizer.
    /// </summary>
    public class MpscSynchronizer
    {
        static readonly Task k_Completed = Task.FromResult(true);

        object m_Lock = new object();
        TaskCompletionSource<bool> m_Tcs;
        CancellationTokenRegistration m_Registration;
        int m_NbSignals;

        public Task WaitAsync(CancellationToken token)
        {
            lock (m_Lock)
            {
                if (m_NbSignals > 0)
                {
                    --m_NbSignals;
                    return k_Completed;
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var registration = token.Register(() =>
                {
                    tcs.TrySetCanceled();
                });
                (m_Tcs, m_Registration) = (tcs, registration);
                return tcs.Task;
            }
        }

        public void RemoveOneSignal()
        {
            lock (m_Lock)
            {
                --m_NbSignals;
                if (m_NbSignals < 0)
                    m_NbSignals = 0;
            }
        }

        public bool IsCurrentlySignaled()
        {
            lock (m_Lock)
                return m_NbSignals > 0;
        }

        public void Set()
        {
            lock (m_Lock)
            {
                if (m_Tcs != null)
                {
                    m_Tcs.TrySetResult(true);
                    m_Registration.Dispose();
                    m_Tcs = null;
                }
                else
                    ++m_NbSignals;
            }
        }
    }
}
