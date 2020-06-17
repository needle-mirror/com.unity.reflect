using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class ProjectsTopMenu : TopMenu
    {
#pragma warning disable 0649
        [SerializeField]
        Button m_RefreshButton;
        
        [SerializeField]
        Image m_RefreshIcon;

        [Space]

        [SerializeField]
        ListControlItem m_Item;

        [SerializeField]
        Transform m_Content;
        
        [Serializable]
        public class ProjectEvent : UnityEvent<Project> { }

        [SerializeField]
        ProjectEvent m_OnItemOpen;
        
#pragma warning restore 0649

        Coroutine m_RefreshButtonCoroutine;

        List<ListControlItem> m_Items = new List<ListControlItem>();
        
        public void RefreshProjectList(List<Project> projects)
        {
            ClearProjects();

            foreach (var project in projects)
            {
                var item = Instantiate(m_Item, m_Content);
                
                item.UpdateData(project);

                item.onOpen += OnItemClicked;
                
                m_Items.Add(item);
            }
        }

        void OnItemClicked(ListControlItemData indata)
        {
            m_OnItemOpen.Invoke(indata.project);
        }

        public void ClearProjects()
        {
            foreach (var item in m_Items)
            {
                Destroy(item.gameObject);
            }

            m_Items.Clear();
        }

        public bool RefreshButtonEnabled
        {
            get => m_RefreshButton.interactable;
            set
            {
                if (m_RefreshButton.interactable == value)
                    return;
                
                m_RefreshButton.interactable = value;

                if (!value)
                {
                    if (m_RefreshButtonCoroutine == null)
                        m_RefreshButtonCoroutine = StartCoroutine(RotateRefreshButton());
                }
            }
        }

        IEnumerator RotateRefreshButton()
        {
            while (!m_RefreshButton.interactable)
            {
                var angle = 0.0f;

                while (angle < 360.0f)
                {
                    angle = Mathf.Min(angle + Time.deltaTime * 500.0f, 360.0f);
                    m_RefreshIcon.rectTransform.rotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, -angle));
                    yield return null;
                }
            }
            
            m_RefreshIcon.rectTransform.rotation = Quaternion.identity;
            m_RefreshButtonCoroutine = null;
        }

        protected override void Start()
        {
            base.Start();
            StartCoroutine(DelayedStart());
        }
        
        public void OnOpen()
        {
            Deactivate();
        }

        public void OnCancel()
        {
            Deactivate();
        }

        IEnumerator DelayedStart()
        {
            yield return null;
            Activate();
        }
    }
}
