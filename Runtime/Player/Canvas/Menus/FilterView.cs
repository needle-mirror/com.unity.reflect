
namespace UnityEngine.Reflect
{
    public class FilterView
    {
        Bounds m_Bounds;

        bool m_Aim;

        Camera m_Cam;
        Quaternion m_CameraRotation;
        Vector3 m_CameraOriginalPosition;
        Quaternion m_CameraOriginalRotation;

        public void AddRenderer(Renderer inRenderer)
        {
            if (m_Bounds.size == Vector3.zero)
            {
                m_Bounds = inRenderer.bounds;
            }
            else
            {
                m_Bounds.Encapsulate(inRenderer.bounds);
            }
        }

        public void SetCamera(Camera inCamera, Vector3 inOriginalPosition, Quaternion inOriginalRotation)
        {
            if (inCamera == null)
            {
                if (m_Aim && (m_Cam != null))
                {
                    Restore();
                }
            }
            else
            {
                //  backup current camera
                m_CameraOriginalPosition = inOriginalPosition;
                m_CameraOriginalRotation = inOriginalRotation;
            }

            m_Cam = inCamera;
        }

        public void Aim(bool inAim)
        {
            m_Aim = inAim;
        }

        public bool IsAiming()
        {
            return m_Aim;
        }

        public Vector3 GetCameraOriginalPosition()
        {
            return m_CameraOriginalPosition;
        }

        public Quaternion GetCameraOriginalRotation()
        {
            return m_CameraOriginalRotation;
        }

        void Restore()
        {
            m_Cam.transform.position = m_CameraOriginalPosition;
            m_Cam.transform.rotation = m_CameraOriginalRotation;
        }
    }
}
