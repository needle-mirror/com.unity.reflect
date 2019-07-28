using UnityEngine.Reflect.Controller.Gestures;
using UnityEngine.Reflect.Controller.Gestures.Desktop;
using UnityEngine.Reflect.Controller.Gestures.Touch;

namespace UnityEngine.Reflect.Controller
{
    public class FreeCamController : Controller
    {

        static readonly float k_ElasticThreshold = .05f;
        static readonly float k_ElasticTime = .5f;
        static readonly float k_PanMagnitude = .1f;

        [Header("Input parameters")]
        public float DesktopScrollSensitivity = 5;
        public float DesktopAltZoomSensitivity = 1;
        public float DesktopPanSensitivity = 1;
        public float DesktopRotateAroundPivotSensitivity = 5;
        public float DesktopRotateCameraSensitivity = 5;
        public Vector2 DesktopMoveSensitivity = Vector2.one;
        public float TouchZoomSensitivity = 500;
        public float TouchZoomThreshold = .03f;
        public float TouchPanSensitivity = 200;
        public float TouchPanThreshold = .03f;
        public float TouchRotateSensitivity = 1500;

        float distanceToPivot = 10;
        Vector3 cameraRotationEuler;
        Vector3 pivotRotationEuler;

        bool elasticReturn = false;
        Vector3 elasticPanPoint;
        Vector3 elasticVelocity;

        public Vector3 Target
        {
            get
            {
                return transform.position + transform.forward * distanceToPivot;
            }
            set
            {
                UpdatePosition(value);
            }
        }

        public float DistanceToPivot
        {
            get
            {
                return distanceToPivot;
            }
            set
            {
                distanceToPivot = value;
                UpdatePosition(Target);
            }
        }

        void UpdatePosition(Vector3 target)
        {
            transform.position = target - transform.forward * distanceToPivot;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position + transform.forward * distanceToPivot, .1f);
        }

        protected override void StartController(GestureListener listener)
        {
            // Subscribe to desktop events
            var mouseZoom = new MouseScrollGesture(Zoom)
            {
                Multiplier = DesktopScrollSensitivity
            };
            var mouseAltZoom = new MouseMoveGesture(v => Zoom(v.x))
            {
                NeededButtons = new KeyCode[] {
                    KeyCode.LeftAlt,
                    KeyCode.Mouse1
                },
                Multiplier = Vector2.one * DesktopAltZoomSensitivity
            };
            var mousePan = new MouseMoveGesture(Pan)
            {
                NeededButtons = new KeyCode[] {
                    KeyCode.Mouse2
                },
                Multiplier = - Vector2.one * DesktopPanSensitivity
            };
            var mouseLeftClickRotate = new MouseMoveGesture(RotateAroundPivot)
            {
                NeededButtons = new KeyCode[] {
                    KeyCode.Mouse0
                },
                Multiplier = Vector2.one * DesktopRotateAroundPivotSensitivity
            };
            mouseLeftClickRotate.startMove += StartRotateAroundPivot;
            var mouseRotateCamera = new MouseMoveGesture(RotateCamera)
            {
                NeededButtons = new KeyCode[] {
                    KeyCode.Mouse1
                },
                ExcludedButtons = new KeyCode[] {
                    KeyCode.LeftAlt
                },
                Multiplier = Vector2.one * DesktopRotateCameraSensitivity,
            };
            mouseRotateCamera.startMove += StartRotateCamera;
            var moveCamera = new DirectionButtonsGesture(MoveCamera) {
                Multiplier = DesktopMoveSensitivity,
            };
            listener.AddListeners(mouseZoom, mouseAltZoom, mousePan, mouseLeftClickRotate, mouseRotateCamera, moveCamera);

            // Subscribe to touch events
            var touchZoom = new TouchPinchGesture(ZoomMobile)
            {
                Multiplier = TouchZoomSensitivity,
                DetectionThreshold = TouchZoomThreshold
            };
            var touchPan = new TouchPanGesture(Pan)
            {
                FingersNumber = 2,
                Multiplier = - Vector2.one * TouchPanSensitivity,
                DetectionThreshold = TouchPanThreshold
            };
            touchPan.onPanStart += StartElasticPan;
            touchPan.onPanEnd += StopElasticPan;
            var touchRotate = new TouchPanGesture(RotateAroundPivot)
            {
                Multiplier = Vector2.one * TouchRotateSensitivity
            };
            touchRotate.onPanStart += StartRotateAroundPivot;
            listener.AddListeners(touchZoom, touchPan, touchRotate);
        }

        void Zoom(float amount)
        {
            distanceToPivot = Mathf.Max(distanceToPivot - amount, 0);
            transform.position += transform.forward * amount;
        }

        void ZoomMobile(float amount)
        {
            var delta = Mathf.Min(distanceToPivot, amount);
            distanceToPivot -= delta;
            transform.position += transform.forward * delta;
        }

        void StartElasticPan()
        {
            if (!elasticReturn)
            {
                elasticPanPoint = Target;
                elasticReturn = false;
            }
        }

        void StopElasticPan()
        {
            elasticVelocity = Vector3.zero;
            elasticReturn = true;
        }

        void Pan(Vector2 delta)
        {
            var magnitude = (distanceToPivot + 1) * k_PanMagnitude;
            transform.position += magnitude * (transform.right * delta.x + transform.up * delta.y);
        }

        void StartRotateAroundPivot()
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, - transform.forward);
            pivotRotationEuler = NormalizeEulerAngles(rotation.eulerAngles);
        }

        void RotateAroundPivot(Vector2 delta)
        {
            var target = Target;
            pivotRotationEuler = ComputeNewEulerAngles(delta.x, delta.y, pivotRotationEuler);
            var rotation = Quaternion.Euler(pivotRotationEuler);
            transform.position = target + rotation * Vector3.forward * distanceToPivot;
            transform.LookAt(target);
        }

        void StartRotateCamera()
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, transform.forward);
            cameraRotationEuler = NormalizeEulerAngles(rotation.eulerAngles);
        }

        void RotateCamera(Vector2 delta)
        {
            cameraRotationEuler = ComputeNewEulerAngles(delta.x, -delta.y, cameraRotationEuler);
            var rotation = Quaternion.Euler(cameraRotationEuler);
            transform.forward = rotation * Vector3.forward;
        }

        void MoveCamera(Vector2 direction)
        {
            transform.position += transform.forward * direction.y + transform.right * direction.x;
        }

        protected override void UpdateController()
        {
            if (elasticReturn)
            {
                if ((Target - elasticPanPoint).magnitude > k_ElasticThreshold)
                {
                    Target = Vector3.SmoothDamp(Target, elasticPanPoint, ref elasticVelocity, k_ElasticTime);
                }
                else
                {
                    elasticReturn = false;
                    Target = elasticPanPoint;
                }
            }
        }
    }
}
