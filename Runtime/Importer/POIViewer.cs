using System;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class POIViewer : MonoBehaviour // TODO does not need to be MonoBehaviour
    {
        public Camera cameraToUse;
        public bool preserveAspect = true; // TODO UpdatePOI if changed

        POI[] m_POIs;
        int m_CurrentIndex = -1;

        void OnEnable()
        {
            m_POIs = FindObjectsOfType<POI>();

            if (cameraToUse == null)
                cameraToUse = Camera.main;

            Camera.onPreRender += OnCameraPreRender;
        }

        void OnDisable()
        {
            Camera.onPreRender -= OnCameraPreRender;
        }

        void OnCameraPreRender(Camera cam)
        {
            if (cam != cameraToUse)
                return;

            if (m_CurrentIndex == -1)
                return;

            GL.Clear(true, true, Color.black);
        }

        void OnGUI()
        {
            var r = new Rect(20, 20, 300, 20);

            var poi = m_CurrentIndex == -1 ? null : m_POIs[m_CurrentIndex];

            if (poi != null)
            {
                var label = poi.orthographic ? poi.label + " (Ortho)" : poi.label;
                GUI.Label(r, label);
            }

            r.y += 30;
            r.width = 100.0f;
            if (GUI.Button(r, "Next"))
                NextPOI();

            r.x += r.width + 20.0f;
            if (GUI.Button(r, "Prev"))
                PreviousPOI();

            r.x += r.width + 100.0f;
            if (GUI.Button(r, "Preserve Aspect"))
                TogglePreserveAspect();

            r = new Rect(20, 100, 300, 20);
            if (GUI.Button(r, "Refresh"))
                ApplyCurrentPOI();
        }

        void NextPOI()
        {
            m_CurrentIndex = (m_CurrentIndex + 1) % m_POIs.Length;
            ApplyCurrentPOI();
        }

        void PreviousPOI()
        {
            m_CurrentIndex = (m_POIs.Length + m_CurrentIndex - 1) % m_POIs.Length;
            ApplyCurrentPOI();
        }

        void TogglePreserveAspect()
        {
            preserveAspect = !preserveAspect;
            ApplyCurrentPOI();
        }

        void ApplyCurrentPOI()
        {
            ApplyToCamera(m_POIs[m_CurrentIndex], cameraToUse, preserveAspect);
        }

        public static void ApplyToCamera(POI poi, Camera cam, bool preserveAspect)
        {
            cam.transform.SetPositionAndRotation(poi.transform.position, poi.transform.rotation);

            cam.ResetProjectionMatrix();
            cam.ResetAspect();
            cam.orthographic = poi.orthographic;

            var sh = (float)Screen.height;
            var sw = (float)Screen.width;

            var screenAspect = sh / sw;
            var rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);

            // Custom Camera (orthographic size or fov)
            if (!poi.aspect.Equals(0.0f)) // TODO More robust way to determine custom vs standard projection?
            {
                if (poi.orthographic)
                {
                    cam.orthographicSize = poi.size;
                }
                else
                {
                    cam.fieldOfView = poi.fov;
                }


                if (preserveAspect)
                {
                    rect = ComputeCameraRectInsideScreen(screenAspect, poi.aspect);
                }

                cam.rect = rect;

                return;
            }

            // Custom Frustum

            var l = poi.left;
            var r = poi.right;
            var b = poi.bottom;
            var t = poi.top;

            var h = poi.top - poi.bottom;
            var w = poi.right - poi.left;

            var poiAspect = h / w;

            if (preserveAspect)
            {
                rect = ComputeCameraRectInsideScreen(screenAspect, poiAspect);
            }
            else
            {
                if (screenAspect > poiAspect)
                {
                    var newH = w * screenAspect;
                    var e = (newH - h) * 0.5f;
                    b -= e;
                    t += e;
                }
                else if (screenAspect < poiAspect)
                {
                    var newW = h / screenAspect;
                    var e = (newW - w) * 0.5f;
                    l -= e;
                    r += e;
                }
            }

            cam.orthographicSize = 1.0f; // Force size to 1.0f when using custom Frustum
            cam.projectionMatrix = poi.orthographic
                ? Matrix4x4.Ortho(l, r, b, t, poi.near, poi.far)
                : Matrix4x4.Frustum(l, r, b, t, poi.near, poi.far);
            cam.rect = rect;
        }

        static Rect ComputeCameraRectInsideScreen(float screenAspect, float rectAspect)
        {
            var rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);

            if (screenAspect > rectAspect)
            {
                rect.width = 1.0f;
                rect.height = rectAspect / screenAspect;
                rect.y = (1.0f - rect.height) * 0.5f;
            }
            else if (screenAspect < rectAspect)
            {
                rect.width = screenAspect / rectAspect;
                rect.height = 1.0f;
                rect.x = (1.0f - rect.width) * 0.5f;
            }

            return rect;
        }
    }
}
