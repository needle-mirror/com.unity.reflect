using System;

namespace UnityEngine.Reflect.Controller.Gestures.Touch
{
    public class TouchDragGesture : TouchGesture
    {
        public event Action<Vector2, int> onDragStart;
        public event Action<Vector2, int> onDragEnd;
        public event Action<Vector2, int> onDrag;

        public override void Update()
        {
            var touches = Input.touches;
            foreach (var touch in touches)
            {
                if (touch.phase == TouchPhase.Began)
                    onDragStart?.Invoke(touch.position, touch.fingerId);
                else if (touch.phase == TouchPhase.Ended)
                    onDragEnd?.Invoke(touch.position, touch.fingerId);
                else
                    onDrag?.Invoke(touch.position, touch.fingerId);
            }
        }
    }
}