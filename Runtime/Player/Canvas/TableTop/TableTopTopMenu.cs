using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Reflect.Controller;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.Reflect
{
    public class TableTopTopMenu : TopMenu
    {
        public GameObject screenMode;
        public GameObject arMode;
        public ARSessionOrigin sessionOrigin;
        public Camera arCamera;
        public FreeCamController freeCameraController;
        public GameObject target;
        public Transform syncRoot;
        public SyncManager syncManager;
        public float m_DefaultDistanceToPivot = 50;

        ARPlaneManager planeManager;
        ARRaycastManager m_RayCastManager;
        ARPointCloudManager pointCloudManager;

        Vector3 originalPosition = Vector3.zero;
        Quaternion originalRotation = Quaternion.identity;
        Vector3 originalScale = Vector3.one;

        protected override void Awake()
        {
            base.Awake();
            planeManager = sessionOrigin.GetComponent<ARPlaneManager>();
            pointCloudManager = sessionOrigin.GetComponent<ARPointCloudManager>();
            m_RayCastManager = sessionOrigin.GetComponent<ARRaycastManager>();
        }

        protected override void Start()
        {
            base.Start();

            LeaveAR();
        }

        void OnEnable()
        {
            syncManager.onProjectOpened += ProjectOpened;
        }

        void OnDisable()
        {
            syncManager.onProjectOpened -= ProjectOpened;
        }

        void SetFreeCameraTarget()
        {
            if (freeCameraController != null)
            {
                freeCameraController.Target = syncRoot.position;
                freeCameraController.DistanceToPivot = m_DefaultDistanceToPivot;
            }
        }

        public void ProjectOpened()
        {
            SetFreeCameraTarget();
        }

        public void EnterAR()
        {
            arMode.gameObject.SetActive(true);

            if (syncRoot != null)
            {
                var rootTrans = syncRoot.transform;
                originalPosition = rootTrans.position;
                originalRotation = rootTrans.rotation;
                originalScale = rootTrans.localScale;
            }
            
            screenMode.gameObject.SetActive(false);
        }

        public void LeaveAR()
        {
            arMode.gameObject.SetActive(false);

            if (syncRoot != null)
            {
                var rootTrans = syncRoot.transform;
                rootTrans.position = originalPosition;
                rootTrans.rotation = originalRotation;
                rootTrans.localScale = originalScale;
            }
            SetFreeCameraTarget();
            
            screenMode.gameObject.SetActive(true);
        }

        public override void Activate()
        {
            base.Activate();

            EnterAR();

            ShowModel(false);
            ShowPointCloud(true);
        }

        private void Update()
        {
            if ((target != null) && (planeManager != null) && (m_RayCastManager != null) && (arCamera != null) && ui.gameObject.activeSelf)
            {
                //  move target to nearest plane
                var cameraRay = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                Debug.DrawRay(arCamera.transform.position, Vector3.forward, Color.green);
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                if (m_RayCastManager.Raycast(cameraRay, hits, TrackableType.Planes))
                {
                    var hitPose = hits[0].pose;
                    target.transform.position = hitPose.position;
                    target.transform.rotation = hitPose.rotation;
                    target.SetActive(true);
                }
                else
                {
                    target.SetActive(false);
                }
            }
            else
            {
                target.SetActive(false);
            }
        }

        public void OnOK()
        {
            Deactivate();

            if (target.gameObject.activeSelf)
            {
                MoveModel(target.transform.position, target.transform.rotation);
                target.gameObject.SetActive(false);
            }
            else
            {
                LeaveAR();
            }
            ShowModel(true);
            ShowPointCloud(false);
        }

        public void OnCancel()
        {
            Deactivate();
            LeaveAR();

            ShowModel(true);
        }

        public void MoveModel(Vector3 position, Quaternion rotation)
        {
            if (syncRoot != null)
            {
                syncRoot.transform.position = position;
                syncRoot.transform.rotation = rotation;
                syncRoot.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            }
        }

        public void ShowModel(bool show)
        {
            if (syncRoot != null)
            {
                syncRoot.gameObject.SetActive(show);
                ARController controller = arCamera.GetComponent<ARController>();
                if (controller != null)
                {
                    controller.syncRoot = syncRoot;
                }
            }
        }

        void ShowPointCloud(bool show)
        {
            pointCloudManager.enabled = show;
//            ARPointCloud pointCloud = pointCloudManager.pointCloud;
//            if (pointCloud != null)
            {
//                pointCloud.gameObject.SetActive(show);
            }
        }
    }
}
