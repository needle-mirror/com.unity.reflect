using System;
using UnityEngine;

namespace UnityEngine.Reflect.Controller.Gestures.Desktop
{
    public class MouseScrollGesture : IGesture
    {
        static readonly float k_ConstantMultiplier = 50;

        public event Action<float> mouseScrolled;

        public float Multiplier { get; set; } = 1;

        Rect screenSize = new Rect(0, 0, 1, 1);

        public MouseScrollGesture(Action<float> mouseScrolled)
        {
            this.mouseScrolled += mouseScrolled;
        }

        public MouseScrollGesture()
        {
        }

        public void Update()
        {
            // bypass if mouse is outside the window
            screenSize.width = Screen.width;
            screenSize.height = Screen.height;
            if (!screenSize.Contains(Input.mousePosition))
                return;

            var scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (scrollWheel != 0)
            {
                mouseScrolled?.Invoke(scrollWheel * Multiplier * Time.deltaTime * k_ConstantMultiplier);
            }
        }
    }
}