using System;
using System.Linq;

namespace UnityEngine.Reflect.Controller.Gestures.Touch
{
    public class TouchMultiTapGesture : TouchGesture
    {
        public event Action<Vector2> onTap;

        public int FingersNumber { get; set; } = 1;
        public int TapNumber { get; set; } = 1;
        public float TimeAllowedBetweenTaps { get; set; } = 1;
        public float TimeAllowedAsTap { get; set; } = .5f;
        public float AllowedDistanceDuringTaps { get; set; } = 50;
        public float AllowedDistanceBetweenTaps { get; set; } = 100;
        public bool ResetAfterComplete { get; set; } = true;

        int tapCounter;
        bool tapPending;
        float lastKeyTime;
        Vector2 tapStartPosition = Vector2.zero;
        Vector2 lastTapPosition = Vector2.zero;

        public TouchMultiTapGesture(Action<Vector2> onTap)
        {
            this.onTap += onTap;
        }

        public TouchMultiTapGesture()
        {
        }

        public override void Update()
        {
            int touchCount = Input.touchCount;
            if (touchCount == FingersNumber)
            {
                var tapPosition = ComputeCentroid();
                if (!tapPending && HasTapStarted())
                {
                    if ((tapPosition - lastTapPosition).magnitude > AllowedDistanceBetweenTaps)
                    {
                        Reset();
                    }
                    tapPending = true;
                    lastKeyTime = Time.time;
                    tapStartPosition = tapPosition;
                }
                else if (tapPending && HasTapEnded())
                {
                    tapPending = false;
                    lastKeyTime = Time.time;
                    lastTapPosition = tapPosition;
                    tapCounter++;

                    if ((tapPosition - tapStartPosition).magnitude > AllowedDistanceDuringTaps)
                    {
                        Reset();
                    }
                    else if (tapCounter == TapNumber)
                    {
                        onTap?.Invoke(tapPosition);

                        if (ResetAfterComplete)
                        {
                            Reset();
                        }
                    }
                }
                else if (tapPending && (Time.time - lastKeyTime) > TimeAllowedAsTap)
                {
                    Reset();
                }
            }
            else if (touchCount == 0)
            {
                if (!tapPending && tapCounter > 0 && (Time.time - lastKeyTime) > TimeAllowedBetweenTaps)
                {
                    Reset();
                }
            }
            else if (touchCount > FingersNumber)
            {
                Reset();
            }
        }

        bool HasTapStarted()
        {
            return Input.touches.Any(x => x.phase == TouchPhase.Began);
        }

        bool HasTapEnded()
        {
            return Input.touches.Any(x => x.phase == TouchPhase.Ended);
        }

        void Reset()
        {
            tapPending = false;
            tapCounter = 0;
            lastKeyTime = 0;
            tapStartPosition = Vector2.zero;
            lastTapPosition = Vector2.zero;
        }
    }
}