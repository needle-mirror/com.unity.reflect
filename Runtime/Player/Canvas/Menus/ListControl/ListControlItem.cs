using System;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class ListControlItem : MonoBehaviour
    {
        public Text id;
        public Text title;
        public Text description;
        public Text date;
        public Image image;
        public Button openButton;
        public Button deleteButton;
        public Button downloadButton;
        public Image connectedImage;
        public Image disconnectedImage;

        public delegate void EventHandler(ListControlItemData inData);
        public event EventHandler onOpen;
        public event EventHandler onDownload;
        public event EventHandler onDelete;

        ListControlItemData m_Data;

        public void UpdateData(Project project)
        {
            var inData = new ListControlItemData
            {
                project = project,
                id = project.serverProjectId,
                projectId = project.projectId,
                title = project.name,
                description = project.description,
                date = DateTime.Today, // TODO
                options = ListControlItemData.Option.Open, //ResolveAvailableOptions(project),
                enabled = true,
                payload = project
            };
            
            UpdateData(inData);
        }

        public void UpdateData(ListControlItemData inData)
        {
            m_Data = inData;

            if (id != null)
            {
                id.text = m_Data.id;
            }
            
            if (title != null)
            {
                title.text = m_Data.title;
                title.color = GetEnabledColor(title.color);
            }
            
            if (description != null)
            {
                description.text = m_Data.description;
                description.color = GetEnabledColor(description.color);
            }
            
            if (date != null)
            {
                date.text = m_Data.date.ToShortDateString();
                date.color = GetEnabledColor(date.color);
            }
            
            if (image != null)
            {
                image.sprite = m_Data.image;
                image.color = GetEnabledColor(image.color);
            }
            
            if (openButton != null)
            {
                openButton.interactable = m_Data.enabled && HasOption(ListControlItemData.Option.Open);
            }
            
            if (downloadButton != null)
            {
                downloadButton.interactable = m_Data.enabled && HasOption(ListControlItemData.Option.Download);
            }
            
            if (deleteButton != null)
            {
                deleteButton.interactable = m_Data.enabled && HasOption(ListControlItemData.Option.LocalFiles);
            }

            var connected = HasOption(ListControlItemData.Option.Connected);
            if (connectedImage != null)
            {
                connectedImage.gameObject.SetActive(connected);
            }
            if (disconnectedImage != null)
            {
                disconnectedImage.gameObject.SetActive(!connected);
            }
            
            var background = GetComponent<Image>();
            if (background != null)
            {
                background.color = inData.selected ? new Color(0.5f, 0.5f, 0.5f, 1f) :  new Color(0f, 0f, 0f, 0.5f);
            }

        }

        bool HasOption(ListControlItemData.Option flag)
        {
            return (m_Data.options & flag) != 0;
        }

        private Color GetEnabledColor(Color color)
        {
            color.a = m_Data.enabled ? 1f : 0.2f;
            return color;
        }

        public void OnOpen()
        {
            onOpen?.Invoke(m_Data);
        }

        public void OnDownload()
        {
            onDownload?.Invoke(m_Data);
        }

        public void OnDelete()
        {
            onDelete?.Invoke(m_Data);
        }
    }
}
