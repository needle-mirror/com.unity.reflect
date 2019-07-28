using UnityEngine.Reflect.Controller.Gestures;
using UnityEngine.Reflect.Controller.Gestures.Desktop;
using UnityEngine.Reflect.Controller.Gestures.Touch;

namespace UnityEngine.Reflect.Controller
{
    [RequireComponent(typeof(Camera))]
    public class FirstPersonController : Controller
    {
        public float reticleSize = 3f;

        [Header("Input Parameters")]
        public float DesktopRotateSensitivity = 5;
        public Vector2 DesktopMoveSensitivity = Vector2.one;

        Vector3 cameraEuler;

        protected override void StartController(GestureListener listener)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            cameraEuler = transform.eulerAngles;
            cameraEuler.z = 0;

            // Subscribe to desktop events
            var mouseRotateCamera = new MouseMoveGesture(RotateCamera) {
                Multiplier = - Vector2.one * DesktopRotateSensitivity
            };
            var moveCamera = new DirectionButtonsGesture(MoveCamera) {
                Multiplier = DesktopMoveSensitivity
            };
            var mouseClick = new MouseClickGesture(SelectObject);
            listener.AddListeners(mouseRotateCamera, moveCamera, mouseClick);
        }

        protected override void DestroyController()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void RotateCamera(Vector2 delta)
        {
            cameraEuler = ComputeNewEulerAngles(- delta.x, delta.y, cameraEuler);
            transform.eulerAngles = cameraEuler;
        }

        void MoveCamera(Vector2 direction)
        {
            transform.position += transform.forward * direction.y + transform.right * direction.x;
        }

        void SelectObject(Vector2 screenPosition)
        {
            var cam = transform.GetComponent<Camera>();
            var ray = cam.ViewportPointToRay(new Vector3(screenPosition.x / Screen.width, screenPosition.y / Screen.height, 0));

            if (Physics.Raycast(ray, out var hit))
            {
                Debug.Log(hit.collider.gameObject.name);
            }
        }

        void OnGUI()
        {
            GUI.DrawTexture(new Rect(Screen.width * 0.5f, Screen.height * 0.5f, reticleSize, reticleSize), Texture2D.whiteTexture);
        }
    }
}
