using System;
using System.Linq;

namespace UnityEngine.Reflect.Controller.Gestures.Desktop
{
    public class DirectionButtonsGesture : IGesture
    {
        static readonly float k_ConstantMultiplier = 10;

        public event Action<Vector2> directionGiven;

        public KeyCode[] ForwardButtons { get; set; } = new KeyCode[] { KeyCode.W, KeyCode.UpArrow };
        public KeyCode[] BackwardButtons { get; set; } = new KeyCode[] { KeyCode.S, KeyCode.DownArrow };
        public KeyCode[] LeftButtons { get; set; } = new KeyCode[] { KeyCode.A, KeyCode.LeftArrow };
        public KeyCode[] RightButtons { get; set; } = new KeyCode[] { KeyCode.D, KeyCode.RightArrow };
        public KeyCode[] NeededButtons { get; set; } = new KeyCode[0];
        public KeyCode[] ExcludedButtons { get; set; } = new KeyCode[0];
        public Vector2 Multiplier { get; set; } = Vector2.one;

        public DirectionButtonsGesture(Action<Vector2> directionGiven)
        {
            this.directionGiven += directionGiven;
        }

        public DirectionButtonsGesture()
        {
        }

        public void Update()
        {
            foreach (var button in NeededButtons)
            {
                if (!Input.GetKey(button))
                    return;
            }

            foreach (var button in ExcludedButtons)
            {
                if (Input.GetKey(button))
                    return;
            }

            var direction = Vector2.zero;
            if (DirectionPushed(ForwardButtons))
                direction.y += 1;
            if (DirectionPushed(BackwardButtons))
                direction.y -= 1;
            if (DirectionPushed(LeftButtons))
                direction.x -= 1;
            if (DirectionPushed(RightButtons))
                direction.x += 1;

            if (direction != Vector2.zero)
                directionGiven?.Invoke(direction * Multiplier * Time.deltaTime * k_ConstantMultiplier);
        }

        bool DirectionPushed(KeyCode[] directionButtons)
        {
            return directionButtons.Any(button => Input.GetKey(button));
        }
    }
}
