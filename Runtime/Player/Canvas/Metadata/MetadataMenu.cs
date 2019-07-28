using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class MetadataMenu : Menu
    {

        protected override void Awake()
        {
            base.Awake();
        }

        protected new void Start()
        {
            base.Start();

            title.GetComponent<Text>().text = key;

            //  add filters
            float y = 0f;
            foreach (string value in nodes.Keys)
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

        public override void AddNode(Transform node)
        {
            if (node.GetComponent<Renderer>() != null)
            {
                Metadata model = node.GetComponent<Metadata>();
                if (model != null)
                {
                    var value = model.GetParameter(key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        MenuItem item;
                        nodes.TryGetValue(value, out item);
                        if (item == null)
                        {
                            var menuitem = NewMenuItem();
                            item = menuitem.GetComponent<MetadataMenuItem>();
                            item.Initialize(value);
                            nodes.Add(value, item);
                        }

                        item.AddNode(model.transform);
                    }
                }
            }
        }
    }
}