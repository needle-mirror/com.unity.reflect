using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    [Actor("a892ea45-0772-48f0-8494-2e5ef04e410f")]
    public class GameObjectLifecycleActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetComponent m_Net;

        PipeOutput<GameObjectCreating> m_GameObjectCreatingOutput;
        PipeOutput<GameObjectDestroying> m_GameObjectDestroyingOutput;
        PipeOutput<GameObjectEnabling> m_GameObjectEnablingOutput;
        PipeOutput<GameObjectDisabling> m_GameObjectDisablingOutput;

        RpcOutput<DelegateJob> m_DelegateJobOutput;
        RpcOutput<GetStableId<DynamicGuid>> m_GetStableIdOutput;
        RpcOutput<GetDynamicIds> m_GetDynamicIdsOutput;
        NetOutput<PreShutdown> m_PreShutdownOutput;
#pragma warning restore 649
        
        readonly Dictionary<DynamicGuid, GameObjectLifecycle> m_Instances = new Dictionary<DynamicGuid, GameObjectLifecycle>();

        long m_NbPendingRequests;
        bool m_IsShutdown;
        bool m_IsMemoryUsageTooHigh;

        void Shutdown()
        {
            foreach (var kv in m_Instances)
                Object.Destroy(kv.Value.GameObject);

            m_Instances.Clear();
        }

        [NetInput]
        void OnCreateGameObjectLifecycle(NetContext<CreateGameObjectLifecycle> ctx)
        {
            var tracker = new Tracker<GameObjectLifecycle>();
            var index = 0;
            
            foreach (var (id, go) in ctx.Data.GameObjectIdList)
            {
                if (m_Instances.ContainsKey(id))
                    continue;
                
                ++m_NbPendingRequests;
                ++tracker.NbPendingCalls;
                var lifecycle = new GameObjectLifecycle(id, new EntryStableGuid(), LifecycleState.Creating, go);
                tracker.Results.Add(lifecycle);
                m_Instances.Add(id, lifecycle);
                var userCtx = new {Lifecycle = lifecycle, Tracker = tracker, Index = index};
                ++index;

                var rpc = m_GetStableIdOutput.Call(this, ctx, userCtx, new GetStableId<DynamicGuid>(id));
                rpc.Success<Boxed<EntryStableGuid>>((self, ctx,userCtx , boxedStableId) =>
                {
                    userCtx.Lifecycle.StableId = boxedStableId.Value;
                    --userCtx.Tracker.NbPendingCalls;

                    if (userCtx.Tracker.NbPendingCalls > 0)
                        return;

                    var data = new GameObjectCreating(userCtx.Tracker.Results
                        .FindAll(x => x != null)
                        .Select(x => new GameObjectIdentifier(x.Id, x.StableId, x.GameObject))
                        .ToList());
                    var pipe = self.m_GameObjectCreatingOutput.Push(this, ctx, (object)null, data);
                    pipe.Success((self, ctx, userCtx, msg) =>
                    {
                        var enablingData = new List<GameObjectIdentifier>();
                        var destroyingData = new List<GameObjectIdentifier>();
                        
                        foreach (var go in msg.GameObjectIds)
                        {
                            --self.m_NbPendingRequests;
                            var lifecycle = self.m_Instances[go.Id];

                            var state = lifecycle.State;
                            lifecycle.State = LifecycleState.Disabled;

                            switch (state)
                            {
                                case LifecycleState.Creating:
                                case LifecycleState.Enabled:
                                case LifecycleState.Disabled:
                                case LifecycleState.Disabling:
                                    break;
                                case LifecycleState.Enabling:
                                    lifecycle.State = LifecycleState.Enabling;
                                    enablingData.Add(go);
                                    break;
                                case LifecycleState.Destroying:
                                    lifecycle.State = LifecycleState.Destroying;
                                    destroyingData.Add(go);
                                    break;
                            }
                        }
                        
                        self.StartEnablingGameObjects(enablingData);
                        self.StartDestroyingGameObjects(destroyingData);
                        
                        self.TryShutdown();
                    });
                    pipe.Failure((self, ctx, userCtx, ex) => Debug.LogException(ex));
                });
                rpc.Failure((self, ctx, userCtx, ex) =>
                {
                    --m_NbPendingRequests;
                    --userCtx.Tracker.NbPendingCalls;
                    userCtx.Tracker.Results[userCtx.Index] = null;
                    self.m_Instances.Remove(lifecycle.Id);
                    Debug.LogException(ex);
                });
            }
        }

        [NetInput]
        void OnDestroyGameObjectLifecycle(NetContext<DestroyGameObjectLifecycle> ctx)
        {
            var destroyGameObjectData = new List<GameObjectIdentifier>();
            
            foreach (var id in ctx.Data.IdList)
            {
                if (!m_Instances.TryGetValue(id, out var lifecycle))
                    continue;
            
                destroyGameObjectData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
            }

            DestroyGameObjects(destroyGameObjectData);
        }

        [NetInput]
        void OnToggleGameObject(NetContext<ToggleGameObject> ctx)
        {
            var visibilityData = new List<(GameObjectIdentifier Obj, bool Enable)>();
            
            foreach (var (id, nbEnabled) in ctx.Data.ToggleGameObjectList)
            {
                if (!m_Instances.TryGetValue(id, out var lifecycle))
                    continue;

                var prevVal = lifecycle.Enabled;
                lifecycle.Enabled += nbEnabled;

                if (lifecycle.Enabled >= 1 && prevVal < 1)
                    visibilityData.Add((new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject), true));
                if (lifecycle.Enabled <= 0 && prevVal > 0)
                    visibilityData.Add((new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject), false));
            }
            
            UpdateGameObjectsVisibility(visibilityData);
        }

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            m_IsMemoryUsageTooHigh = ctx.Data.IsMemoryLevelTooHigh;
            
            if (m_IsMemoryUsageTooHigh)
                DestroyAllGameObjects();
        }

        [RpcInput]
        void OnRunFunctionOnGameObject(RpcContext<RunFuncOnGameObject> ctx)
        {
            if (ctx.Data.Id != default)
            {
                if (!m_Instances.TryGetValue(ctx.Data.Id, out var lifecycle))
                {
                    ctx.SendFailure(new MissingGameObjectException());
                    return;
                }

                RunFuncOnGameObjectJob(ctx, lifecycle);
            }
            else if (ctx.Data.StableId != default)
            {
                var rpc = m_GetDynamicIdsOutput.Call(this, ctx, (object)null, new GetDynamicIds(ctx.Data.StableId));
                rpc.Success<List<DynamicGuid>>((self, ctx, userCtx, ids) =>
                {
                    foreach (var id in ids)
                    {
                        if (m_Instances.TryGetValue(id, out var lifecycle))
                        {
                            RunFuncOnGameObjectJob(ctx, lifecycle);
                            return;
                        }
                    }

                    ctx.SendFailure(new MissingGameObjectException());
                });
                rpc.Failure((self, ctx, userCtx, ex) => ctx.SendFailure(ex));
            }
            else
                ctx.SendFailure(new ArgumentException($"As least one of ({nameof(ctx.Data.Id)}, {nameof(ctx.Data.StableId)}) must be specified."));
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_IsShutdown = true;

            // Complete all pending messages, else some GameObject
            // may be stuck in the queue and won't be destroyed properly
            m_Net.Tick(TimeSpan.MaxValue);
            TryShutdown();
        }

        void DestroyAllGameObjects()
        {
            DestroyGameObjects(m_Instances
                .Select(x => new GameObjectIdentifier(x.Value.Id, x.Value.StableId, x.Value.GameObject))
                .ToList());
        }

        void StartEnablingGameObjects(List<GameObjectIdentifier> gameObjects)
        {
            if (gameObjects == null || gameObjects.Count == 0)
                return;
            
            m_NbPendingRequests += gameObjects.Count;
            var pipe = m_GameObjectEnablingOutput.Push(this, (object)null, (object)null, new GameObjectEnabling(gameObjects));
            pipe.Success((self, ctx, userCtx, data) =>
            {
                var disablingData = new List<GameObjectIdentifier>();
                var destroyingData = new List<GameObjectIdentifier>();
                
                self.m_NbPendingRequests -= data.GameObjectIds.Count;
                foreach (var go in data.GameObjectIds)
                {
                    var lifecycle = self.m_Instances[go.Id];

                    var state = lifecycle.State;
                    lifecycle.State = LifecycleState.Enabled;

                    switch (state)
                    {
                        case LifecycleState.Creating:
                        case LifecycleState.Enabled:
                        case LifecycleState.Disabled:
                        case LifecycleState.Enabling:
                            break;
                        case LifecycleState.Disabling:
                            lifecycle.State = LifecycleState.Disabling;
                            disablingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                            break;
                        case LifecycleState.Destroying:
                            lifecycle.State = LifecycleState.Destroying;
                            destroyingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                            break;
                    }
                }
                
                self.StartDisablingGameObjects(disablingData);
                self.StartDisablingGameObjects(destroyingData);

                self.TryShutdown();
            });
            pipe.Failure((self, ctx, userCtx, ex) => Debug.LogException(ex));
        }

        void StartDisablingGameObjects(List<GameObjectIdentifier> gameObjects)
        {
            if (gameObjects == null || gameObjects.Count == 0)
                return;
            
            m_NbPendingRequests += gameObjects.Count;
            var pipe = m_GameObjectDisablingOutput.Push(this, (object)null, (object)null, new GameObjectDisabling(gameObjects));
            pipe.Success((self, ctx, userCtx, data) =>
            {
                var enablingData = new List<GameObjectIdentifier>();
                var destroyingData = new List<GameObjectIdentifier>();
                
                self.m_NbPendingRequests -= data.GameObjectIds.Count;
                foreach (var go in data.GameObjectIds)
                {
                    var lifecycle = self.m_Instances[go.Id];

                    var state = lifecycle.State;
                    lifecycle.State = LifecycleState.Disabled;

                    switch (state)
                    {
                        case LifecycleState.Creating:
                        case LifecycleState.Enabled:
                        case LifecycleState.Disabled:
                        case LifecycleState.Disabling:
                            break;
                        case LifecycleState.Enabling:
                            lifecycle.State = LifecycleState.Enabling;
                            enablingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                            break;
                        case LifecycleState.Destroying:
                            lifecycle.State = LifecycleState.Destroying;
                            destroyingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                            break;
                    }
                }
                
                self.StartEnablingGameObjects(enablingData);
                self.StartDestroyingGameObjects(destroyingData);

                self.TryShutdown();
            });
            pipe.Failure((self, ctx, userCtx, ex) => Debug.LogException(ex));
        }

        void StartDestroyingGameObjects(List<GameObjectIdentifier> gameObjects)
        {
            if (gameObjects == null || gameObjects.Count == 0)
                return;
            
            m_NbPendingRequests += gameObjects.Count;
            var pipe = m_GameObjectDestroyingOutput.Push(this, (object)null, (object)null, new GameObjectDestroying(gameObjects));
            pipe.Success((self, ctx, userCtx, data) =>
            {
                foreach (var go in data.GameObjectIds)
                {
                    var lifecycle = self.m_Instances[go.Id];
                    self.m_Instances.Remove(lifecycle.Id);
                    // Todo: Send the GameObject to an actor able to dismantle the GameObject and pool it instead of Destroying it in the job
                    var jobInput = new DestroyJobInput(lifecycle);
                    var rpc = self.m_DelegateJobOutput.Call(self, (object)null, jobInput, new DelegateJob(jobInput, (c, input) =>
                    {
                        var data = (DestroyJobInput)input;
                        Object.Destroy(data.Lifecycle.GameObject);
                        c.SendSuccess(NullData.Null);
                    }));

                    rpc.Success<NullData>((self, ctx, jobInput, _) =>
                    {
                        --self.m_NbPendingRequests;
                        self.TryShutdown();
                    });

                    rpc.Failure((self, ctx, jobInput, ex) =>
                    {
                        --self.m_NbPendingRequests;
                        if (!(ex is OperationCanceledException))
                            Debug.LogException(ex);
                        self.TryShutdown();
                    });
                }
            });
            pipe.Failure((self, ctx, userCtx, ex) => Debug.LogException(ex));
        }

        void UpdateGameObjectsVisibility(List<(GameObjectIdentifier Obj, bool Enable)> data)
        {
            if (data == null || data.Count == 0)
                return;
            
            var disablingData = new List<GameObjectIdentifier>();
            var enablingData = new List<GameObjectIdentifier>();
            
            foreach (var (go, enable) in data)
            {
                var lifecycle = m_Instances[go.Id];
                var state = lifecycle.State;
                if (state == LifecycleState.Destroying)
                    continue;

                if (enable && state != LifecycleState.Enabled)
                    lifecycle.State = LifecycleState.Enabling;
                if (!enable && state != LifecycleState.Disabled)
                    lifecycle.State = LifecycleState.Disabling;

                switch (state)
                {
                    case LifecycleState.Creating:
                    case LifecycleState.Enabling:
                    case LifecycleState.Disabling:
                        break;
                    case LifecycleState.Enabled:
                        if (!enable)
                        {
                            lifecycle.State = LifecycleState.Disabling;
                            disablingData.Add(new GameObjectIdentifier(go.Id, go.StableId, go.GameObject));
                        }
                        break;
                    case LifecycleState.Disabled:
                        if (enable)
                        {
                            lifecycle.State = LifecycleState.Enabling;
                            enablingData.Add(new GameObjectIdentifier(go.Id, go.StableId, go.GameObject));
                        }
                        break;
                }
            }
                        
            StartDisablingGameObjects(disablingData);
            StartEnablingGameObjects(enablingData);
        }

        void RunFuncOnGameObjectJob(RpcContext<RunFuncOnGameObject> ctx, GameObjectLifecycle lifecycle)
        {
            var jobInput = new FuncOnGameObjectJobInput(lifecycle, ctx.Data.Func);
            var jobRpc = m_DelegateJobOutput.Call(this, ctx, (object)null, new DelegateJob(jobInput, (ctx, o) =>
            {
                var input = (FuncOnGameObjectJobInput)o;
                if (input.Lifecycle.GameObject == null)
                {
                    ctx.SendFailure(new MissingGameObjectException());
                    return;
                }
                input.Result = input.Func(input.Lifecycle.GameObject);
                ctx.SendSuccess(input);
            }));
            jobRpc.Success<FuncOnGameObjectJobInput>((self, ctx, userCtx, res) => ctx.SendSuccess(res.Result));
            jobRpc.Failure((self, ctx, userCtx, ex) => ctx.SendFailure(ex));
        }

        void DestroyGameObjects(List<GameObjectIdentifier> data)
        {
            if (data == null || data.Count == 0)
                return;
            
            var disablingData = new List<GameObjectIdentifier>();
            var destroyingData = new List<GameObjectIdentifier>();
            
            foreach (var go in data)
            {
                var lifecycle = m_Instances[go.Id];
                var state = lifecycle.State;
                lifecycle.State = LifecycleState.Destroying;

                switch (state)
                {
                    case LifecycleState.Creating:
                    case LifecycleState.Enabling:
                    case LifecycleState.Disabling:
                    case LifecycleState.Destroying:
                        break;
                    case LifecycleState.Enabled:
                        disablingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                        break;
                    case LifecycleState.Disabled:
                        destroyingData.Add(new GameObjectIdentifier(lifecycle.Id, lifecycle.StableId, lifecycle.GameObject));
                        break;
                }
            }
                        
            StartDisablingGameObjects(disablingData);
            StartDestroyingGameObjects(destroyingData);
        }

        void TryShutdown()
        {
            if (!m_IsShutdown || m_NbPendingRequests != 0)
                return;

            m_PreShutdownOutput.Send(new PreShutdown());
        }

        enum LifecycleState
        {
            Creating,
            Disabled,
            Enabling,
            Enabled,
            Disabling,
            Destroying
        }

        class GameObjectLifecycle
        {
            public DynamicGuid Id;
            public EntryStableGuid StableId;
            public LifecycleState State;
            public int Enabled;
            public GameObject GameObject;

            public GameObjectLifecycle(DynamicGuid id, EntryStableGuid stableId, LifecycleState state, GameObject gameObject)
            {
                Id = id;
                StableId = stableId;
                State = state;
                GameObject = gameObject;
            }
        }

        class DestroyJobInput
        {
            public GameObjectLifecycle Lifecycle;

            public DestroyJobInput(GameObjectLifecycle lifecycle)
            {
                Lifecycle = lifecycle;
            }
        }

        class FuncOnGameObjectJobInput
        {
            public GameObjectLifecycle Lifecycle;
            public Func<GameObject, object> Func;
            public object Result;

            public FuncOnGameObjectJobInput(GameObjectLifecycle lifecycle, Func<GameObject, object> func)
            {
                Lifecycle = lifecycle;
                Func = func;
            }
        }

        class Tracker<T>
        {
            public int NbPendingCalls;
            public List<T> Results = new List<T>();
        }

        public class Settings : ActorSettings
        {
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }

    public class MissingGameObjectException : Exception { }
}
