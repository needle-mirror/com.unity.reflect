using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MetadataMenu : Menu
    {

        protected new void Start()
        {
            base.Start();

            title.GetComponent<Text>().text = key;

            //  add filters
            float y = 0f;
            foreach (var value in nodes.Keys)
            {
                MenuItem filter;
                nodes.TryGetValue(value, out filter);
                if (filter != null)
                {
                    filter.SetPosition(y);
                    y -= 30f;
                    filter.gameObject.SetActive(true);
                }
            }

            menuItem.gameObject.SetActive(false);

            //  set content scrollable size
            Vector2 offset = scrollContent.GetComponent<RectTransform>().offsetMin;
            offset.y = y;
            scrollContent.GetComponent<RectTransform>().offsetMin = offset;
        }

        public override bool AddNode(Transform node)
        {
            var ret = false;
            if (node.GetComponent<Renderer>() != null)
            {
                var model = node.GetComponent<Metadata>();
                if (model != null)
                {
                    var value = model.GetParameter(key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        MenuItem item;
                        if (!nodes.TryGetValue(value, out item))
                        {
                            var menuitem = NewMenuItem();
                            item = menuitem.GetComponent<MetadataMenuItem>();
                            item.Initialize(this, value);
                            nodes.Add(value, item);
                        }

                        ret = item.AddNode(model.transform);
                    }
                }
            }

            return ret;
        }
    
        public override void RemoveNode(Transform node)
        {
            if (node.GetComponent<Renderer>() != null)
            {
                var model = node.GetComponent<Metadata>();
                if (model != null)
                {
                    var value = model.GetParameter(key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        MenuItem item;
                        nodes.TryGetValue(value, out item);
                        if (item != null)
                        {
                            item.RemoveNode(model.transform);
                        }
                    }
                }
            }
        }
    }
}