using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    public class ListControl : MonoBehaviour
    {
        public GameObject itemTemplate;

        public delegate void EventHandler(ListControlItemData inData);
        public event EventHandler onOpen;
        public event EventHandler onDownload;
        public event EventHandler onDelete;

        ListControlDataSource dataSource;
        Dictionary<string, ListControlItem> items = new Dictionary<string, ListControlItem>();

        public void SetDataSource(ListControlDataSource inDataSource)
        {
            dataSource = inDataSource;

            if (dataSource != null)
            {
                dataSource.itemDataAdded += OnItemAdded;
                dataSource.itemDataRemoved += OnItemRemoved;
                dataSource.itemDataUpdated += OnItemUpdated;
            }
        }

        void OnItemAdded(ListControlItemData inData)
        {
            //  create game object
            GameObject obj = Instantiate(itemTemplate, itemTemplate.transform.parent);
            obj.SetActive(true);

            //  store data
            ListControlItem item = obj.GetComponent<ListControlItem>();
            item.onOpen += OnOpen;
            item.onDownload += OnDownload;
            item.onDelete += OnDelete;
            item.UpdateData(inData);
            items.Add(inData.id, item);
        }

        void OnItemRemoved(ListControlItemData inData)
        {
            if (items.TryGetValue(inData.id, out var item))
            {
                items.Remove(inData.id);
                item.transform.SetParent(null);
                Destroy(item.gameObject);
            }
        }

        void OnItemUpdated(ListControlItemData inData)
        {
            ListControlItem item;
            if (items.TryGetValue(inData.id, out item))
            {
                item.UpdateData(inData);
            }
        }

        public void OnOpen(ListControlItemData inData)
        {
            onOpen?.Invoke(inData);
        }

        public void OnDownload(ListControlItemData inData)
        {
            onDownload?.Invoke(inData);
        }

        public void OnDelete(ListControlItemData inData)
        {
            onDelete?.Invoke(inData);
        }
    }
}