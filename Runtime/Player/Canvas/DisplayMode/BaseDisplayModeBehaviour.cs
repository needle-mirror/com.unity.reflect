using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public abstract class BaseDisplayModeBehaviour : MonoBehaviour, IDisplayMode
    {
        const string FORMAT_DESCRIPTION = "{0}\n{1}";

#pragma warning disable 0649
        [SerializeField] protected string title;
        [SerializeField] protected string description;
        [SerializeField] protected Sprite image;
        [SerializeField] protected bool enabledByDefault;
        [Tooltip("Lower priority at the top of the menu")]
        [SerializeField, Range(0, 10)] protected int menuOrderPriority;
        [SerializeField] protected DisplayModeStatusParameters statusParameters;
#pragma warning restore 0649

        protected ListControlItemData listControlItemData;
        protected ReflectUIManager m_UIManager;

        public string Title => title;

        public string Description => description;

        public Sprite Image => image;

        public bool EnabledByDefault => enabledByDefault;

        public DisplayModeStatusParameters StatusParameters => statusParameters;

        public int MenuOrderPriority => menuOrderPriority;

        public ListControlItemData ListControlItemData => listControlItemData;

        public abstract bool IsAvailable { get; }

        protected virtual void Start()
        {
            m_UIManager = FindObjectOfType<ReflectUIManager>();

            listControlItemData = new ListControlItemData
            {
                id = GetType().Name.Replace("Behaviour", ""),
                title = title,
                image = image,
                options = ListControlItemData.Option.Open,
                enabled = enabledByDefault
            };
            RefreshStatus();
        }

        public virtual void RefreshStatus()
        {
            listControlItemData.description = string.Format(FORMAT_DESCRIPTION, description, GetStatusMessage());
        }

        public virtual void OnModeEnabled(bool isEnabled, ListControlDataSource source)
        {
            // when the display mode is enabled, disable its menu button
            listControlItemData.enabled = !isEnabled;
            source.UpdateItem(listControlItemData);
        }

        public virtual IEnumerator CheckAvailability()
        {
            yield return null;
        }

        public abstract string GetStatusMessage();
    }
}
