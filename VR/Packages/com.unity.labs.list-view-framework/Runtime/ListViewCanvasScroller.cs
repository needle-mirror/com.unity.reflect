using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity.Labs.ListView
{
    public class ListViewCanvasScroller : ListViewScroller, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        RectTransform m_RectTransform;

        void Awake() { m_RectTransform = GetComponent<RectTransform>(); }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RectTransform, eventData.position, null, out localPoint);
            OnScrollStarted(localPoint);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_Scrolling)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RectTransform, eventData.position, null, out localPoint);
                m_ListView.scrollOffset = m_StartOffset - (localPoint.y - m_StartPosition.y);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnScrollEnded();
        }
    }
}
