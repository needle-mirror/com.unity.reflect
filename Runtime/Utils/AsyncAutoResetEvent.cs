using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Simple implementation to be able to await in a
    ///     task for an <see cref="AutoResetEvent"/> without blocking a thread.
    /// </summary>
    public class AsyncAutoResetEvent
    {
        static readonly Task k_Completed = Task.FromResult(true);
        readonly Queue<(TaskCompletionSource<bool>, CancellationTokenRegistration)> m_Waits = new Queue<(TaskCompletionSource<bool>, CancellationTokenRegistration)>();
        bool m_Signaled;

        public Task WaitAsync(CancellationToken token = default)
        {
            lock (m_Waits)
            {
                if (m_Signaled)
                {
                    m_Signaled = false;
                    return k_Completed;
                }

                var tcs = new TaskCompletionSource<bool>();
                var registration = token.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.SetCanceled();
                    }
                });
                m_Waits.Enqueue((tcs, registration));
                return tcs.Task;
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> tcs = null;
            var registration = new CancellationTokenRegistration();

            lock (m_Waits)
            {
                if (m_Waits.Count > 0)
                    (tcs, registration) = m_Waits.Dequeue();
                else if (!m_Signaled)
                    m_Signaled = true;
            }

            if (tcs != null)
            {
                tcs.SetResult(true);
                registration.Dispose();
            }
        }
    }
}
