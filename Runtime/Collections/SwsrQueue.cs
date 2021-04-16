namespace Unity.Reflect.Actor
{
    /// <summary>
    ///     Single-writer single-reader queue
    /// </summary>
    // Todo: Possible false sharing when cursors are following each other
    public class SwsrQueue<T>
    {
        readonly int m_Capacity;
        readonly T[] m_Items;

        volatile int m_ReadIndex;
        volatile int m_WriteIndex;

        public SwsrQueue() : this(1023) { }

        public SwsrQueue(int capacity)
        {
            // Need one more item to differentiate between full and empty state
            m_Capacity = capacity + 1;
            m_Items = new T[m_Capacity];
        }

        public bool TryEnqueue(T item)
        {
            var next = (m_WriteIndex + 1) % m_Capacity;
            if (next == m_ReadIndex)
                return false;

            m_Items[m_WriteIndex] = item;
            m_WriteIndex = next;
            return true;
        }

        public bool TryDequeue(out T item)
        {
            if (m_ReadIndex == m_WriteIndex)
            {
                item = default;
                return false;
            }

            var next = (m_ReadIndex + 1) % m_Capacity;
            item = m_Items[m_ReadIndex];
            m_ReadIndex = next;
            return true;
        }
    }
}
