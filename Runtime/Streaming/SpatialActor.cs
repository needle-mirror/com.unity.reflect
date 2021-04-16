using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect.Actor;
using Unity.Reflect.Collections;
using Unity.Reflect.Geometry;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class SpatialActor
    {
        static readonly int k_MinNbObjects = 100;

#pragma warning disable 649
        Settings m_Settings;
        
        NetComponent m_Net;
        DoubleExecutor m_DoubleExecutor;

        NetOutput<UpdateStreaming> m_UpdateStreamingOutput;
        NetOutput<UpdateVisibility> m_UpdateVisibilityOutput;
#pragma warning restore 649
        
        MpscSynchronizer m_BackgroundThreadSynchronizer = new MpscSynchronizer();
        MpscSynchronizer m_MainThreadSynchronizer = new MpscSynchronizer();

        List<ISpatialObject> m_PrevVisibilityResults = new List<ISpatialObject>();
        List<ISpatialObject> m_VisibilityResults = new List<ISpatialObject>();

        List<Guid> m_InstancesToAdd;
        List<Guid> m_InstancesToRemove;
        List<Guid> m_InstancesToShow;
        List<Guid> m_InstancesToHide;

        Bounds m_Bounds = new Bounds();
        Camera m_Camera;
        Transform m_CameraTransform;
        float m_VisibilitySqrDist, m_GlobalMaxSqrDistance;
        Vector3 m_CamPos, m_CamForward, m_BoundsSize, m_ObjDirection, m_GlobalBoundsSize;
        int m_NbLoadedGameObjects;

        Dictionary<Guid, SpatialObject> m_IdToSpatialObjects = new Dictionary<Guid, SpatialObject>();

        Func<ISpatialObject, bool> m_VisibilityPredicate;
        Func<ISpatialObject, float> m_VisibilityPrioritizer;

        bool m_ProjectLifecycleStarted;

        Ray m_SelectionRay;
        Ray[] m_SelectionRaySamplePoints;

        SpatialCulling m_Culling;
		
        ISpatialCollection<ISpatialObject> m_SpatialCollection;
        
        public void Inject()
        {
            m_Culling = new SpatialCulling(m_Settings);
            m_SpatialCollection = CreateDefaultSpatialCollection();

            m_VisibilityPredicate = DefaultVisibilityPredicate;
            m_VisibilityPrioritizer = DefaultVisibilityPrioritizer;

            InitCamera();

            m_DoubleExecutor.Actor = this;
            m_BackgroundThreadSynchronizer.Set();
        }

        public void Initialize()
        {
            if (m_ProjectLifecycleStarted)
                return;

            m_ProjectLifecycleStarted = true;

            CacheCameraData();
        }

        public void Shutdown()
        {
            if (!m_ProjectLifecycleStarted)
                return;

            m_ProjectLifecycleStarted = false;

            m_SpatialCollection.Dispose();
            
            if (m_CameraTransform != null && m_CameraTransform.gameObject)
                Object.Destroy(m_CameraTransform.gameObject);

            m_Camera = null;
            m_CameraTransform = null;
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            foreach(var entry in ctx.Data.Added)
            {
                var obj = new SpatialObject(entry.Id, entry.Spatial.Box);

                m_IdToSpatialObjects.Add(entry.Id, obj);
                Add(obj);
            }

            foreach(var entry in ctx.Data.Removed)
            {
                if (!m_IdToSpatialObjects.TryGetValue(entry.Id, out var obj))
                    return;
                
                m_IdToSpatialObjects.Remove(entry.Id);
                Remove(obj);
                obj.Dispose();
            }

            foreach(var entry in ctx.Data.Changed)
            {
                if (m_IdToSpatialObjects.TryGetValue(entry.OldInfo.Id, out var obj))
                {
                    m_IdToSpatialObjects.Remove(entry.OldInfo.Id);
                    Remove(obj);
                    obj.Dispose();
                }
                
                obj = new SpatialObject(entry.NewInfo.Id, entry.NewInfo.Spatial.Box);
                m_IdToSpatialObjects.Add(entry.NewInfo.Id, obj);
                Add(obj);
            }

            m_GlobalBoundsSize = m_SpatialCollection.Bounds.size;
            m_GlobalMaxSqrDistance = Mathf.Max(m_GlobalBoundsSize.x, m_GlobalBoundsSize.y, m_GlobalBoundsSize.z);
            m_GlobalMaxSqrDistance *= m_GlobalMaxSqrDistance;
        }

        [NetInput]
        void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            if (!m_Settings.IsActive)
                return;

            if (!m_IdToSpatialObjects.TryGetValue(ctx.Data.InstanceId, out var obj))
                return;

            obj.LoadedObject = ctx.Data.GameObject;
            ++m_NbLoadedGameObjects;
        }

        [RpcInput]
        void OnPickFromRay(RpcContext<PickFromRay> ctx)
        {
            var results = new List<ISpatialObject>();
            Pick(ctx.Data.Ray, results);
            ctx.SendSuccess(results);
        }

        [RpcInput]
        void OnPickFromSamplePoints(RpcContext<PickFromSamplePoints> ctx)
        {
            var results = new List<ISpatialObject>();
            Pick(ctx.Data.SamplePoints, ctx.Data.Count, results);
            ctx.SendSuccess(results);
        }

        [RpcInput]
        void OnPickFromDistance(RpcContext<PickFromDistance> ctx)
        {
            var results = new List<ISpatialObject>();
            Pick(ctx.Data.Distance, results);
            ctx.SendSuccess(results);
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            if (fieldName == nameof(Settings.PriorityWeightAngle))
                m_Settings.PriorityWeightAngle = (float)newValue;
            else if (fieldName == nameof(Settings.PriorityWeightDistance))
                m_Settings.PriorityWeightAngle = (float)newValue;
            else if (fieldName == nameof(Settings.PriorityWeightSize))
                m_Settings.PriorityWeightAngle = (float)newValue;
            else if (fieldName == nameof(Settings.UseCulling))
                m_Settings.UseCulling = (bool)newValue;
        }
        
        // TODO: OnGameObjectRemoved/Changed
        // case StreamEvent.Changed:
        // obj.LoadedObject = gameObject.data;
        // break;
        // case StreamEvent.Removed:
        // obj.LoadedObject = null;
        // --m_NbLoadedGameObjects;
        // break;

        void Add(ISpatialObject obj)
        {
            m_SpatialCollection.Add(obj);
        }

        void Remove(ISpatialObject obj)
        {
            m_SpatialCollection.Remove(obj);
        }

        public ISpatialCollection<ISpatialObject> CreateDefaultSpatialCollection()
        {
            return new RTree(m_Settings.MinPerNode, m_Settings.MaxPerNode);
        }

        
        TimeSpan m_LastEndTime = TimeSpan.FromTicks(0);
        async Task VisibilityTask(CancellationToken token)
        {
            await m_BackgroundThreadSynchronizer.WaitAsync(token);

            var currentTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            var remaining = TimeSpan.FromMilliseconds(40) - (currentTime - m_LastEndTime);
            if (remaining > TimeSpan.FromMilliseconds(1))
                await Task.Delay(remaining, token);

            // this is where the magic happens
            m_SpatialCollection.Search(m_VisibilityPredicate,
                m_VisibilityPrioritizer,
                m_Settings.VisibleObjectsMax,
                m_VisibilityResults);

            m_InstancesToAdd = new List<Guid>(m_VisibilityResults.Count);
            foreach (var obj in m_VisibilityResults)
                m_InstancesToAdd.Add(obj.Id);

            m_InstancesToShow = new List<Guid>();
            foreach (var obj in m_VisibilityResults.Except(m_PrevVisibilityResults))
            {
                if (obj.IsVisible) 
                    continue;
                
                m_InstancesToShow.Add(obj.Id);
                obj.IsVisible = true;
            }

            m_InstancesToHide = new List<Guid>();
            m_InstancesToRemove = new List<Guid>();
            foreach (var obj in m_PrevVisibilityResults.Except(m_VisibilityResults))
            {
                if (obj.IsVisible)
                {
                    m_InstancesToHide.Add(obj.Id);
                    obj.IsVisible = false;
                }
                // only unload when past the object limit
                if (m_NbLoadedGameObjects > m_Settings.VisibleObjectsMax)
                    m_InstancesToRemove.Add(obj.Id);
            }

            // save the results
            m_PrevVisibilityResults.Clear();
            m_PrevVisibilityResults.AddRange(m_VisibilityResults);

            m_MainThreadSynchronizer.Set();
            m_LastEndTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
        }

        bool DefaultVisibilityPredicate(ISpatialObject obj)
        {
            if (m_Settings.UseCulling)
                return m_Culling.IsVisible(obj);
            
            m_Bounds.SetMinMax(obj.Min, obj.Max);
            return m_Bounds.SqrDistance(m_CamPos) < m_VisibilitySqrDist;
        }

        float DefaultVisibilityPrioritizer(ISpatialObject obj)
        {
            m_Bounds.SetMinMax(obj.Min, obj.Max);
            m_BoundsSize = m_Bounds.size;
            m_ObjDirection = m_Bounds.ClosestPoint(m_CamPos) - m_CamPos;
            // lower priority is better so change sign after adding the weighted values
            return -1f *
                   // backward [0-1] forward
                   (m_Settings.PriorityWeightAngle * (1f - Vector3.Angle(m_CamForward, m_ObjDirection) / 180f)
                    // farther [0-1] closer
                    + m_Settings.PriorityWeightDistance * (1f - m_ObjDirection.sqrMagnitude / m_GlobalMaxSqrDistance)
                    // smaller [0-1] larger
                    + m_Settings.PriorityWeightSize * (m_BoundsSize.x + m_BoundsSize.y + m_BoundsSize.z) / (m_GlobalBoundsSize.x + m_GlobalBoundsSize.y + m_GlobalBoundsSize.z));
        }

        void InitCamera()
        {
            m_Camera = Camera.main;
            if (m_Camera == null)
            {
                Debug.LogError($"[{nameof(SpatialActor)}] active main camera not found!");
                return;
            }

            if (m_CameraTransform == null)
                m_CameraTransform = new GameObject("SpatialFilterCameraTracker").transform;

            m_CameraTransform.SetParent(m_Camera.transform, false);

            m_Culling.SetCamera(m_Camera);
        }

        void CacheCameraData()
        {
            if (m_Camera == null || !m_Camera.gameObject.activeInHierarchy)
                InitCamera();

            m_CamPos = m_CameraTransform.position;
            m_CamForward = m_CameraTransform.forward;
            m_VisibilitySqrDist = m_Camera.farClipPlane;
            m_VisibilitySqrDist *= m_VisibilitySqrDist;
        }

        void Pick(Ray ray, List<ISpatialObject> results)
        {
            m_SelectionRay = ray;

            m_SpatialCollection.Search(CheckIntersectRay,
                GetRayCastDistance,
                m_Settings.SelectedObjectsMax,
                results);
        }

        void Pick(int distance, List<ISpatialObject> results)
        {
            m_Settings.ColliderObjectsMaxDistance = distance;

            m_SpatialCollection.Search(CheckColliderDistance,
                GetRayCastDistance,
                k_MinNbObjects,
                results);

            for (int i = 0; i < results.Count; i++)
                results[i].IsVisible = true;
        }

        void Pick(Vector3[] samplePoints, int samplePointCount, List<ISpatialObject> results)
        {
            if (m_SelectionRaySamplePoints == null || m_SelectionRaySamplePoints.Length != samplePointCount - 1)
                m_SelectionRaySamplePoints = new Ray[samplePointCount - 1];

            for (var i = 0; i < m_SelectionRaySamplePoints.Length; ++i)
            {
                m_SelectionRaySamplePoints[i].origin = samplePoints[i];
                m_SelectionRaySamplePoints[i].direction = samplePoints[i + 1] - samplePoints[i];
            }

            m_SpatialCollection.Search(CheckIntersectRaySamplePoints,
                GetRayCastDistance,
                m_Settings.SelectedObjectsMax,
                results);
        }

        bool CheckIntersectRaySamplePoints(ISpatialObject obj)
        {
            m_Bounds.SetMinMax(obj.Min, obj.Max);
            for (var i = 0; i < m_SelectionRaySamplePoints.Length; ++i)
            {
                var ray = m_SelectionRaySamplePoints[i];
                if (!m_Bounds.IntersectRay(ray, out var distance))
                    continue;

                obj.Priority = distance;
                if (i > 0)
                    obj.Priority += Vector3.Distance(m_SelectionRaySamplePoints[0].origin, ray.origin);

                return true;
            }

            return false;
        }

        bool CheckIntersectRay(ISpatialObject obj)
        {
            m_Bounds.SetMinMax(obj.Min, obj.Max);
            if (!m_Bounds.IntersectRay(m_SelectionRay, out var distance))
                return false;

            obj.Priority = distance;
            return true;
        }

        bool CheckColliderDistance(ISpatialObject obj)
        {
            m_Bounds.SetMinMax(obj.Min, obj.Max);
            var distance = m_Bounds.SqrDistance(m_Camera.transform.position);
            if (distance > m_Settings.ColliderObjectsMaxDistance)
                return false;

            obj.Priority = distance;
            return true;
        }

        static float GetRayCastDistance(ISpatialObject obj)
        {
            return obj.Priority;
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            [Header("RTree")]
            public int MaxPerNode = 16;
            public int MinPerNode = 6;
            
            [Header("Culling")]
            [SerializeField, Transient(nameof(DirectionalLight))] ExposedReference<Transform> m_DirectionalLight;
            [HideInInspector] public Transform DirectionalLight;

            public RenderTexture DepthRenderTexture;
            public RenderTexture DepthRenderTextureNoAsync;
            public Material DepthRenderMaterial;
            public bool IsActive = true;
            
            [Header("Priority")]
            public float PriorityWeightAngle = 0.5f;
            public float PriorityWeightDistance = 10f;
            public float PriorityWeightSize = 5f;
            
            [Header("Visibility")]
            public int VisibleObjectsMax = 100000;
            
            [Header("Culling")]
            public bool UseCulling = true;
            public float CameraFrustumNormalOffset = 10f;

            [Space]
            public bool UseDistanceCullingAvoidance = true;
            public float AvoidCullingWithinDistance = 5f;

            [Space]
            public bool UseShadowCullingAvoidance = true;
            public float ShadowDistanceFactor = 3f;

            [Space]
            public bool UseSizeCulling = true;
            [Range(0, 10)] public float MinimumScreenAreaRatioMesh = 4;

            [Space]
            public bool UseDepthCulling = true;
            public float DepthOffset = 0.005f;
            
            [Header("Selection")]
            public int SelectedObjectsMax = 10;
            public int ColliderObjectsMaxDistance = 25;
            
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }

        class SpatialObject : ISpatialObject
        {
            public Guid Id { get; }
            public Vector3 Min { get; }
            public Vector3 Max { get; }
            public Vector3 Center { get; }
            public float Priority { get; set; }
            public bool IsVisible { get; set; }
            public GameObject LoadedObject { get; set; }

            public SpatialObject(Guid id, AABB box)
            {
                Id = id;
                
                Min = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
                Max = new Vector3(box.Max.X, box.Max.Y, box.Max.Z);
                Center = Min + (Max - Min) * 0.5f;
            }

            public void Dispose()
            {
                // nothing to do
            }
        }

        class DoubleExecutor : IAsyncComponent
        {
            public SpatialActor Actor;

            public async Task WaitAsync(CancellationToken token)
            {
                await Actor.VisibilityTask(token);
            }

            public bool Tick(TimeSpan endTime, CancellationToken token)
            {
                if (!Actor.m_Settings.IsActive)
                    return true;

                Actor.CacheCameraData();

                if (Actor.m_SpatialCollection.ObjectCount <= 0)
                    return true;

                if (Actor.m_Settings.UseCulling)
                    Actor.m_Culling.OnUpdate();

                if (!Actor.m_MainThreadSynchronizer.IsCurrentlySignaled())
                    return true;

                Actor.m_MainThreadSynchronizer.RemoveOneSignal();
                Actor.m_UpdateStreamingOutput.Send(new UpdateStreaming(Actor.m_InstancesToAdd, Actor.m_InstancesToRemove));
                Actor.m_UpdateVisibilityOutput.Send(new UpdateVisibility(Actor.m_InstancesToShow, Actor.m_InstancesToHide));
                Actor.m_BackgroundThreadSynchronizer.Set();

                return true;
            }
        }
    }
}
