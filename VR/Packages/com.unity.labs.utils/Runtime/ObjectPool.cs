using System.Collections.Generic;

namespace Unity.Labs.Utils
{
    public class ObjectPool<T> where T: class, new()
    {
        protected readonly Queue<T> m_Queue = new Queue<T>();

        public virtual T Get()
        {
            return m_Queue.Count == 0 ? new T() : m_Queue.Dequeue();
        }

        public void Recycle(T instance)
        {
            ClearInstance(instance);
            m_Queue.Enqueue(instance);
        }

        /// <summary>
        /// Implement a clearing function in this in a derived class to
        /// have the <seealso cref="Recycle"/> method automatically clear the item.
        /// </summary>
        /// <param name="instance">The object to return to the pool</param>
        protected virtual void ClearInstance(T instance) { }
    }
}
