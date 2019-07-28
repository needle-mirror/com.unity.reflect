
namespace UnityEngine.Reflect.Controller.Gestures.Touch
{
    public abstract class TouchGesture : IGesture
    {
        public abstract void Update();

        protected Vector2 ComputeCentroid()
        {
            var touchCount = Input.touchCount;
            if (touchCount == 0)
                return Vector2.zero;

            var result = Vector2.zero;
            for (var i = 0; i < touchCount; i++)
            {
                result += Input.GetTouch(i).position;
            }
            return result / touchCount;
        }

        protected Vector2 FromPixels(Vector2 value)
        {
            return value / Mathf.Max(Screen.width, Screen.height);
        }
    }
}