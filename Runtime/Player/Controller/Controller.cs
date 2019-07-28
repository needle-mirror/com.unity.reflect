using UnityEditor;
using UnityEngine.EventSystems;
using UnityEngine.Reflect.Controller.Gestures;

namespace UnityEngine.Reflect.Controller
{
    public abstract class Controller : MonoBehaviour
    {
        static readonly float k_XAxisMaxValue = 89;
        const float k_MaxAngle = 360;
        const float k_HalfAngle = 180;
        const float k_QuarterAngle = 90;

        GestureListener gestureListener = new GestureListener();
         
        bool m_IsBlocked;

        protected void OnEnable()
        {
            ResetListener();
        }

#if UNITY_EDITOR
        protected void OnValidate()
        {
            // Hack to detect input parameters changes.
            if (EditorApplication.isPlaying && gameObject.activeInHierarchy)
            {
                ResetListener();
            }
        }
#endif

        protected void OnDisable()
        {
            gestureListener.Clear();
            DestroyController();
        }

        protected void Update()
        {
            if (IsBlocked())
                return;
            
            gestureListener.Update();
            UpdateController();
        }

        void ResetListener()
        {
            gestureListener.Clear();
            StartController(gestureListener);
        }

        protected virtual void StartController(GestureListener listener)
        {
        }

        protected virtual void DestroyController()
        {
        }

        protected virtual void UpdateController()
        {
        }

        protected static Vector3 ComputeNewEulerAngles(float x, float y, Vector3 euler)
        {
            euler.x += y;
            euler.y += x;

            euler.x = Mathf.Min(Mathf.Max(euler.x, -k_XAxisMaxValue), k_XAxisMaxValue);
            euler.y = euler.y % k_MaxAngle;

            return euler;
        }

        protected static Vector3 NormalizeEulerAngles(Vector3 euler)
        {
            euler.x = euler.x % k_MaxAngle;

            // Clamp X between -180 and 180 degrees
            if (euler.x > k_HalfAngle)
                euler.x -= k_MaxAngle;

            // Clamp X between -90 and 90 degrees
            var abs = Mathf.Abs(euler.x);
            if (abs > k_QuarterAngle)
            {
                euler.y += k_HalfAngle;
                euler.x = Mathf.Sign(euler.x) * (k_HalfAngle - abs);
            }

            euler.z = 0;
            return euler;
        }

        bool IsBlocked()
        {
            var id = -1;
            var pressed = false;
            var scrolled = false;

            for (var i = 0; i < Input.touchCount; ++i)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                {
                    id = Input.GetTouch(i).fingerId;
                    pressed = true;
                    break;
                }
            }

            if (!pressed)
            {
                pressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            }

            if (!pressed)
            {
                scrolled = Input.mouseScrollDelta.x > 0.0f || Input.mouseScrollDelta.y > 0.0f;
            }

            if (pressed || scrolled)
            {
                m_IsBlocked = EventSystem.current.IsPointerOverGameObject(id);
            }

            return m_IsBlocked;
        }
    }
}
