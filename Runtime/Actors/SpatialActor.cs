using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Collections;
using Unity.Reflect.Geometry;
using UnityEngine;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    [Actor("a3331352-8c23-4322-8986-ae0479087d0e")]
    public class SpatialActor
    {
        public const string k_IsDisabledFlag = "IsDisabled";
        public const string k_IsHlodFlag = "IsHlod";
        static readonly int k_MinNbObjects = 100;

#pragma warning disable 649
        Settings m_Settings;
        
        NetComponent m_Net;
        TimerComponent m_Timer;

        NetOutput<UpdateVisibility> m_UpdateVisibilityOutput;
        NetOutput<PreShutdown> m_PreShutdownOutput;
        RpcOutput<DelegateJob> m_DelegateJobOutput;
#pragma warning restore 649

        ActorHandle m_Self;

        List<ISpatialObject> m_PrevVisibilityResults = new List<ISpatialObject>();
        List<ISpatialObject> m_VisibilityResults = new List<ISpatialObject>();

        List<DynamicGuid> m_InstancesToShow;
        List<DynamicGuid> m_InstancesToHide;

        Bounds m_Bounds;
        CameraDataChanged m_LastCameraData;
        CameraDataChanged m_CameraData;
        Matrix4x4 m_RootMatrix;
        float m_GlobalMaxSqrDistance;
        Vector3 m_GlobalBoundsSize;

        Dictionary<DynamicGuid, SpatialObject> m_IdToSpatialObjects = new Dictionary<DynamicGuid, SpatialObject>();

        HashSet<string> m_VisibilityIgnoreFlags = new HashSet<string>() { }; 
        Predicate<ISpatialObject> m_VisibilityPredicate;
        Func<ISpatialObject, float> m_VisibilityPrioritizer;
        
        bool m_IsPreShutdown;
        bool m_IsFirstCameraData = true;
        bool m_IsDirty;

        SpatialCulling m_Culling;
		
        ISpatialCollection<ISpatialObject> m_SpatialCollection;
        
        public void Initialize(ActorHandle self)
        {
            m_Self = self;

            m_Culling = new SpatialCulling(m_Settings);
            m_SpatialCollection = CreateDefaultSpatialCollection();

            m_VisibilityPredicate = DefaultVisibilityPredicate;
            m_VisibilityPrioritizer = DefaultVisibilityPrioritizer;

            m_Net.Register<RunVisibilityUpdate>(OnUpdateVisibility);
        }

        public void Shutdown()
        {
            m_Culling.Shutdown();
            m_SpatialCollection.Dispose();
        }
        
        public TickResult Tick(TimeSpan endTime)
        {
            var a = m_Timer.Tick(endTime);
            var b = m_Net.Tick(endTime);
            return a == TickResult.Yield || b == TickResult.Yield ? TickResult.Yield : TickResult.Wait;
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_IsPreShutdown = true;
            m_PreShutdownOutput.Send(new PreShutdown());
        }

        [NetInput]
        void OnTransformObjectBounds(NetContext<TransformObjectBounds> ctx)
        {
            for (var i = 0; i < ctx.Data.Ids.Count; ++i)
            {
                if (m_IdToSpatialObjects.TryGetValue(ctx.Data.Ids[i], out var obj))
                {
                    Remove(obj);
                    obj.UpdateBounds(ctx.Data.TransformMatrix);
                    Add(obj);
                }
            }
            
            m_GlobalBoundsSize = m_SpatialCollection.Bounds.size;
            m_GlobalMaxSqrDistance = Mathf.Max(m_GlobalBoundsSize.x, m_GlobalBoundsSize.y, m_GlobalBoundsSize.z);
            m_GlobalMaxSqrDistance *= m_GlobalMaxSqrDistance;
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            foreach(var entry in ctx.Data.Delta.Added)
            {
                var obj = new SpatialObject(entry.Id, entry.Data);

                m_IdToSpatialObjects.Add(entry.Id, obj);
                Add(obj);
            }

            foreach(var entry in ctx.Data.Delta.Removed)
            {
                if (!m_IdToSpatialObjects.TryGetValue(entry.Id, out var obj))
                    return;
                
                m_IdToSpatialObjects.Remove(entry.Id);
                Remove(obj);
                obj.Dispose();
            }

            foreach(var entry in ctx.Data.Delta.Changed)
            {
                if (m_IdToSpatialObjects.TryGetValue(entry.Prev.Id, out var obj))
                {
                    m_IdToSpatialObjects.Remove(entry.Prev.Id);
                    Remove(obj);
                    obj.Dispose();
                }
                
                obj = new SpatialObject(entry.Next.Id, entry.Next.Data);
                m_IdToSpatialObjects.Add(entry.Next.Id, obj);
                Add(obj);
            }

            m_GlobalBoundsSize = m_SpatialCollection.Bounds.size;
            m_GlobalMaxSqrDistance = Mathf.Max(m_GlobalBoundsSize.x, m_GlobalBoundsSize.y, m_GlobalBoundsSize.z);
            m_GlobalMaxSqrDistance *= m_GlobalMaxSqrDistance;

            m_IsDirty = true;
        }
        
        [NetInput]
        void AddVisibilityIgnoredFlag(NetContext<AddVisibilityIgnoreFlag> ctx)
        {
            if (ctx.Data.FlagIds == null || ctx.Data.FlagIds.Length == 0)
                return;
            for (int i = 0; i < ctx.Data.FlagIds.Length; i++)
            {
                m_VisibilityIgnoreFlags.Add(ctx.Data.FlagIds[i]);
            }
            
            m_IsDirty = true;
        }

        [NetInput]
        void RemoveVisibilityIgnoredFlag(NetContext<RemoveVisibilityIgnoreFlag> ctx)
        {
            if (ctx.Data.FlagIds == null || ctx.Data.FlagIds.Length == 0)
                return;
            for (int i = 0; i < ctx.Data.FlagIds.Length; i++)
            {
                m_VisibilityIgnoreFlags.Remove(ctx.Data.FlagIds[i]);
            }
            
            m_IsDirty = true;
        }
        
        [NetInput]
        void AddSpatialObjectFlag(NetContext<AddSpatialObjectFlag> ctx)
        {
            SetSpatialFlag(ctx.Data.FlagId, ctx.Data.Objects, false);
        }

        [NetInput]
        void RemoveSpatialObjectFlag(NetContext<RemoveSpatialObjectFlag> ctx)
        {
            SetSpatialFlag(ctx.Data.FlagId, ctx.Data.Objects, true);
        }

        private void SetSpatialFlag(string flagId, IEnumerable<DynamicGuid> ids, bool remove)
        {
            if (flagId == null)
                return;

            if(remove)
            {
                foreach (var id in ids)
                {   
                    if (m_IdToSpatialObjects.TryGetValue(id, out var obj))
                    {
                        obj.Flags.Remove(flagId);
                    }
                }
            }
            else
            {                
                foreach (var id in ids)
                {   
                    if (m_IdToSpatialObjects.TryGetValue(id, out var obj))
                    {
                        obj.Flags.Add(flagId);
                    }
                }
            }
        }

        [RpcInput]
        void OnPick(RpcContext<SpatialPickingArguments> ctx)
        {
            ctx.SendSuccess(Pick(ctx.Data.ExcludedFlags, ctx.Data.GetDistance, ctx.Data.CheckIntersection));
        }

        List<ISpatialObject> Pick(ISet<string> excludeFlags, Func<ISpatialObject, float> distanceFunction, Func<Bounds, ISpatialObject, bool> checkIntersection)
        {
            var results = new List<ISpatialObject>();
            var bounds = m_Bounds;

            bool CheckIntersectionAndProjectBounds(ISpatialObject obj)
            {
                if (IsAnyFlagSet(obj, excludeFlags))
                    return false;

                return checkIntersection(bounds, obj);
            }

            m_SpatialCollection.Search<ISpatialObject>(CheckIntersectionAndProjectBounds,
                distanceFunction,
                results.Add,
                m_Settings.SelectedObjectsMax);

            return results;
        }

        bool IsAnyFlagSet(ISpatialObject obj, ISet<string> flags)
        {
            if (flags == null || flags.Count == 0)
                return false;

            var spatialObject = obj as SpatialObject;
            if (spatialObject == null)
                return false;

            if (flags.Overlaps(spatialObject.Flags))
                return true;

            return false;
        }
        

        [PipeInput]
        void OnGameObjectEnabling(PipeContext<GameObjectEnabling> ctx)
        {
            // Enabled objects remove IsDisabled flag
            foreach (var obj in ctx.Data.GameObjectIds)
            {
                SetSpatialFlag(k_IsDisabledFlag, new[] { obj.Id }, true);
                AddGameObjectToSpatialObject(obj.Id, obj.GameObject);
            }

            m_IsDirty = true;
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDisabling(PipeContext<GameObjectDisabling> ctx)
        {
            // Disabled objects set IsDisabled flag
            foreach (var obj in ctx.Data.GameObjectIds)
                SetSpatialFlag(k_IsDisabledFlag, new[] { obj.Id }, false);

            m_IsDirty = true;
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            foreach (var obj in ctx.Data.GameObjectIds)
                AddGameObjectToSpatialObject(obj.Id, obj.GameObject);

            m_IsDirty = true;
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var obj in ctx.Data.GameObjectIds)
            {
                if (m_Settings.IsActive && m_IdToSpatialObjects.TryGetValue(obj.Id, out var spatialObj))
                    spatialObj.LoadedObject = null;
            }

            m_IsDirty = true;

            ctx.Continue();
        }

        [NetInput]
        void OnCameraDataChanged(NetContext<CameraDataChanged> ctx)
        {
            m_CameraData = ctx.Data;

            if (!m_IsFirstCameraData) 
                return;
            
            m_IsFirstCameraData = false;
            m_LastCameraData = m_CameraData;
            m_Net.Send(m_Self, new RunVisibilityUpdate());
        }

        void OnUpdateVisibility(NetContext<RunVisibilityUpdate> ctx)
        {
            if (m_IsPreShutdown)
                return;

            if (!m_Settings.IsActive || 
                m_SpatialCollection.ObjectCount <= 0 || 
                m_CameraData == null || 
                m_LastCameraData == null || 
                !m_IsDirty && m_LastCameraData.Position == m_CameraData.Position && m_LastCameraData.Forward == m_CameraData.Forward)
            {
                m_Net.DelayedSend(TimeSpan.FromMilliseconds(100), m_Self, new RunVisibilityUpdate());
                return;
            }
            
            var input = new CameraJobInput(this, m_CameraData);
            var rpc = m_DelegateJobOutput.Call(this, (object)null, (object)null, new DelegateJob(input, (ctx, input) =>
            {
                var data = (CameraJobInput)input;
                data.Self.m_Culling.SetCameraData(data.CameraData);
                ctx.SendSuccess(new Boxed<Matrix4x4>(m_Settings.Root.localToWorldMatrix));
            }));

            rpc.Success<Boxed<Matrix4x4>>((self, ctx, t1, rootMatrix) =>
            {
                m_RootMatrix = rootMatrix.Value;
                self.StartVisibility();
            });
            rpc.Failure((self, ctx, t1, ex) =>
            {
                if (!(ex is OperationCanceledException))
                    Debug.LogException(ex);
            });
        }

        void StartVisibility()
        {
            var t1 = new Boxed<TimeSpan>(TimeSpan.FromTicks(Stopwatch.GetTimestamp()));
            m_VisibilityResults.Clear();

            // this is where the magic happens
            m_SpatialCollection.Search<ISpatialObject>(m_VisibilityPredicate,
                m_VisibilityPrioritizer,
                m_VisibilityResults.Add,
                m_Settings.VisibleObjectsMax);

            m_IsDirty = false;

            m_InstancesToShow = new List<DynamicGuid>();
            foreach (var obj in m_VisibilityResults.Except(m_PrevVisibilityResults))
            {
                if (obj.IsVisible) 
                    continue;
                
                m_InstancesToShow.Add(obj.Id);
                obj.IsVisible = true;
            }

            m_InstancesToHide = new List<DynamicGuid>();
            foreach (var obj in m_PrevVisibilityResults.Except(m_VisibilityResults))
            {
                if (!obj.IsVisible) 
                    continue;
                
                m_InstancesToHide.Add(obj.Id);
                obj.IsVisible = false;
            }

            // save the results
            m_PrevVisibilityResults.Clear();
            m_PrevVisibilityResults.AddRange(m_VisibilityResults);

            if (m_Settings.UseCulling)
            {
                var rpc = m_DelegateJobOutput.Call(this, (object)null, t1, new DelegateJob(m_Culling, (ctx, input) =>
                {
                    var culling = (SpatialCulling)input;
                    culling.OnUpdate(ctx);
                }));

                rpc.Success((self, ctx, t1, _) => self.CompleteVisibilityResult(t1.Value));

                rpc.Failure((self, ctx, t1, ex) =>
                {
                    if (!(ex is OperationCanceledException))
                        Debug.LogException(ex);
                });
            }
            else
                CompleteVisibilityResult(t1.Value);
        }

        void CompleteVisibilityResult(TimeSpan t1)
        {
            m_LastCameraData = m_CameraData;
            m_UpdateVisibilityOutput.Send(new UpdateVisibility(m_InstancesToShow, m_InstancesToHide));

            var t2 = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            var remaining = TimeSpan.FromMilliseconds(40) - (t2 - t1);
            m_Net.DelayedSend(remaining, m_Self, new RunVisibilityUpdate());
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
                m_Settings.PriorityWeightDistance = (float)newValue;
            else if (fieldName == nameof(Settings.PriorityWeightSize))
                m_Settings.PriorityWeightSize = (float)newValue;
            else if (fieldName == nameof(Settings.UseCulling))
                m_Settings.UseCulling = (bool)newValue;
            else if (fieldName == nameof(Settings.UseDepthCulling))
                m_Settings.UseDepthCulling = (bool)newValue;
        }

        void AddGameObjectToSpatialObject(DynamicGuid id, GameObject gameObject)
        {
            if (!m_Settings.IsActive)
                return;

            if (!m_IdToSpatialObjects.TryGetValue(id, out var obj))
                return;

            obj.LoadedObject = gameObject;
        }

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

        bool DefaultVisibilityPredicate(ISpatialObject obj)
        {
            if (IsAnyFlagSet(obj, m_VisibilityIgnoreFlags))
                return false;

            if (m_Settings.UseCulling)
                return m_Culling.IsVisible(obj);

            var (min, max) = RecalculateBounds( obj, m_RootMatrix);
            m_Bounds.SetMinMax(min, max);
            return m_Bounds.SqrDistance(m_CameraData.Position) < m_CameraData.SqrFar;
        }

        float DefaultVisibilityPrioritizer(ISpatialObject obj)
        {
            var (min, max) = RecalculateBounds( obj, m_RootMatrix);
            m_Bounds.SetMinMax(min, max);
            var boundsSize = m_Bounds.size;
            var objDirection = m_Bounds.ClosestPoint(m_CameraData.Position) - m_CameraData.Position;
            // lower priority is better so change sign after adding the weighted values
            return -1f *
                   // backward [0-1] forward
                   (m_Settings.PriorityWeightAngle * (1f - Vector3.Angle(m_CameraData.Forward, objDirection) / 180f)
                    // farther [0-1] closer
                    + m_Settings.PriorityWeightDistance * (1f - objDirection.sqrMagnitude / m_GlobalMaxSqrDistance)
                    // smaller [0-1] larger
                    + m_Settings.PriorityWeightSize * (boundsSize.x + boundsSize.y + boundsSize.z) / (m_GlobalBoundsSize.x + m_GlobalBoundsSize.y + m_GlobalBoundsSize.z));
        }

        public static (Vector3 min, Vector3 max) RecalculateBounds(ISpatialObject obj, Matrix4x4 rootMatrix)
        {
            // Applying a transformation may completely change the placement of the current min/max
            // and so every corner must be transformed and a new min/max pair must be evaluated.
            //
            //        d--------c max
            //       /|       /|
            //      a--------b |
            //      | h------|-g
            //      |/       |/
            //  min e--------f

            var min = obj.Min;
            var max = obj.Max;

            var a = rootMatrix.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z));
            var b = rootMatrix.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z));
            var c = rootMatrix.MultiplyPoint3x4(max);
            var d = rootMatrix.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z));
            var e = rootMatrix.MultiplyPoint3x4(min);
            var f = rootMatrix.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z));
            var g = rootMatrix.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z));
            var h = rootMatrix.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z));

            min.x = Mathf.Min(a.x, b.x, c.x, d.x, e.x, f.x, g.x, h.x);
            max.x = Mathf.Max(a.x, b.x, c.x, d.x, e.x, f.x, g.x, h.x);

            min.y = Mathf.Min(a.y, b.y, c.y, d.y, e.y, f.y, g.y, h.y);
            max.y = Mathf.Max(a.y, b.y, c.y, d.y, e.y, f.y, g.y, h.y);

            min.z = Mathf.Min(a.z, b.z, c.z, d.z, e.z, f.z, g.z, h.z);
            max.z = Mathf.Max(a.z, b.z, c.z, d.z, e.z, f.z, g.z, h.z);

            return (min, max);
        }

        class RunVisibilityUpdate { }

        class CameraJobInput
        {
            public SpatialActor Self;
            public CameraDataChanged CameraData;

            public CameraJobInput(SpatialActor self, CameraDataChanged cameraData)
            {
                Self = self;
                CameraData = cameraData;
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            [SerializeField]
            [Transient(nameof(Root))]
            ExposedReference<Transform> m_Root;

            [HideInInspector]
            public Transform Root;
            
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

            [Space]
            public bool UseSizeCulling = true;
            [Range(0, 10)] public float MinimumScreenAreaRatioMesh = 4;

            [Space]
            public bool UseDepthCulling = true;
            public float DepthOffset = 0.005f;
            
            [Header("Selection")]
            public int SelectedObjectsMax = 10;
            
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }

        class SpatialObject : ISpatialObject
        {
            public DynamicGuid Id { get; }
            public EntryData Entry { get; }
            public Vector3 Min { get; private set; }
            public Vector3 Max { get; private set; }
            public Vector3 Center { get; private set; }
            public float Priority { get; set; }
            public bool Ignore { get; set; }
            public bool IsVisible { get; set; }
            public GameObject LoadedObject { get; set; }
            public HashSet<string> Flags { get; set; }

            public SpatialObject(DynamicGuid dynamicGuid, EntryData entryData)
            {
                Id = dynamicGuid;
                Entry = entryData;
                Flags = new HashSet<string>();
                UpdateBounds(System.Numerics.Matrix4x4.Identity);
            }

            public void UpdateBounds(System.Numerics.Matrix4x4 transformMatrix)
            {
                var newBounds = transformMatrix * Entry.Spatial.Box;

                Min = newBounds.Min.ToUnity();
                Max = newBounds.Max.ToUnity();
                Center = newBounds.Center.ToUnity();
            }

            public void Dispose()
            {
                // nothing to do
            }
        }
    }

    public interface ISpatialPickingLogic
    {
        bool CheckIntersection(Bounds globalBounds, ISpatialObject obj);
        float GetDistance(ISpatialObject obj);
    }
    
    public abstract class BasePickingLogic : ISpatialPickingLogic
    {
        public abstract bool CheckIntersection(Bounds globalBounds, ISpatialObject obj);
        public float GetDistance(ISpatialObject obj) => obj.Priority;
    }

    public class PickFromRay : BasePickingLogic
    {
        Ray Ray;

        public PickFromRay(Ray ray)
        {
            Ray = ray;
        }

        public override bool CheckIntersection(Bounds bounds, ISpatialObject obj)
        {
            bounds.SetMinMax(obj.Min, obj.Max);
            if (!bounds.IntersectRay(Ray, out var distance))
                return false;

            obj.Priority = distance;
            return true;
        }
    }

    public class PickFromSamplePoints : BasePickingLogic
    {
        int Count;

        Ray[] m_SelectionRaySamplePoints;

        public PickFromSamplePoints(Vector3[] samplePoints, int count)
        {
            Count = count;
            m_SelectionRaySamplePoints = new Ray[samplePoints.Length - 1];                

            for (var i = 0; i < m_SelectionRaySamplePoints.Length; ++i)
            {
                m_SelectionRaySamplePoints[i].origin = samplePoints[i];
                m_SelectionRaySamplePoints[i].direction = samplePoints[i + 1] - samplePoints[i];
            }
        }

        public override bool CheckIntersection(Bounds bounds, ISpatialObject obj)
        {
            bounds.SetMinMax(obj.Min, obj.Max);
            for (var i = 0; i < m_SelectionRaySamplePoints.Length; ++i)
            {
                var ray = m_SelectionRaySamplePoints[i];
                if (!bounds.IntersectRay(ray, out var distance))
                    continue;

                obj.Priority = distance;
                if (i > 0)
                    obj.Priority += Vector3.Distance(m_SelectionRaySamplePoints[0].origin, ray.origin);

                return true;
            }

            return false;
        }

    }

    public class PickFromDistance : BasePickingLogic
    {
        Vector3 Origin;
        float Distance;
        float SqrDistance => Distance * Distance;

        public PickFromDistance(Vector3 origin, float distance)
        {
            Origin = origin;
            Distance = distance;
        }
        
        public override bool CheckIntersection(Bounds bounds, ISpatialObject obj)
        {
            bounds.SetMinMax(obj.Min, obj.Max);
            var distance = bounds.SqrDistance(Origin);
            if (distance > SqrDistance)
                return false;

            obj.Priority = distance;
            return true;
        }
    }
}
