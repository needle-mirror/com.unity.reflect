using System;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Geometry;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    [Actor("c76a2ba5-fd1c-465d-a9df-93fe861859e5", true)]
    public class CameraActor
    {
#pragma warning disable 649
        Settings m_Settings;
        
        NetComponent m_Net;
        NetOutput<CameraDataChanged> m_CameraDataChangedOutput;
#pragma warning restore 649

        Camera m_Camera;
        Transform m_CameraTransform;
        Bounds m_RootBounds;
        float m_DiagonalLength;
        Vector3[] m_CachedPoints = new Vector3[8];

        public void Initialize()
        {
            InitCamera();
            SendCameraData();
        }

        public void Shutdown()
        {
            m_Camera = null;
        }

        public TickResult Tick(TimeSpan endTime)
        {
            m_Net.Tick(TimeSpan.MaxValue);
            SendCameraData();
            return TickResult.Yield;
        }

        [EventInput]
        void OnRootBoundingBoxChanged(EventContext<GlobalBoundsUpdated> ctx)
        {
            UpdateBounds(ctx.Data.GlobalBounds);
        }

        [EventInput]
        void OnSceneZonesChanged(EventContext<SceneZonesChanged> ctx)
        {
            UpdateBounds(ctx.Data.Zones[0].Bounds.ToUnity());
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;
            
            if (fieldName == nameof(Settings.NearPlane))
                m_Settings.NearPlane = (float)newValue;
            else if (fieldName == nameof(Settings.FarPlane))
                m_Settings.FarPlane = (float)newValue;
            else if (fieldName == nameof(Settings.EnableDynamicFarPlane))
                m_Settings.EnableDynamicFarPlane = (bool)newValue;
        }

        void UpdateBounds(Bounds bb)
        {
            m_RootBounds = bb;
            var length3d = m_RootBounds.max - m_RootBounds.min;
            m_DiagonalLength = Mathf.Sqrt(length3d.x * length3d.x + length3d.y * length3d.y + length3d.z * length3d.z);
        }

        void InitCamera()
        {
            m_Camera = Camera.main;
            if (m_Camera != null) 
                return;

            Debug.LogError($"[{nameof(CameraActor)}] Active main camera not found!");
        }

        void SendCameraData()
        {
            if (m_Camera == null || !m_Camera.gameObject.activeInHierarchy)
                InitCamera();

            m_Camera.nearClipPlane = m_Settings.NearPlane;
            
            var transform = m_Camera.transform;
            if (m_Settings.EnableDynamicFarPlane)
            {
                if (m_DiagonalLength <= m_Camera.nearClipPlane)
                    m_DiagonalLength = Settings.k_DefaultFarPlane;

                var extents = m_RootBounds.extents;

                m_CachedPoints[0] = m_RootBounds.center + new Vector3(-extents.x, -extents.y, -extents.z);
                m_CachedPoints[1] = m_RootBounds.center + new Vector3(extents.x, -extents.y, -extents.z);
                m_CachedPoints[2] = m_RootBounds.center + new Vector3(extents.x, -extents.y, extents.z);
                m_CachedPoints[3] = m_RootBounds.center + new Vector3(-extents.x, -extents.y, extents.z);
                m_CachedPoints[4] = m_RootBounds.center + new Vector3(-extents.x, extents.y, -extents.z);
                m_CachedPoints[5] = m_RootBounds.center + new Vector3(extents.x, extents.y, -extents.z);
                m_CachedPoints[6] = m_RootBounds.center + new Vector3(extents.x, extents.y, extents.z);
                m_CachedPoints[7] = m_RootBounds.center + new Vector3(-extents.x, extents.y, extents.z);

                var transformedBounds = GeometryUtility.CalculateBounds(m_CachedPoints, m_Settings.Root.localToWorldMatrix);

                var pos = transform.position;
                if (transformedBounds.Contains(pos))
                    m_Camera.farClipPlane = m_DiagonalLength;
                else
                    m_Camera.farClipPlane = Vector3.Distance(transformedBounds.ClosestPoint(pos), pos) + m_DiagonalLength;
            }
            else
                m_Camera.farClipPlane = m_Settings.FarPlane;
            
            var msg = new CameraDataChanged(
                transform.position,
                m_Camera.nearClipPlane,
                m_Camera.farClipPlane,
                m_Camera.projectionMatrix * transform.worldToLocalMatrix, // Todo: RV-1292
                transform.forward,
                GeometryUtility.CalculateFrustumPlanes(m_Camera),
                m_Camera.fieldOfView, 
                Screen.height);

            m_CameraDataChangedOutput.Send(msg);
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public const float k_DefaultNearPlane = 0.05f;
            public const float k_DefaultFarPlane = 3000f;

            [SerializeField]
            [Transient(nameof(Root))]
            ExposedReference<Transform> m_Root;

            [HideInInspector]
            public Transform Root;

            public float NearPlane = k_DefaultNearPlane;
            public float FarPlane = k_DefaultFarPlane;

            public bool EnableDynamicFarPlane = true;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
