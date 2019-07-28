using System;

namespace UnityEngine.Reflect.Controller.Gestures.Touch
{
    public class TouchPinchGesture : TouchGesture
    {
        static readonly float k_ConstantMultiplier = 10;

        public event Action<float> onPinch;
        public event Action onPinchStart;
        public event Action onPinchEnd;

        public float Multiplier { get; set; } = 1;
        public float DetectionThreshold { get; set; } = 0;

        float lastDistance = -1;
        bool pinchPending = false;

        public TouchPinchGesture(Action<float> onPinch)
        {
            this.onPinch += onPinch;
        }

        public TouchPinchGesture()
        {
        }

        public override void Update()
        {
            if (Input.touchCount == 2)
            {

                var currentDistance = ComputeDistance();
                if (!pinchPending)
                {
                    // Try to detect the pinch gesture
                    if (lastDistance == -1)
                        lastDistance = currentDistance;

                    var delta = Mathf.Abs(currentDistance - lastDistance);

                    if (delta >= DetectionThreshold)
                    {
                        onPinchStart?.Invoke();
                        pinchPending = true;
                        lastDistance = currentDistance;
                    }
                }
                else
                {
                    // The pinch is pending
                    var delta = currentDistance - lastDistance;
                    onPinch?.Invoke(delta * Multiplier * Time.deltaTime * k_ConstantMultiplier);
                    lastDistance = currentDistance;
                }
            }
            else
            {
                // Reset
                if (pinchPending)
                    onPinchEnd?.Invoke();
                lastDistance = -1;
                pinchPending = false;
            }
        }

        float ComputeDistance()
        {
            var touches = Input.touches;
            var delta = touches[0].position - touches[1].position;
            return FromPixels(delta).magnitude;
        }
    }
}