using System.Collections.Generic;

namespace UnityEngine.Reflect.Controller.Gestures
{
    public class GestureListener
    {
        private List<IGesture> m_Listeners = new List<IGesture>();

        public void Update()
        {
            foreach (var listener in m_Listeners)
            {
                listener.Update();
            }
        }

        public void AddListeners(params IGesture[] listeners)
        {
            foreach (var listener in listeners)
            {
                m_Listeners.Add(listener);
            }
        }

        public void RemoveListener(IGesture listener)
        {
            m_Listeners.Remove(listener);
        }

        public void Clear()
        {
            m_Listeners.Clear();
        }
    }
}
