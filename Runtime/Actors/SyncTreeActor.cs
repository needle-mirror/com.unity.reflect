using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Collections;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    [Actor("fc06b87e-7f22-43c2-bc9d-ffcf40802621")]
    public class SyncTreeActor
    {
        static readonly int k_LayerDefault = LayerMask.NameToLayer("Default");
        static readonly int k_LayerHlod = LayerMask.NameToLayer("HLOD");
        
#pragma warning disable 649
        Settings m_Settings;

        RpcComponent m_Rpc;
        NetComponent m_Net;
        TimerComponent m_Timer;
        
        NetOutput<AddSpatialObjectFlag> m_AddSpatialFlagOutput;
        NetOutput<SpatialDataChanged> m_SpatialDataChangedOutput;
        NetOutput<UpdateStreaming> m_UpdateStreamingOutput;
        NetOutput<PreShutdown> m_PreShutdownOutput;
        NetOutput<RemoveAllManifestEntries> m_RemoveAllManifestEntriesOutput;
        PipeOutput<ClearBeforeSyncUpdate> m_ClearBeforeSyncUpdateOutput;
        RpcOutput<GetSpatialManifest, string> m_GetSpatialManifestOutput;
        RpcOutput<GetManifests> m_GetManifestsOutput;
        RpcOutput<RunFuncOnGameObject> m_RunFuncOncGameObjectOutput;
        RpcOutput<StopStreaming> m_StopStreamingOutput;
        RpcOutput<RestartStreaming> m_RestartStreamingOutput;
        EventOutput<SceneZonesChanged> m_SceneZonesChangedOutput;
        EventOutput<DebugDrawGizmos> m_DebugDrawGizmosOutput;
        EventOutput<DebugGui> m_DebugGuiOutput;
        EventOutput<ReloadProject> m_ReloadProjectOutput;
#pragma warning restore 649

        ActorHandle m_Handle;

        SceneZoneCalculator m_ZoneCalculator = new SceneZoneCalculator();

        readonly List<SyncTree.Object> m_PrevActiveResults = new List<SyncTree.Object>();
        readonly List<SyncTree.Object> m_ActiveResults = new List<SyncTree.Object>();
        readonly List<SyncTree.Node> m_NodeResults = new List<SyncTree.Node>();
        readonly List<(DynamicGuid Id, HlodState State)> m_HlodStateResults = new List<(DynamicGuid, HlodState)>();
        readonly HashSet<SyncId> m_NodesRequested = new HashSet<SyncId>();
        readonly Dictionary<DynamicGuid, HlodState> m_HlodStates = new Dictionary<DynamicGuid, HlodState>();
        readonly Dictionary<DynamicGuid, SyncTree.IObject> m_IdToObjects = new Dictionary<DynamicGuid, SyncTree.IObject>();
        readonly Dictionary<int, (int Nodes, int Hlods, int Objects)> m_NodeDepthCounts = new Dictionary<int, (int Nodes, int Hlods, int Objects)>();

        Delta<DynamicEntry> m_DynamicEntryDelta = new Delta<DynamicEntry>();
        List<DynamicEntry> m_VisibleEntries = new List<DynamicEntry>();
        List<SyncId> m_NodesToRequest = new List<SyncId>();

        List<DynamicGuid> m_InstancesToLoad = new List<DynamicGuid>();
        List<DynamicGuid> m_InstancesToUnloadSinceLastUpdate = new List<DynamicGuid>();

        Bounds m_Bounds = new Bounds();
        CameraDataChanged m_LastCameraData;
        CameraDataChanged m_CameraData;
        Vector3 m_CamPos;
        float m_GeometricErrorFactor;
        
        bool m_IsPreShutdown;
        bool m_IsWaitingForSceneRemoval;
        bool m_IsFirstCameraData = true;
        string m_RootNodeVersion; // null before receiving root node, then empty if project has no nodes

        Vector3 m_Min, m_Max, m_Point;
        Vector4 m_Point4;
		
        SyncTree m_SyncTree;
        List<SceneZoneCalculator.SceneZoneData> m_SceneZones = new List<SceneZoneCalculator.SceneZoneData>();

        Func<SyncTree.IObject, float> m_Prioritizer;
		
        bool IsSpatializationActive => !string.IsNullOrEmpty(m_RootNodeVersion);
        
        public void Initialize(ActorHandle selfHandle)
        {
            m_Handle = selfHandle;
            
            m_SyncTree = new SyncTree
            {
                DelayMode = m_Settings.HlodDelayMode, 
                UseHlods = m_Settings.UseHlods, 
                UsePreloading = m_Settings.UsePreloading
            };
            
            SetPrioritizer(m_Settings.Prioritizer);
            
            m_Net.Register<RunSyncTreeUpdate>(OnUpdateSyncTree);
        }

        public void Shutdown()
        {
            ClearSyncTree();
        }

        public TickResult Tick(TimeSpan endTime)
        {
            var a = m_Timer.Tick(endTime);
            var b = m_Net.Tick(endTime);
            return a == TickResult.Yield || b == TickResult.Yield ? TickResult.Yield : TickResult.Wait;
        }

        void ClearSyncTree()
        {
            m_SyncTree.Clear();
            m_IdToObjects.Clear();
            m_NodesRequested.Clear();
            m_HlodStates.Clear();
            m_NodeDepthCounts.Clear();
            m_RootNodeVersion = null;
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_IsPreShutdown = true;
            m_PreShutdownOutput.Send(new PreShutdown());
        }

        [EventInput]
        void OnMaxLoadedGameObjectsChanged(EventContext<MaxLoadedGameObjectsChanged> _)
        {
            m_SyncTree.IsDirty = true;
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            var delta = ctx.Data.Delta;
            if (m_IsWaitingForSceneRemoval && delta.Removed.Count > 0)
            {
                // This input msg is the result of the m_RemoveAllManifestEntriesOutput call
                m_IsWaitingForSceneRemoval = false;

                var clearBeforeSyncUpdateRpc = m_ClearBeforeSyncUpdateOutput.Push(this, (object)null, (object)null, new ClearBeforeSyncUpdate());
                clearBeforeSyncUpdateRpc.Success((self, ctx, userCtx, _) =>
                {

                    var restartStreamingRpc = m_RestartStreamingOutput.Call(this, (object)null, (object)null, new RestartStreaming());
                    restartStreamingRpc.Success((self, ctx, userCtx, _) =>
                    {
                        self.PerformUpdateManifestsCall();
                    });
                    restartStreamingRpc.Failure(FailureCallback);
                });
                clearBeforeSyncUpdateRpc.Failure(FailureCallback);

                return;
            }

            foreach(var entry in delta.Added)
            {
                if (SyncTree.TryCreateObject(entry, out var obj))
                    AddObject(obj);
            }

            foreach(var entry in delta.Removed)
            {
                if (m_IdToObjects.TryGetValue(entry.Id, out var obj))
                    RemoveObject(obj);
            }

            foreach(var (prev, next) in delta.Changed)
            {
                if (!SyncTree.TryCreateObject(next, out var nextObj))
                    continue;

                if (m_IdToObjects.TryGetValue(prev.Id, out var prevObj))
                    RemoveObject(prevObj);
                
                AddObject(nextObj);
            }

            if (ctx.Data.Delta.Added.All(x => x.Data.EntryType != typeof(SyncNode))) 
                return;
            
            var results = m_ZoneCalculator.ComputeSceneZones(m_SyncTree.RootNode);

            if (m_SceneZones.Select(x => x.Node).SequenceEqual(results.Select(x => x.Node))) 
                return;
            
            m_SceneZones = results;
            if (m_SceneZones.Count > 0)
                m_SceneZonesChangedOutput.Broadcast(new SceneZonesChanged(results.Select(x => x.ObjZone).ToList()));
        }

        [NetInput]
        void OnSyncNodesAdded(NetContext<SyncNodesAdded> ctx)
        {
            var hlodSourceId = ctx.Data.HlodSourceId;
            foreach (var syncNode in ctx.Data.SyncNodes)
            {
                // TODO: find a better way to do this that doesn't involve manually adding the HlodSourceId
                var newNode = new SyncNode(new SyncId(syncNode.Id.Value, hlodSourceId), syncNode.BoundingBox, syncNode.IsRoot,
                    syncNode.GeometricError,                
                    syncNode.NodeInstanceIds, 
                    syncNode.HlodInstanceIds.Select(x => new SyncId(x.Value, hlodSourceId)).ToList(), 
                    syncNode.ChildNodeIds.Select(x => new SyncId(x.Value, hlodSourceId)).ToList());
                
                m_SyncTree.Add(newNode);
                
                // if we receive a node we haven't requested directly, don't attempt to request it later
                m_NodesRequested.Add(newNode.Id);
            }
        }

        [RpcInput]
        void OnUpdateManifests(RpcContext<UpdateManifests, NullData> ctx)
        {
            if (!m_Settings.UseSpatialManifest)
            {
                OnUpdateManifestsLegacy(ctx);
                return;
            }

            var getNodesOptions = new GetNodesOptions { Depth = m_Settings.RootGetNodesDepth };
            var spatialRpc = m_GetSpatialManifestOutput.Call(this, ctx, (object)null, new GetSpatialManifest(getNodesOptions));
            spatialRpc.Success((self, ctx, userCtx, versionId) =>
            {
                Debug.Log("Spatial root loaded successfully!");
                m_RootNodeVersion = versionId;

                var getManifestOptions = new GetManifestOptions { IncludeSpatializedModels = false };
                var nonSpatialRpc = m_GetManifestsOutput.Call(this, ctx, (object)null, new GetManifests(getManifestOptions));
                nonSpatialRpc.Success((self, ctx, userCtx, data) => ctx.SendSuccess(NullData.Null));
                nonSpatialRpc.Failure((self, ctx, userCtx, ex) =>
                {
                    Debug.Log("Unable to load non-spatialized manifest. Some features may not work properly.");
                    ctx.SendFailure(ex);
                });
                
            });
            spatialRpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.Log("Unable to load spatial manifest, attempting to load full manifests...");
                OnUpdateManifestsLegacy(ctx);
            });
        }

        void OnUpdateManifestsLegacy(RpcContext<UpdateManifests, NullData> ctx)
        {
            var getManifestOptions = new GetManifestOptions { IncludeSpatializedModels = true };
            var rpc = m_GetManifestsOutput.Call(this, ctx, (object)null, new GetManifests(getManifestOptions));
            rpc.Success((self, ctx, userCtx, data) =>
            {
                Debug.Log("Full manifests loaded successfully!");
                m_RootNodeVersion = "";
                ctx.SendSuccess(NullData.Null);
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                m_RootNodeVersion = null;
                ctx.SendFailure(ex);
            });
        }

        [NetInput]
        void OnCameraDataChanged(NetContext<CameraDataChanged> ctx)
        {
            m_CameraData = ctx.Data;

            if (m_CamPos != m_CameraData.Position)
            {
                m_SyncTree.IsPreloading = false;
                m_SyncTree.IsDirty = true;
            }
            
            m_CamPos = m_CameraData.Position;
            m_GeometricErrorFactor = 1f / (2f * Mathf.Tan(Mathf.Deg2Rad * m_CameraData.FieldOfView / 2f));

            if (!m_IsFirstCameraData) 
                return;

            m_IsFirstCameraData = false;
            m_LastCameraData = m_CameraData;
            m_Net.Send(m_Handle, new RunSyncTreeUpdate());
        }

        [EventInput]
        void OnRemoteManifestChanged(EventContext<RemoteManifestChanged> ctx)
        {
            if (!m_Settings.IsLiveSyncEnabled)
                return;

            // When spatialization is active, no need to perform the sync update for every manifest that changed.
            // We only need to listen to the hlod source, which will be notified as updated as soon as the spatial manifest + hlods are generated.
            if (!IsSpatializationActive || ctx.Data.SourceId.Equals("hlod"))
                PerformLiveSyncUpdate();
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            switch (fieldName)
            {
                case nameof(Settings.IsLiveSyncEnabled):
                    if (!m_Settings.IsLiveSyncEnabled && (bool)newValue)
                        PerformLiveSyncUpdate();
                    m_Settings.IsLiveSyncEnabled = (bool)newValue;
                    break;
                case nameof(Settings.UseSpatialManifest):
                    m_Settings.UseSpatialManifest = (bool)newValue;
                    break;
                case nameof(Settings.UseHlods):
                    m_Settings.UseHlods = (bool)newValue;
                    if (m_SyncTree != null)
                    {
                        m_SyncTree.UseHlods = m_Settings.UseHlods;
                        m_SyncTree.IsDirty = true;
                    }
                    break;
                case nameof(Settings.HlodDelayMode):
                    var delayMode = (HlodMode)newValue;
                    m_Settings.HlodDelayMode = delayMode;
                    if (m_SyncTree != null)
                    {
                        m_SyncTree.DelayMode = delayMode;
                        m_SyncTree.IsDirty = true;
                    }
                    break;
                case nameof(Settings.Prioritizer):
                    var prioritizer = (Prioritizer)newValue;
                    m_Settings.Prioritizer = prioritizer;
                    SetPrioritizer(prioritizer);
                    break;
                case nameof(Settings.TargetFps):
                    m_Settings.TargetFps = (int)newValue;
                    break;
            }
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                if (!m_IdToObjects.TryGetValue(go.Id, out var obj))
                    continue;
                
                obj.IsLoaded = true;
                if (m_Settings.UseHlodLoadingMaterial && m_Settings.HlodDelayMode != HlodMode.None && obj.IsHlodInstance)
                    SetLayer(go.Id, m_HlodStates.TryGetValue(go.Id, out var state) ? state : HlodState.Loading);
            }

            if (!m_SyncTree.IsPreloading && !m_PrevActiveResults.Exists(x => !x.IsLoaded))
            {
                m_SyncTree.IsPreloading = true;
                m_SyncTree.IsDirty = true;
            }

            if (m_Settings.HlodDelayMode != HlodMode.None)
                m_SyncTree.IsDirty = true;
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                if (m_IdToObjects.TryGetValue(go.Id, out var obj))
                    obj.IsLoaded = false;
            }

            if (m_Settings.HlodDelayMode != HlodMode.None)
                m_SyncTree.IsDirty = true;
            
            ctx.Continue();
        }

        void FailureCallback(SyncTreeActor self, object ctx, object userCtx, Exception ex)
        {
            if (!(ex is OperationCanceledException)) Debug.LogException(ex);
        }

        void PerformLiveSyncUpdate()
        {
            if (IsSpatializationActive)
            {
                m_ReloadProjectOutput.Broadcast(new ReloadProject());
            }
            else
            {
                PerformUpdateManifestsCall();
            }
        }

        void PerformUpdateManifestsCall()
        {
            var rpc = m_Rpc.Call(this, (object)null, (object)null, m_Handle, new UpdateManifests());
            rpc.Success<NullData>((self, ctx, userCtx, _) => Debug.Log("Live sync update successful!"));
            rpc.Failure((self, ctx, userCtx, ex) => { if (!(ex is OperationCanceledException)) Debug.LogException(ex); });
        }

        void AddObject(SyncTree.IObject obj)
        {
            m_IdToObjects.Add(obj.Entry.Id, obj);
            m_SyncTree.Add(obj);
        }

        void RemoveObject(SyncTree.IObject obj)
        {
            m_IdToObjects.Remove(obj.Entry.Id);
            m_SyncTree.Remove(obj);
        }
        
        void OnUpdateSyncTree(NetContext<RunSyncTreeUpdate> ctx)
        {
            if (m_IsPreShutdown)
                return;

            // wait until we've received a root node version event to determine if we need to use the spatial manifest
            if (m_SyncTree.ObjectCount == 0 || 
                m_RootNodeVersion == null || 
                m_CameraData == null || 
                m_LastCameraData == null || 
                !m_SyncTree.IsDirty && m_LastCameraData.Position == m_CameraData.Position && m_LastCameraData.Forward == m_CameraData.Forward)
            {
                m_Net.DelayedSend(TimeSpan.FromMilliseconds(100), m_Handle, new RunSyncTreeUpdate());
                return;
            }

            m_LastCameraData = m_CameraData;
            
            var t1 = TimeSpan.FromTicks(Stopwatch.GetTimestamp());

            var didSearch = true;
            var useSpatialManifest = !string.IsNullOrEmpty(m_RootNodeVersion) && m_Settings.UseSpatialManifest;
            if (useSpatialManifest && m_SyncTree.HasRootSyncNode)
            {
                if (m_Settings.UseHlods)
                    m_SyncTree.Search(m_CamPos, 
                        SearchPredicate, 
                        m_Prioritizer, 
                        m_ActiveResults, 
                        m_NodeResults, 
                        m_HlodStateResults, 
                        m_Settings.MaxInstanceCountLegacy);
                else
                    m_SyncTree.Search(m_CamPos, 
                        DisabledPredicate,
                        m_Prioritizer,
                        m_ActiveResults,
                        m_NodeResults,
                        m_HlodStateResults, 
                        m_Settings.MaxInstanceCountLegacy);
                    
                m_SyncTree.IsDirty = false;
            }
            else if (!useSpatialManifest && m_SyncTree.IsDirty)
            {
                m_SyncTree.Search(m_CamPos, 
                    DisabledPredicate,
                    m_Prioritizer,
                    m_ActiveResults, 
                    m_NodeResults, 
                    m_HlodStateResults, 
                    m_Settings.MaxInstanceCountLegacy);
			
                m_SyncTree.IsDirty = false;
            }
            else
            {
                didSearch = false;
            }

            if (!m_DynamicEntryDelta.IsEmpty())
                m_DynamicEntryDelta = new Delta<DynamicEntry>();

            if (didSearch)
            {
                m_InstancesToLoad = new List<DynamicGuid>();
                
                m_InstancesToLoad.AddRange(m_ActiveResults.Select(x => x.Entry.Id));
                
                m_DynamicEntryDelta.Added.AddRange(m_ActiveResults
                    .Where(x => x.IsVisible)
                    .Select(x => x.Entry)
                    .Except(m_VisibleEntries));

                var resultsNotActiveAnymore = m_PrevActiveResults
                    .Except(m_ActiveResults)
                    .ToList();

                m_InstancesToUnloadSinceLastUpdate = new List<DynamicGuid>();
                m_InstancesToUnloadSinceLastUpdate.AddRange(resultsNotActiveAnymore
                    .Select(x => x.Entry.Id));

                m_DynamicEntryDelta.Removed.AddRange(m_ActiveResults
                    .Where(x => !x.IsVisible)
                    .Select(x => x.Entry)
                    .Intersect(m_VisibleEntries));
                m_DynamicEntryDelta.Removed.AddRange(resultsNotActiveAnymore
                    .Select(x => x.Entry)
                    .Intersect(m_VisibleEntries)
                    .Except(m_DynamicEntryDelta.Removed));

                // save the results
                m_PrevActiveResults.Clear();
                m_PrevActiveResults.AddRange(m_ActiveResults);
                m_VisibleEntries.AddRange(m_DynamicEntryDelta.Added);
                m_VisibleEntries = m_VisibleEntries.Except(m_DynamicEntryDelta.Removed).ToList();

                if (useSpatialManifest)
                {
                    m_NodesToRequest = new List<SyncId>();
                    if (m_RootNodeVersion != null && m_NodeResults.Count > 0)
                    {
                        m_NodesToRequest.AddRange(m_NodeResults
                            .Where(x => !x.AreChildNodeEntriesCreated)
                            .SelectMany(x => x.SyncNode.ChildNodeIds)
                            .Except(m_NodesRequested));   
                    }
                }
            }

            m_UpdateStreamingOutput.Send(new UpdateStreaming(m_InstancesToLoad, m_InstancesToUnloadSinceLastUpdate));

            if (!m_DynamicEntryDelta.IsEmpty())
            {
                m_SpatialDataChangedOutput.Send(new SpatialDataChanged(m_DynamicEntryDelta));
                SendDebugDrawGizmos();
                var hlodSyncIds = new HashSet<DynamicGuid>();
                foreach(var data in m_DynamicEntryDelta.Added)
                {
                    if(m_IdToObjects.TryGetValue(data.Id, out var obj))
                    {
                        if(obj.IsHlodInstance)
                        {
                            hlodSyncIds.Add(data.Id);
                        }
                    }
                }

                m_AddSpatialFlagOutput.Send(new AddSpatialObjectFlag(SpatialActor.k_IsHlodFlag, hlodSyncIds));
            }

            if (m_HlodStateResults.Count > 0)
            {
                foreach (var hlod in m_HlodStateResults)
                    SetLayer(hlod.Id, m_Settings.UseHlodLoadingMaterial ? hlod.State : HlodState.Default);
            }

            if (m_NodesToRequest.Count > 0)
            {
                const int maxBatchSize = 256;
                var nbBatches = Mathf.CeilToInt(m_NodesToRequest.Count / (float)maxBatchSize);
                for (var i = 0; i < nbBatches; ++i)
                {
                    var batchSize = i == nbBatches - 1 ? m_NodesToRequest.Count % maxBatchSize : maxBatchSize;
                    var getNodesOptions = new GetNodesOptions { Version = m_RootNodeVersion };
                    var rpc = m_GetSpatialManifestOutput.Call(this, (object)null, (object)null, 
                        new GetSpatialManifest(getNodesOptions, m_NodesToRequest.GetRange(i * maxBatchSize, batchSize)));
                    rpc.Success((self, ctx, tracker, _) => { });
                    rpc.Failure((self, ctx, tracker, ex) =>
                    {
                        if (!(ex is OperationCanceledException))
                            Debug.LogException(ex);
                    });
                }
                        
                foreach (var syncId in m_NodesToRequest)
                    m_NodesRequested.Add(syncId);
            }
            
            SendDebugGui();

            var t2 = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            var remaining = TimeSpan.FromMilliseconds(40) - (t2 - t1);
            m_Net.DelayedSend(remaining, m_Handle, new RunSyncTreeUpdate());
        }

        void SendDebugDrawGizmos()
        {
#if UNITY_EDITOR
            var command = new Action(() => { });
            
            var nodeGradient = m_Settings.NodeGradient;
            var depth = m_SyncTree.Depth;
            foreach (var node in m_NodeResults)
            {
                var color = nodeGradient.Evaluate(depth > 0 ? (float)node.Depth / depth : 0);
                var center = node.Bounds.Center.ToUnity();
                var size = node.Bounds.Size.ToUnity();
                command += () =>
                {
                    Gizmos.color = color;
                    Gizmos.DrawWireCube(center, size);
                };
            }
            
            var objectGradient = m_Settings.ObjectGradient;
            float count = m_ActiveResults.Count;
            for (var i = 0; i < count; ++i)
            {
                var obj = m_ActiveResults[i];
                var color = objectGradient.Evaluate(count > 0 ? i / count : 0);
                var center = obj.Bounds.Center.ToUnity();
                var size = obj.Bounds.Size.ToUnity();
                command += () =>
                {
                    Gizmos.color = color;
                    Gizmos.DrawCube(center, size);
                };
            }
            
            m_DebugDrawGizmosOutput.Broadcast(new DebugDrawGizmos(command));
#endif
        }

        void SendDebugGui()
        {
            const string debugFormat = "Total objects {0}\n" +
                                       "Total nodes {1}\n" +
                                       "Visible objects/HLODs {2}\n" +
                                       "Geometric error threshold {3}";
            const string usePreloadingLabel = "Use preloading";
            const string useHlodLoadingMaterialLabel = "Use HLOD loading material";
            const string rootGetNodesDepthFormat = "Root GetNodes() depth {0}";
            const string showNodeDepthCountsLabel = "Show active node depth counts";
            const string nodeDepthCountsLabel = "Depth\t| Nodes\t| HLODs\t| Objects";
            const string nodeDepthFormat = "{0}\t| {1}\t| {2}\t| {3}";

            var nodeDepthResult = "";
            if (m_Settings.DebugNodeDepthCounts)
            {
                RefreshNodeDepthCounts();
                var sb = new StringBuilder();
                sb.AppendLine(nodeDepthCountsLabel);
                for (var i = 0; i < m_NodeDepthCounts.Count; ++i)
                {
                    var (nodes, hlods, objects) = m_NodeDepthCounts[i];
                    if (i == m_NodeDepthCounts.Count - 1)
                        sb.Append(string.Format(nodeDepthFormat, i, nodes, hlods, objects));
                    else
                        sb.AppendLine(string.Format(nodeDepthFormat, i, nodes, hlods, objects));
                }
                nodeDepthResult = sb.ToString();
            }

            m_DebugGuiOutput.Broadcast(new DebugGui(() =>
            {
                GUILayout.Label(string.Format(debugFormat,
                    m_SyncTree.ObjectCount,
                    m_SyncTree.SyncNodeCount,
                    m_VisibleEntries.Count,
                    m_Settings.GeometricErrorThreshold));
                m_Settings.GeometricErrorThreshold = (int)GUILayout.HorizontalSlider(m_Settings.GeometricErrorThreshold, 0, 100);
                
                m_SyncTree.UsePreloading = m_Settings.UsePreloading = GUILayout.Toggle(m_Settings.UsePreloading, usePreloadingLabel);
                
                m_Settings.UseHlodLoadingMaterial = GUILayout.Toggle(m_Settings.UseHlodLoadingMaterial, useHlodLoadingMaterialLabel);
                
                GUILayout.Label(string.Format(rootGetNodesDepthFormat, m_Settings.RootGetNodesDepth));
                m_Settings.RootGetNodesDepth = (int)GUILayout.HorizontalSlider(m_Settings.RootGetNodesDepth, 0, 3);
                
                var wasDebugNodeDepthCounts = m_Settings.DebugNodeDepthCounts;
                m_Settings.DebugNodeDepthCounts = GUILayout.Toggle(m_Settings.DebugNodeDepthCounts, showNodeDepthCountsLabel);
                if (wasDebugNodeDepthCounts != m_Settings.DebugNodeDepthCounts)
                    m_SyncTree.IsDirty = true;

                if (!string.IsNullOrEmpty(nodeDepthResult)) 
                    GUILayout.Label(nodeDepthResult);
            }));
        }

        void SetLayer(DynamicGuid id, HlodState hlodState)
        {
            if (m_HlodStates.TryGetValue(id, out var state) && state == hlodState)
                return;
            
            m_HlodStates[id] = hlodState;
            var layer = hlodState == HlodState.Loading ? k_LayerHlod : k_LayerDefault;
            var rpc = m_RunFuncOncGameObjectOutput.Call(this, (object)null, (object)null, new RunFuncOnGameObject(id, default, obj =>
            {
                obj.SetLayerRecursively(layer);
                return obj;
            }));
            rpc.Success((self, ctx, userCtx, _) => { });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                if (!(ex is OperationCanceledException) && !(ex is MissingGameObjectException))
                    Debug.LogException(ex);
            });
        }

        bool SearchPredicate(SyncTree.Node node)
        {
            m_Bounds.SetMinMax(node.Bounds.Min.ToUnity(), node.Bounds.Max.ToUnity());
            var tileDistance = (m_Bounds.ClosestPoint(m_CamPos) - m_CamPos).magnitude;
            // if we're inside the bounds always expand
            if (tileDistance <= 0f)
                return true;
            
            // let SSE = (geometricError ⋅ screenHeight) / (tileDistance ⋅ 2 ⋅ tan(fovy / 2))
            // and GEF = 1 / (2 ⋅ tan(fovy / 2))
            // therefore SSE = geometricError ⋅ screenHeight ⋅ GEF / tileDistance
            var screenSpaceError = node.GeometricError * m_CameraData.ScreenHeight * m_GeometricErrorFactor / tileDistance;
            return screenSpaceError > m_Settings.GeometricErrorThreshold;
        }

        static bool DisabledPredicate(SyncTree.Node node)
        {
            // here we assume the screen-space geometric error threshold is 0, so nodes will always expand
            // unless prevented by another check (ex: object limit)
            return true;
        }

        void SetPrioritizer(Prioritizer prioritizer)
        {
            switch (prioritizer)
            {
                case Prioritizer.SqrDistance:
                    m_Prioritizer = SqrDistancePrioritizer;
                    break;
                case Prioritizer.SqrDistanceByExtents:
                    m_Prioritizer = SqrDistanceByExtentsPrioritizer;
                    break;
                case Prioritizer.SqrDistanceByVolume:
                    m_Prioritizer = SqrDistanceByVolumePrioritizer;
                    break;
                case Prioritizer.QuadDistanceByExtents:
                    m_Prioritizer = QuadDistanceByExtentsPrioritizer;
                    break;
                case Prioritizer.QuadDistanceByVolume:
                    m_Prioritizer = QuadDistanceByVolumePrioritizer;
                    break;
                case Prioritizer.AngledQuadDistanceByExtents:
                    m_Prioritizer = AngledQuadDistanceByExtentsPrioritizer;
                    break;
                case Prioritizer.ScreenAreaPoints:
                    m_Prioritizer = ScreenAreaPointsPrioritizer;
                    break;
                case Prioritizer.ScreenAreaFov:
                    m_Prioritizer = ScreenAreaFovPrioritizer;
                    break;
            }
        }

        float SqrDistancePrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            return m_Bounds.SqrDistance(m_CamPos);
        }

        float SqrDistanceByExtentsPrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            var extents = m_Bounds.extents;
            return m_Bounds.SqrDistance(m_CamPos) / (extents.x + extents.y + extents.z);
        }

        float SqrDistanceByVolumePrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            var size = m_Bounds.size;
            return m_Bounds.SqrDistance(m_CamPos) / (size.x * size.y * size.z);
        }

        float QuadDistanceByExtentsPrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            var extents = m_Bounds.extents;
            var sqrDist = m_Bounds.SqrDistance(m_CamPos);
            return sqrDist * sqrDist / (extents.x + extents.y + extents.z);
        }

        float QuadDistanceByVolumePrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            var size = m_Bounds.size;
            var sqrDist = m_Bounds.SqrDistance(m_CamPos);
            return sqrDist * sqrDist / (size.x * size.y * size.z);
        }

        float AngledQuadDistanceByExtentsPrioritizer(SyncTree.IObject obj)
        {
            m_Bounds.SetMinMax(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity());
            var extents = m_Bounds.extents;
            var closestPoint = m_Bounds.ClosestPoint(m_CamPos);
            var diff = closestPoint - m_CamPos;
            var quadDist = diff.sqrMagnitude * diff.sqrMagnitude;
            var angle = Vector3.Angle(m_CameraData.Forward, diff) / 180f;
            return quadDist * angle / (extents.x + extents.y + extents.z);
        }

        float ScreenAreaPointsPrioritizer(SyncTree.IObject obj)
        {
            if (TryCalculateScreenRect(obj.Bounds.Min.ToUnity(), obj.Bounds.Max.ToUnity(), out var min, out var max))
                return 100f * (1f - (max.x - min.x) * (max.y - min.y));

            return SqrDistanceByVolumePrioritizer(obj);
        }

        float ScreenAreaFovPrioritizer(SyncTree.IObject obj)
        {
            var r = Mathf.Max(m_Bounds.extents.x, m_Bounds.extents.y, m_Bounds.extents.z) /
                    (m_CameraData.FieldOfView * Vector3.Dot(m_CameraData.Forward, m_Bounds.center - m_CameraData.Position));
            var a = Mathf.PI * r * r;
            return 100f * (1f - a / 4f);
        }

        bool TryCalculateScreenRect(Vector3 boundsMin, Vector3 boundsMax, out Vector2 min, out Vector2 max)
        {
            m_Min = min = boundsMin;
            m_Max = max = boundsMax;

            for (byte i = 0; i < 8; ++i)
            {
                // calculate each corner of the bounding box
                m_Point.x = (i >> 2) % 2 > 0 ? m_Max.x : m_Min.x;
                m_Point.y = (i >> 1) % 2 > 0 ? m_Max.y : m_Min.y;
                m_Point.z = i % 2 > 0 ? m_Max.z : m_Min.z;

                // screen rect will be invalid if a point is behind the camera due to the projection matrix
                // GetDistanceToPoint is faster here than GetSide since it uses floats instead of doubles internally
                if (m_CameraData.ForwardPlane.GetDistanceToPoint(m_Point) < 0f)
                    return false;

                m_Point = WorldToViewportPoint(m_Point);

                if (i == 0)
                {
                    min = max = m_Point;
                    continue;
                }

                if (m_Point.x < min.x) min.x = m_Point.x;
                if (m_Point.y < min.y) min.y = m_Point.y;
                if (m_Point.x > max.x) max.x = m_Point.x;
                if (m_Point.y > max.y) max.y = m_Point.y;
            }

            min.x = Mathf.Clamp01(min.x);
            min.y = Mathf.Clamp01(min.y);
            max.x = Mathf.Clamp01(max.x);
            max.y = Mathf.Clamp01(max.y);

            return true;
        }

        Vector3 WorldToViewportPoint(Vector3 point)
        {
            // convert to Vector4 for Matrix4x4 multiplication
            m_Point4.Set(point.x, point.y, point.z, 1f);
            m_Point4 = m_CameraData.ViewProjectionMatrix * m_Point4;

            // normalize
            point = m_Point4;
            point /= -m_Point4.w;

            // convert from clip to Unity viewport space
            point.x += 1f;
            point.x /= 2f;
            point.y += 1f;
            point.y /= 2f;

            return point;
        }

        void RefreshNodeDepthCounts()
        {
            m_NodeDepthCounts.Clear();
            foreach (var node in m_NodeResults)
            {
                var depth = node.Depth;
                var objects = node.Objects.Count(x => x.IsVisible);
                var hlods = node.Hlods.Count(x => x.IsVisible);
                if (m_NodeDepthCounts.TryGetValue(depth, out var value))
                    m_NodeDepthCounts[depth] = (value.Nodes + 1, value.Hlods + hlods, value.Objects + objects);
                else
                    m_NodeDepthCounts.Add(depth, (1, hlods, objects));
            }
        }
        
        class RunSyncTreeUpdate { }

        public enum Prioritizer
        {
            SqrDistance, 
            SqrDistanceByExtents, 
            SqrDistanceByVolume, 
            QuadDistanceByExtents, 
            QuadDistanceByVolume, 
            AngledQuadDistanceByExtents, 
            ScreenAreaPoints, 
            ScreenAreaFov
        }

        class SceneZoneCalculator
        {
            const float k_DensityCriteria = 1.0f / 27.0f;

            Dictionary<SyncNode, Aabb> m_CachedAabbs = new Dictionary<SyncNode, Aabb>();
            Dictionary<SyncNode, ZoneDensity> m_CachedDensities = new Dictionary<SyncNode, ZoneDensity>();

            public List<SceneZoneData> ComputeSceneZones(SyncTree.Node root)
            {
                var results = new List<SceneZoneData>();

                if (root?.SyncNode == null)
                    return results;

                var stack = new Stack<(SyncTree.Node Node, bool RemoveParent, int Depth)>();
                stack.Push((root, false, 0));

                while (stack.Count != 0)
                {
                    var (node, removeParent, depth) = stack.Pop();

                    var objBox = ComputeAabb(node);
                    var density = ComputeDensity(node, objBox);

                    if (density.VolumeSum / density.SpaceVolume > k_DensityCriteria)
                    {
                        if (node.SyncNode.ChildNodeIds.Count > 0)
                        {
                            foreach (var childNode in node.ChildNodes)
                            {
                                if (childNode.SyncNode != null)
                                {
                                    var childObjBox = ComputeAabb(childNode);
                                    var childDensity = ComputeDensity(childNode, childObjBox);

                                    // Child is way bigger than parent, it's probably garbage, discard it
                                    if (childObjBox.Extents.X > objBox.Extents.X * 2.0f ||
                                        childObjBox.Extents.Y > objBox.Extents.Y * 2.0f ||
                                        childObjBox.Extents.Z > objBox.Extents.Z * 2.0f)
                                        continue;

                                    if (childDensity.VolumeSum / childDensity.SpaceVolume > k_DensityCriteria)
                                    {
                                        var maxDistance = ShortestSize(objBox.Extents);
                                        var overlap = objBox.Overlap(childObjBox);
                                        if (overlap.X > maxDistance || overlap.Y > maxDistance || overlap.Z > maxDistance)
                                        {
                                            stack.Push((childNode, false, depth + 1));
                                            continue;
                                        }

                                        objBox.Encapsulate(childObjBox);
                                        density = new ZoneDensity(objBox.Volume, density.VolumeSum + childDensity.VolumeSum);
                                    }
                                }
                            }
                        }

                        if (removeParent)
                            results.RemoveAll(x => IsParentOf(x.Node, node));

                        results.Add(new SceneZoneData(node, depth,
                            new SceneZone(objBox, density, true),
                            new SceneZone(node.Bounds, ComputeDensity(node, node.Bounds), true)));
                    }
                    else
                    {
                        var nbReadyChild = 0;
                        foreach (var childNode in node.ChildNodes)
                        {
                            if (childNode.SyncNode != null)
                            {
                                stack.Push((childNode, true, depth + 1));
                                ++nbReadyChild;
                            }
                        }

                        if (nbReadyChild == 0 && node.SyncNode.ChildNodeIds.Count > 0)
                        {
                            if (removeParent)
                                results.RemoveAll(x => IsParentOf(x.Node, node));

                            results.Add(new SceneZoneData(node, depth,
                                new SceneZone(objBox, density, false),
                                new SceneZone(node.Bounds, ComputeDensity(node, node.Bounds), false)));
                        }
                    }
                }

                if (results.Count == 0)
                {
                    results.Add(new SceneZoneData(root, 0,
                        new SceneZone(ComputeAabb(root), ComputeDensity(root, ComputeAabb(root)), false),
                        new SceneZone(root.Bounds, ComputeDensity(root, root.Bounds), false)));
                }

                var mergedIds = new List<int>();
                do
                {
                    mergedIds.Clear();

                    for (var i = 0; i < results.Count; ++i)
                    {
                        if (mergedIds.Contains(i))
                            continue;

                        for (var j = 0; j < results.Count; ++j)
                        {
                            if (j == i || mergedIds.Contains(j))
                                continue;

                            var zoneA = results[i];
                            var zoneB = results[j];
                            var a = zoneA.ObjZone.Bounds;
                            var b = zoneB.ObjZone.Bounds;

                            var maxDistance = Math.Min(ShortestSize(a.Extents), ShortestSize(b.Extents));
                            var overlap = a.Overlap(b);
                            if (overlap.X > maxDistance || overlap.Y > maxDistance || overlap.Z > maxDistance)
                                continue;

                            a.Encapsulate(b);
                            zoneA.ObjZone.IsReliable |= zoneB.ObjZone.IsReliable;
                            zoneA.NodeZone.IsReliable |= zoneB.NodeZone.IsReliable;
                            zoneA.ObjZone.Bounds = a;
                            zoneA.ObjZone.Density = new ZoneDensity(a.Volume, zoneA.ObjZone.Density.VolumeSum + zoneB.ObjZone.Density.VolumeSum);
                            results[i] = zoneA;
                            mergedIds.Add(j);
                        }
                    }

                    mergedIds.Sort();
                    for(var j = mergedIds.Count - 1; j >= 0; --j)
                        results.RemoveAt(mergedIds[j]);

                } while (mergedIds.Count > 0);
                
                return results
                    .OrderByDescending(x => x.ObjZone.Density.SpaceVolume)
                    .ToList();
            }

            static float ShortestSize(System.Numerics.Vector3 v)
            {
                if (v.X < v.Y) return v.X < v.Z ? v.X : v.Z;
                return v.Y < v.Z ? v.Y : v.Z;
            }

            static bool IsParentOf(SyncTree.Node parent, SyncTree.Node child)
            {
                while (child != null)
                {
                    if (child == parent)
                        return true;

                    child = child.Parent;
                }

                return false;
            }

            Aabb ComputeAabb(SyncTree.Node node)
            {
                if (m_CachedAabbs.TryGetValue(node.SyncNode, out var box))
                    return box;
                
                if (node.Objects.Count > 0)
                    box = node.Objects[0].Bounds;
                else if (node.Hlods.Count > 0)
                    box = node.Hlods[0].Bounds;
                
                foreach(var obj in node.Objects)
                    box.Encapsulate(obj.Bounds);

                foreach(var hlod in node.Hlods)
                    box.Encapsulate(hlod.Bounds);

                m_CachedAabbs.Add(node.SyncNode, box);

                return box;
            }

            ZoneDensity ComputeDensity(SyncTree.Node node, Aabb box)
            {
                if (m_CachedDensities.TryGetValue(node.SyncNode, out var density))
                    return density;

                var volumeSum = 0.0;
                foreach (var obj in node.Objects)
                    volumeSum += obj.Bounds.Volume;
                foreach (var hlod in node.Hlods)
                    volumeSum += hlod.Bounds.Volume;
                
                var spaceVolume = box.Volume;
                density = new ZoneDensity(spaceVolume, volumeSum);
                m_CachedDensities.Add(node.SyncNode, density);
                return density;
            }

            public struct SceneZoneData
            {
                public SyncTree.Node Node;
                public int Depth;
                public SceneZone ObjZone;
                public SceneZone NodeZone;

                public SceneZoneData(SyncTree.Node node, int depth, SceneZone objZone, SceneZone nodeZone)
                {
                    Node = node;
                    Depth = depth;
                    ObjZone = objZone;
                    NodeZone = nodeZone;
                }
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public const int k_DefaultMaxInstanceCountLegacy = 100000;
            public const int k_DefaultTargetFps = 30;
            public const int k_DefaultGeometricErrorThreshold = 3;
            
            [Header("Live Sync")]
            public bool IsLiveSyncEnabled;
            
            [Header("HLODs")]
            public bool UseSpatialManifest = true;
            public bool UseHlods = true;
            public HlodMode HlodDelayMode = HlodMode.Hlods;
            public Prioritizer Prioritizer = Prioritizer.SqrDistance;
            public int MaxInstanceCountLegacy = k_DefaultMaxInstanceCountLegacy;
            public int TargetFps = k_DefaultTargetFps;
            public int GeometricErrorThreshold = k_DefaultGeometricErrorThreshold;
            public bool UsePreloading = false;
            public int RootGetNodesDepth = 2;
            public bool UseHlodLoadingMaterial = false;

            [Header("Debug")] 
            public Gradient ObjectGradient;
            public Gradient NodeGradient;
            public bool DebugNodeDepthCounts;
            
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
