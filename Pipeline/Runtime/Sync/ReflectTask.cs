using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IOnDrawGizmosSelected
    {
        void OnDrawGizmosSelected();
    }

    [Serializable]
    public class UpdateDelegateParam : Param<IUpdateDelegate> { }

    public abstract class ReflectTask : IDisposable
    {
        protected Task m_Task;
        protected CancellationTokenSource m_TokenSource;
        protected CancellationToken m_Token;

        public event Action<Exception> onException; 

        public void SetUpdateDelegate(IUpdateDelegate value) => value.update += Update;
        public void RemoveUpdateDelegate(IUpdateDelegate value) => value.update -= Update;
        
        void Update(float unscaledDeltaTime)
        {
            if (m_TokenSource != null && m_TokenSource.IsCancellationRequested)
            {
                return;
            }
            
            if (m_Task != null && m_Task.IsFaulted)
            {
                var exception = m_Task.Exception;
                m_Task = null;
                
                Debug.LogException(exception);
                onException?.Invoke(exception);
            }

            UpdateInternal(unscaledDeltaTime);
        }

        public virtual void Run()
        {
            if (m_Task == null)
            {
                m_TokenSource = new CancellationTokenSource();
                m_Token = m_TokenSource.Token;
                m_Task = Task.Run(() => RunInternal(m_Token).Wait(m_Token), m_Token);
                
                //Debug.Log($"Starting Process {this}");
            }
        }

        protected abstract Task RunInternal(CancellationToken token);
        
        protected abstract void UpdateInternal(float unscaledDeltaTime);

        public virtual void Dispose()
        {
            if (m_Task == null)
                return;

            if (m_Task.IsCompleted)
            {
                m_TokenSource = null;
                m_Task = null;
                return;
            }

            m_TokenSource.Cancel();

            try
            {
                const int timeOut = 2000;
                if (!m_Task.Wait(timeOut))
                {
                    Debug.LogError($"Task {this} did not stop after {timeOut}MS ({m_Task.Status}).");
                }
            }
            catch (Exception)
            {
                // Task properly cancelled
            }
            finally
            {
                m_TokenSource.Dispose();
            }

            m_TokenSource = null;
            m_Task = null;
        }

        ~ReflectTask()
        {
            if (m_Task != null && !m_Task.IsCompleted)
            {
                Debug.LogError($"Task {this} was not stopped.");
            }
        }
    }
}
