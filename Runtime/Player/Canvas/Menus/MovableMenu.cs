
namespace UnityEngine.Reflect
{
    public class MovableMenu : MonoBehaviour
    {
        public enum Direction
        {
            up, right, down, left
        }

        protected RectTransform rect;

        Vector2 targetMin;
        Vector2 targetMax;

        Vector2 originalMin;
        Vector2 originalMax;

        const float speed = 8f;

        protected virtual void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        protected virtual void Start()
        {
            targetMin = rect.offsetMin;
            targetMax = rect.offsetMax;
        }

        public void MoveTo(Vector2 minPos, Vector2 maxPos)
        {
            targetMin = minPos;
            targetMax = maxPos;
        }

        public void MoveDeltaX(float x)
        {
            targetMin.x += x;
            targetMax.x += x;
        }

        public void MoveDeltaY(float y)
        {
            targetMin.y += y;
            targetMax.y += y;
        }

        public void Hide(Direction direction = Direction.down)
        {
            originalMin = targetMin;
            originalMax = targetMax;

            switch (direction)
            {
                case Direction.up:
                    MoveDeltaY(-Screen.height);
                    break;
                case Direction.down:
                    MoveDeltaY(Screen.height);
                    break;
                case Direction.left:
                    MoveDeltaY(-Screen.width);
                    break;
                case Direction.right:
                    MoveDeltaY(Screen.width);
                    break;
            }
        }

        public void Show()
        {
            targetMin = originalMin;
            targetMax = originalMax;
        }

        private void Update()
        {
            rect.offsetMin = Vector2.Lerp(rect.offsetMin, targetMin, Time.deltaTime * speed);
            rect.offsetMax = Vector2.Lerp(rect.offsetMax, targetMax, Time.deltaTime * speed);
        }
    }
}