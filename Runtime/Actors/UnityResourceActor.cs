using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    /// <summary>
    ///     Load and cache basic unity resource types, like textures and meshes.
    /// </summary>
    [Actor]
    public abstract class UnityResourceActor<TResource, TModel, TAcquireResource> where TResource : UnityEngine.Object where TModel : class where TAcquireResource : AcquireUnityResource
    {
#pragma warning disable 649
        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        NetOutput<ReleaseModelResource> m_ReleaseModelResourceOutput;
        RpcOutput<DelegateJob> m_DelegateJobOutput;
        PipeOutput<ResourceCreating<TResource>> m_ResourceCreatingOutput;
        PipeOutput<ResourceDestroying<TResource>> m_ResourceDestroyingOutput;
#pragma warning restore 649

        bool m_IsAutomaticCleaningEnabled;
        
        Dictionary<EntryGuid, List<Tracker>> m_Waiters = new Dictionary<EntryGuid, List<Tracker>>();
        Records m_Records = new Records();

        public void Shutdown()
        {
            m_Records.DestroyAll();
        }
        
        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            if (ctx.Data.IsMemoryLevelTooHigh)
                DestroyAllResources();
        }

        [PipeInput]
        void OnCleanAfterCriticalMemory(PipeContext<CleanAfterCriticalMemory> ctx)
        {
            DestroyAllResources();
            ctx.Continue();
        }

        [NetInput]
        void OnSetAutomaticCacheCleaning(NetContext<SetAutomaticCacheCleaning> ctx)
        {
            m_IsAutomaticCleaningEnabled = ctx.Data.IsEnabled;

            if (m_IsAutomaticCleaningEnabled)
            {
                var destroyingResources = m_Records.RemoveUnused();

                DestroyManyResources(destroyingResources);
            }
        }
        
        void DestroyAllResources()
        {
            DestroyManyResources(m_Records.Resources);
            m_Records.Clear();
        }

        void DestroySingleResource(Resource resource)
        {
            var pipe = m_ResourceDestroyingOutput.PushCritical(this, (object)null, resource, new ResourceDestroying<TResource>(resource.Id, (TResource)resource.MainResource));
            pipe.Success((self, ctx, resource, msg) => CompleteDestroyingResource(resource));
            pipe.Failure((self, ctx, resource, ex) => CompleteDestroyingResource(resource));
        }

        void CompleteDestroyingResource(Resource resource)
        {
            var rpc = m_DelegateJobOutput.Call(this, (object)null, (object)null, new DelegateJob(resource, (c, input) =>
            {
                var resource = (Resource)input;
                if (resource.MainResource != null)
                    Object.Destroy(resource.MainResource);

                c.SendSuccess(NullData.Null);
            }));
            rpc.Success<NullData>((self, ctx, userCtx, _) => {});
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                if (!(ex is OperationCanceledException))
                    Debug.LogException(ex);
            });
        }

        void DestroyManyResources(Dictionary<EntryGuid, Resource> resources)
        {
            var countDown = new CountDownTracker<Dictionary<EntryGuid, Resource>>(0, resources);
            foreach (var kv in countDown.Data)
            {
                ++countDown.NbRemaining;
                
                var pipe = m_ResourceDestroyingOutput.PushCritical(this, (object)null, countDown, new ResourceDestroying<TResource>(kv.Key, (TResource)kv.Value.MainResource));
                pipe.Success((self, ctx, countDown, msg) => TryCompleteDestroyingResources(countDown));
                pipe.Failure((self, ctx, countDown, ex) => TryCompleteDestroyingResources(countDown));
            }
        }

        void TryCompleteDestroyingResources(CountDownTracker<Dictionary<EntryGuid, Resource>> countDown)
        {
            TryCompleteDestroyingResources(countDown,
                (c, input) =>
                {
                    var data = (DestroyJobInput)input;
                    foreach (var resource in data.Resources.Select(x => x.Value))
                    {
                        if (resource.MainResource != null)
                            UnityEngine.Object.Destroy(resource.MainResource);
                    }

                    c.SendSuccess(NullData.Null);
                });
        }

        void TryCompleteDestroyingResources(CountDownTracker<Dictionary<EntryGuid, Resource>> countDown, Action<RpcContext<DelegateJob>, object> job)
        {
            --countDown.NbRemaining;
            if (countDown.NbRemaining > 0)
                return;

            var jobInput = new DestroyJobInput(countDown.Data);
            var rpc = m_DelegateJobOutput.Call(this, (object)null, (object)null, new DelegateJob(jobInput, job));
            rpc.Success<NullData>((self, ctx, jobInput, _) => {});
            rpc.Failure((self, ctx, jobInput, ex) => Debug.LogException(ex));
        }

        protected void AcquireResource(RpcContext<TAcquireResource> ctx, RpcOutput<ConvertResource<TModel>> convertResourceOutput)
        {
            if (CompleteIfStreamCanceled(ctx))
                return;

            if (CompleteIfResourceInCache(ctx))
                return;

            var tracker = new Tracker { Ctx = ctx };

            if (m_Waiters.TryGetValue(ctx.Data.ResourceData.Id, out var trackers))
            {
                trackers.Add(tracker);
                return;
            }

            m_Waiters.Add(ctx.Data.ResourceData.Id, new List<Tracker> { tracker });

            // From this point a request won't be cancellable because it's too complicated to track interlaced resource requests.
            // We may implement it later but the overhead may outweigh the benefits
            var rpc = m_AcquireResourceOutput.Call(this, ctx, (object)null, new AcquireResource(new StreamState(), ctx.Data.ResourceData));
            rpc.Success<ISyncModel>((self, ctx, _, syncModel) =>
            {
                var (entry, trackers) = self.GetCommonData(ctx);

                if (syncModel is TModel model)
                {
                    ProcessResourceConversion(self, ctx, convertResourceOutput, new ConvertResource<TModel>(ctx.Data.ResourceData, model), syncModel);
                }
                else
                {
                    foreach(var tracker in trackers)
                        tracker.Ctx.SendFailure(new Exception($"No converter exists for type {syncModel.GetType()}"));

                    self.m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(syncModel));
                    self.m_Waiters.Remove(entry.Id);
                }
            });

            rpc.Failure((self, ctx, _, ex) =>
            {
                var (entry, trackers) = GetCommonData(ctx);

                foreach(var tracker in trackers)
                    tracker.Ctx.SendFailure(ex);

                m_Waiters.Remove(entry.Id);
            });
        }

        static void ProcessResourceConversion<TSync>(UnityResourceActor<TResource, TModel, TAcquireResource> self, RpcContext<TAcquireResource> ctx, RpcOutput<ConvertResource<TSync>> output, ConvertResource<TSync> msg, ISyncModel model)
            where TSync : class
        {
            var rpc = output.Call(self, ctx, model, msg);
            rpc.Success<ConvertedResource<NullData>>((self, ctx, model, convertedResource) =>
            {
                var (entry, trackers) = self.GetCommonData(ctx);

                var resource = new Resource(entry.Id, convertedResource.MainResource, trackers.Count);
                self.m_Records.Add(entry.Id, resource);

                var pipe = self.m_ResourceCreatingOutput.Push(self, ctx, resource, new ResourceCreating<TResource>(entry.Id, (TResource)resource.MainResource));
                pipe.Success((self, ctx, resource, msg) => CompleteConvertedResource(self, ctx, resource, model));
                pipe.Failure((self, ctx, resource, ex) =>
                {
                    Debug.LogException(ex);
                    CompleteConvertedResource(self, ctx, resource, model);
                });
            });

            rpc.Failure((self, ctx, model, ex) =>
            {
                var (entry, trackers) = self.GetCommonData(ctx);

                foreach(var tracker in trackers)
                    tracker.Ctx.SendFailure(ex);

                self.m_Waiters.Remove(entry.Id);
                self.m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(model));
            });
        }

        static void CompleteConvertedResource(UnityResourceActor<TResource, TModel, TAcquireResource> self, RpcContext<TAcquireResource> ctx, Resource resource, ISyncModel model)
        {
            var (entry, trackers) = self.GetCommonData(ctx);

            foreach (var tracker in trackers)
                tracker.Ctx.SendSuccess(resource.MainResource);

            self.m_Waiters.Remove(entry.Id);
            self.m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(model));
        }

        bool CompleteIfResourceInCache(RpcContext<TAcquireResource> ctx)
        {
            if (!m_Records.TryIncrement(ctx.Data.ResourceData.Id, out var resource))
                return false;

            ctx.SendSuccess(resource.MainResource);
            return true;
        }

        protected void ReleaseUnityResource(TResource unityResource)
        {
            if (!m_Records.TryDecrement(unityResource, out var resource))
                return;

            if (m_IsAutomaticCleaningEnabled && resource.Count == 0)
            {
                m_Records.Remove(resource);
                DestroySingleResource(resource);
            }
        }

        (EntryData entry, List<Tracker> waiters) GetCommonData(RpcContext<TAcquireResource> ctx)
        {
            return (ctx.Data.ResourceData, m_Waiters[ctx.Data.ResourceData.Id]);
        }

        static bool CompleteIfStreamCanceled(RpcContext<TAcquireResource> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess(NullData.Null);
                return true;
            }

            return false;
        }

        class Tracker
        {
            public RpcContext<TAcquireResource> Ctx;
        }

        class Resource
        {
            public EntryGuid Id;
            public Object MainResource;
            public int Count;

            public Resource(EntryGuid id, Object mainResource, int count)
            {
                Id = id;
                MainResource = mainResource;
                Count = count;
            }
        }

        class CountDownTracker<T>
        {
            public int NbRemaining;
            public T Data;

            public CountDownTracker(int nbRemaining, T data)
            {
                NbRemaining = nbRemaining;
                Data = data;
            }
        }

        class DestroyJobInput
        {
            public Dictionary<EntryGuid, Resource> Resources;

            public DestroyJobInput(Dictionary<EntryGuid, Resource> resources)
            {
                Resources = resources;
            }
        }

        class Records
        {
            Dictionary<Mesh, Resource> m_Meshes = new Dictionary<Mesh, Resource>(new Comparer());
            Dictionary<Texture, Resource> m_Textures = new Dictionary<Texture, Resource>(new Comparer());

            public Dictionary<EntryGuid, Resource> Resources = new Dictionary<EntryGuid, Resource>();

            public void DestroyAll()
            {
                foreach (var resource in Resources.Select(x => x.Value))
                {
                    if (resource.MainResource != null)
                        Object.Destroy(resource.MainResource);
                }

                Resources.Clear();
                m_Meshes.Clear();
                m_Textures.Clear();
            }

            public Dictionary<EntryGuid, Resource> RemoveUnused()
            {
                var destroyingResources = new Dictionary<EntryGuid, Resource>();
                foreach (var kv in Resources)
                {
                    var res = kv.Value;
                    if (res.Count == 0)
                        destroyingResources.Add(kv.Key, kv.Value);
                }

                foreach (var kv in destroyingResources)
                {
                    Resources.Remove(kv.Key);
                    if (kv.Value.MainResource is Mesh mesh)
                        m_Meshes.Remove(mesh);
                    else if (kv.Value.MainResource is Texture texture)
                        m_Textures.Remove(texture);
                }

                return destroyingResources;
            }

            public void Clear()
            {
                Resources = new Dictionary<EntryGuid, Resource>();
                m_Meshes = new Dictionary<Mesh, Resource>();
                m_Textures = new Dictionary<Texture, Resource>();
            }

            public void Add(EntryGuid id, Resource resource)
            {
                Resources.Add(id, resource);
                if (resource.MainResource is Mesh mesh)
                    m_Meshes.Add(mesh, resource);
                else if (resource.MainResource is Texture texture)
                    m_Textures.Add(texture, resource);
            }

            public void Remove(Resource resource)
            {
                Resources.Remove(resource.Id);
                if (resource.MainResource is Mesh mesh)
                    m_Meshes.Remove(mesh);
                else if (resource.MainResource is Texture texture)
                    m_Textures.Remove(texture);
            }

            public bool TryIncrement(EntryGuid id, out Resource resource)
            {
                if (!Resources.TryGetValue(id, out resource))
                    return false;

                ++resource.Count;
                return true;
            }

            public bool TryDecrement(EntryGuid id, out Resource resource)
            {
                if (!Resources.TryGetValue(id, out resource))
                    return false;

                --resource.Count;
                return true;
            }
            
            public bool TryDecrement<TResource>(TResource unityResource, out Resource resource)
            {
                if (unityResource is Mesh mesh)
                {
                    return TryDecrement(mesh, out resource);
                }
                
                if (unityResource is Texture texture)
                {
                    return TryDecrement(texture, out resource);
                }

                resource = null;
                return false;
            }

            bool TryDecrement(Mesh mesh, out Resource resource)
            {
                if (!m_Meshes.TryGetValue(mesh, out resource))
                    return false;

                --resource.Count;
                return true;
            }

            bool TryDecrement(Texture texture, out Resource resource)
            {
                if (!m_Textures.TryGetValue(texture, out resource))
                    return false;

                --resource.Count;
                return true;
            }
        }

        class Comparer : IEqualityComparer<Object>
        {
            public bool Equals(Object x, Object y) => ReferenceEquals(x, y);
            public int GetHashCode(Object obj) => obj.GetHashCode();
        }
    }

    public class ConvertedResource<T>
    {
        public Object MainResource;
        public List<T> Dependencies;

        public ConvertedResource(Object mainResource, List<T> dependencies)
        {
            MainResource = mainResource;
            Dependencies = dependencies;
        }
    }
}
