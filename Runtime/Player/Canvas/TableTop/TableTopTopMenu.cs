using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.XR;
using UnityEngine.Reflect.Controller;
using UnityEngine.Reflect.Services;

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
        ARPointCloudManager pointCloudManager;

        Vector3 originalPosition;
        Quaternion originalRotation;
        Vector3 originalScale;

        protected override void Awake()
        {
            base.Awake();
            planeManager = sessionOrigin.GetComponent<ARPlaneManager>();
            pointCloudManager = sessionOrigin.GetComponent<ARPointCloudManager>();
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
            screenMode.gameObject.SetActive(false);

            if (syncRoot != null)
            {
                originalPosition = syncRoot.transform.position;
                originalRotation = syncRoot.transform.rotation;
                originalScale = syncRoot.transform.localScale;
            }
        }

        public void LeaveAR()
        {
            arMode.gameObject.SetActive(false);
            screenMode.gameObject.SetActive(true);

            if (syncRoot != null)
            {
                syncRoot.transform.position = originalPosition;
                syncRoot.transform.rotation = originalRotation;
                syncRoot.transform.localScale = originalScale;
            }
            SetFreeCameraTarget();
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
            if ((target != null) && (planeManager != null) && (arCamera != null) && ui.gameObject.activeSelf)
            {
                //  move target to nearest plane
                Ray cameraRay = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                Debug.DrawRay(arCamera.transform.position, Vector3.forward, Color.green);
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                if (sessionOrigin.Raycast(cameraRay, hits, TrackableType.PlaneWithinPolygon))
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
            ARPointCloud pointCloud = pointCloudManager.pointCloud;
            if (pointCloud != null)
            {
                pointCloud.gameObject.SetActive(show);
            }
        }
    }
}
