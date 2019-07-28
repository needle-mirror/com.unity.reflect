using System.Collections.Generic;
using UnityEngine.Reflect.Controller.Gestures;
using UnityEngine.Reflect.Controller.Gestures.Desktop;
using UnityEngine.Reflect.Controller.Gestures.Touch;

namespace UnityEngine.Reflect.Controller
{
    public class ARController : Controller
    {
        public Transform syncRoot;

        [Header("Input Parameters")]
        public float DesktopScrollSensitivity = 1;
        public float DesktopRotateAroundPivotSensitivity = 10;
        public float TouchPinchSensitivity = 10f;
        public float TouchRotateAroundPivotSensitivity = 2000;

        Vector3 rotationPivot;

        protected override void StartController(GestureListener listener)
        {
            // Subscribe to desktop events
            var mouseZoom = new MouseScrollGesture(Scale) {
                Multiplier = DesktopScrollSensitivity
            };
            var mouseRotatePivot = new MouseMoveGesture(RotateAroundPivot) {
                NeededButtons = new KeyCode[] {
                    KeyCode.Mouse0
                },
                Multiplier = - Vector2.one * DesktopRotateAroundPivotSensitivity
            };
            listener.AddListeners(mouseZoom, mouseRotatePivot);

            // Subscribe to touch events
            var touchZoom = new TouchPinchGesture(Scale) {
                Multiplier = TouchPinchSensitivity,
            };
            var touchRotatePivot = new TouchPanGesture(RotateAroundPivot)
            {
                Multiplier = - Vector2.one * TouchRotateAroundPivotSensitivity
            };
            listener.AddListeners(touchZoom, touchRotatePivot);
        }

        void Scale(float amount)
        {
            var newScale = syncRoot.localScale + Vector3.one * amount;
            newScale = NegativeFilter(newScale);
            syncRoot.localScale = newScale;
        }

        Vector3 NegativeFilter(Vector3 value)
        {
            value.x = value.x < 0 ? 0 : value.x;
            value.y = value.y < 0 ? 0 : value.y;
            value.z = value.z < 0 ? 0 : value.z;
            return value;
        }

        void RotateAroundPivot(Vector2 delta)
        {
            syncRoot.Rotate(Vector3.up, delta.x, Space.World);
        }
    }
}
