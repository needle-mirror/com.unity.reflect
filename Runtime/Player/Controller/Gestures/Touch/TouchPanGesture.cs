using System;

namespace UnityEngine.Reflect.Controller.Gestures.Touch
{
    public class TouchPanGesture : TouchGesture
    {
        static readonly float k_ConstantMultiplier = 10;

        public event Action<Vector2> onPan;
        public event Action onPanStart;
        public event Action onPanEnd;

        public int FingersNumber { get; set; } = 1;
        public float DetectionThreshold { get; set; } = 0;
        public Vector2 Multiplier { get; set; } = Vector2.one;

        private Vector2 lastPosition;
        private bool detectionPending = false;
        private bool panPending = false;

        public TouchPanGesture(Action<Vector2> onPan)
        {
            this.onPan += onPan;
        }

        public TouchPanGesture()
        {
        }

        public override void Update()
        {
            if (Input.touchCount == FingersNumber)
            {
                var currentPosition = ComputeCentroid();

                if (!panPending)
                {
                    // Try to detect the pan gesture
                    if (!detectionPending)
                    {
                        detectionPending = true;
                        lastPosition = currentPosition;
                    }

                    var delta = FromPixels(currentPosition - lastPosition).magnitude;

                    if (delta >= DetectionThreshold)
                    {
                        onPanStart?.Invoke();
                        panPending = true;
                        lastPosition = currentPosition;
                    }

                }
                else
                {
                    // The pan is pending
                    var delta = FromPixels(currentPosition - lastPosition);
                    onPan?.Invoke(delta * Multiplier * Time.deltaTime * k_ConstantMultiplier);
                    lastPosition = currentPosition;
                }
            }
            else
            {
                // Reset
                if (panPending)
                    onPanEnd?.Invoke();
                panPending = false;
                detectionPending = false;
            }
        }
    }
}