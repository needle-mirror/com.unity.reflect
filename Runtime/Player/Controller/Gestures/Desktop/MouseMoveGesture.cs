﻿using System;
using System.Linq;

namespace UnityEngine.Reflect.Controller.Gestures.Desktop
{
    public class MouseMoveGesture : IGesture
    {
        static readonly float k_ConstantMultiplier = 50;

        public event Action<Vector2> mouseMoved;
        public event Action startMove;
        public event Action endMove;

        public KeyCode[] NeededButtons { get; set; } = new KeyCode[0];
        public KeyCode[] ExcludedButtons { get; set; } = new KeyCode[0];
        public Vector2 Multiplier { get; set; } = Vector2.one;

        bool pending = false;

        public MouseMoveGesture(Action<Vector2> mouseMoved)
        {
            this.mouseMoved += mouseMoved;
        }

        public MouseMoveGesture()
        {
        }

        public void Update()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (CheckButtons())
            {
                if (!pending)
                    startMove?.Invoke();

                pending = true;

                var x = Input.GetAxis("Mouse X");
                var y = Input.GetAxis("Mouse Y");
                if (x != 0 || y != 0)
                    mouseMoved?.Invoke(new Vector2(x, y) * Multiplier * Time.deltaTime * k_ConstantMultiplier);
            }
            else if (pending)
            {
                pending = false;
                endMove?.Invoke();
            }
#endif
        }

        bool CheckButtons()
        {
            if (NeededButtons.Any(button => !Input.GetKey(button)))
                return false;

            if (ExcludedButtons.Any(button => Input.GetKey(button)))
                return false;

            return true;
        }
    }
}
