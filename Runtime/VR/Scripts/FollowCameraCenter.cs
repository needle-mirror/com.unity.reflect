using UnityEngine;

namespace UnityEngine.Reflect
{
    public class FollowCameraCenter : MonoBehaviour
    {
        [SerializeField]
        Camera m_Camera = null;

        [SerializeField]
        float m_Distance = 100f;

        [SerializeField]
        float m_RefreshRate = 0.5f;

        [SerializeField]
        Vector2 m_ViewportMin = Vector2.zero;

        [SerializeField]
        Vector2 m_ViewportMax = Vector2.one;

        float m_Timer;
        Vector3 m_ViewportPoint;

        protected void Start()
        {
            Refresh();
        }

        protected void Update()
        {
            m_Timer += Time.deltaTime;
            if (m_Timer > m_RefreshRate)
            {
                m_Timer = 0f;
                if (!CheckIsVisible())
                {
                    Refresh();
                }
            }
        }

        void Refresh()
        {
            transform.SetPositionAndRotation(m_Camera.transform.forward * m_Distance, 
                Quaternion.LookRotation(m_Camera.transform.forward, m_Camera.transform.up));
        }

        bool CheckIsVisible()
        {
            m_ViewportPoint = m_Camera.WorldToViewportPoint(transform.position);

            return m_ViewportPoint.x > m_ViewportMin.x &&
                m_ViewportPoint.x < m_ViewportMax.x &&
                m_ViewportPoint.y > m_ViewportMin.y &&
                m_ViewportPoint.y < m_ViewportMax.y &&
                m_ViewportPoint.z > 0f;
        }
    }
}
