using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    public sealed class ListControlDataSource
    {
        Dictionary<string, ListControlItemData> m_Items = new Dictionary<string, ListControlItemData>();

        public delegate void EventHandler(ListControlItemData inData);
        public event EventHandler itemDataAdded;
        public event EventHandler itemDataRemoved;
        public event EventHandler itemDataUpdated;

        public int GetItemCount()
        {
            return m_Items.Count;
        }

        public bool HasItem(string itemId)
        {
            return m_Items.ContainsKey(itemId);
        }

        public void AddItem(ListControlItemData inData)
        {
            if(!HasItem(inData.id))
            {
                m_Items.Add(inData.id, inData);
                itemDataAdded?.Invoke(inData);
            }
        }

        public void RemoveItem(string itemId)
        {
            if(HasItem(itemId))
            {
                var itemToRemove = m_Items[itemId];
                m_Items.Remove(itemId);
                itemDataRemoved?.Invoke(itemToRemove);
            }
        }

        public void UpdateItem(ListControlItemData inData)
        {
            if(HasItem(inData.id))
            {
                m_Items[inData.id] = inData;
                itemDataUpdated?.Invoke(inData);
            }
        }

        public void AddOrUpdateItem(ListControlItemData inData)
        {
            var hadItem = HasItem(inData.id);
            
            m_Items[inData.id] = inData;

            if (hadItem)
                itemDataUpdated?.Invoke(inData);
            else
                itemDataAdded?.Invoke(inData);
        }

        public void Clear()
        {
            foreach (var item in m_Items)
            {
                itemDataRemoved?.Invoke(item.Value);
            }
            m_Items.Clear();
        }
    }
}