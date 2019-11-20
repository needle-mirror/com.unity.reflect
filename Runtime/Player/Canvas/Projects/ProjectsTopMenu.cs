using System.Collections;
using UnityEngine;
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
        
#pragma warning restore 0649

        Coroutine m_RefreshButtonCoroutine;
        
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
