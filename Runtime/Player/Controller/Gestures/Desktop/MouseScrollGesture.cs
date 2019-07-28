using System;

namespace UnityEngine.Reflect.Controller.Gestures.Desktop
{
    public class MouseScrollGesture : IGesture
    {
        static readonly float k_ConstantMultiplier = 50;

        public event Action<float> mouseScrolled;

        public float Multiplier { get; set; } = 1;

        public MouseScrollGesture(Action<float> mouseScrolled)
        {
            this.mouseScrolled += mouseScrolled;
        }

        public MouseScrollGesture()
        {
        }

        public void Update()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            var scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (scrollWheel != 0)
            {
                mouseScrolled?.Invoke(scrollWheel * Multiplier * Time.deltaTime * k_ConstantMultiplier);
            }
#endif
        }
    }
}
