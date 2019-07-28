using System;
using System.Linq;

namespace UnityEngine.Reflect.Controller.Gestures.Desktop
{
    public class MouseClickGesture : IGesture
    {
        public event Action<Vector2> mouseClicked;

        public KeyCode Button { get; set; } = KeyCode.Mouse0;
        public KeyCode[] NeededButtons { get; set; } = new KeyCode[0];
        public int ClickNumber { get; set; } = 1;
        public float TimeAllowedBetweenClicks { get; set; } = 1;
        public float TimeAllowedAsClick { get; set; } = .5f;
        public bool ResetAfterComplete { get; set; } = true;

        float lastMouseDownTime = 0;
        float lastMouseUpTime = 0;
        int clickCount = 0;

        public MouseClickGesture(Action<Vector2> mouseClicked)
        {
            this.mouseClicked += mouseClicked;
        }

        public MouseClickGesture()
        {
        }

        public void Update()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (NeededButtons.Any(button => !Input.GetKey(button)))
                return;

            CheckClick();
#endif
        }

        void CheckClick()
        {
            if (Input.GetKeyDown(Button))
            {
                if (clickCount > 0 && (Time.time - lastMouseUpTime) > TimeAllowedBetweenClicks)
                {
                    Reset();
                }
                lastMouseDownTime = Time.time;
            }
            else if (Input.GetKeyUp(Button))
            {
                if ((Time.time - lastMouseDownTime) <= TimeAllowedAsClick)
                {
                    clickCount++;

                    if (clickCount == ClickNumber)
                    {
                        mouseClicked?.Invoke(Input.mousePosition);
                        if (ResetAfterComplete)
                        {
                            Reset();
                        }
                    }
                    lastMouseUpTime = Time.time;
                }
                else
                {
                    Reset();
                }
            }
        }

        void Reset()
        {
            clickCount = 0;
            lastMouseDownTime = 0;
            lastMouseUpTime = 0;
        }
    }
}
