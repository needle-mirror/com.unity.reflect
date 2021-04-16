using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Reflect.Actor;
using Unity.Reflect.Model;

namespace Unity.Reflect.Streaming
{
    /// <summary>
    ///     Load and cache basic unity resource types, like materials, textures and meshes.
    /// </summary>
    [Actor]
    public class UnityResourceActor
    {
#pragma warning disable 649
        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        NetOutput<ReleaseResource> m_ReleaseResourceOutput;
        RpcOutput<ConvertResource<SyncMesh>> m_ConvertMeshOutput;
        RpcOutput<ConvertResource<SyncMaterial>> m_ConvertMaterialOutput;
        RpcOutput<ConvertResource<SyncTexture>> m_ConvertTextureOutput;
#pragma warning restore 649

        Dictionary<Guid, List<Tracker>> m_Waiters = new Dictionary<Guid, List<Tracker>>();
        Dictionary<Guid, Resource> m_LoadedResources = new Dictionary<Guid, Resource>();

        [RpcInput]
        void OnAcquireUnityResource(RpcContext<AcquireUnityResource> ctx)
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

                RpcOutput<ConvertResource<object>> output;
                object msg;
                // Hack to toggle between different output based on previous request result.
                // Needs a standard way to do this. It could be a native mechanism that does the same as this
                if (syncModel is SyncMesh mesh)
                {
                    output = Unsafe.As<RpcOutput<ConvertResource<object>>>(m_ConvertMeshOutput);
                    msg = new ConvertResource<SyncMesh>(ctx.Data.ResourceData, mesh);
                }
                else if (syncModel is SyncMaterial material)
                {
                    output = Unsafe.As<RpcOutput<ConvertResource<object>>>(m_ConvertMaterialOutput);
                    msg = new ConvertResource<SyncMaterial>(ctx.Data.ResourceData, material);
                }
                else if (syncModel is SyncTexture texture)
                {
                    output = Unsafe.As<RpcOutput<ConvertResource<object>>>(m_ConvertTextureOutput);
                    msg = new ConvertResource<SyncTexture>(ctx.Data.ResourceData, texture);
                }
                else
                {
                    foreach(var tracker in trackers)
                        tracker.Ctx.SendFailure(new Exception($"No converter exists for type {syncModel.GetType()}"));

                    self.m_ReleaseResourceOutput.Send(new ReleaseResource(entry.Id));
                    self.m_Waiters.Remove(entry.Id);
                    return;
                }

                var rpc = output.Call(self, ctx, (object)null, Unsafe.As<ConvertResource<object>>(msg));
                rpc.Success<ConvertedResource>((self, ctx, _, convertedResource) =>
                {
                    var (entry, trackers) = self.GetCommonData(ctx);
                    var resource = new Resource { MainResource = convertedResource.MainResource, Dependencies = convertedResource.Dependencies };

                    self.m_LoadedResources[entry.Id] = resource;

                    foreach (var tracker in trackers)
                        tracker.Ctx.SendSuccess(resource.MainResource);

                    self.m_Waiters.Remove(entry.Id);
                    resource.Count = trackers.Count;

                    self.m_ReleaseResourceOutput.Send(new ReleaseResource(entry.Id));
                });

                rpc.Failure((self, ctx, _, ex) =>
                {
                    var (entry, trackers) = self.GetCommonData(ctx);

                    foreach(var tracker in trackers)
                        tracker.Ctx.SendFailure(ex);

                    self.m_Waiters.Remove(entry.Id);
                    
                    self.m_ReleaseResourceOutput.Send(new ReleaseResource(entry.Id));
                });
            });

            rpc.Failure((self, ctx, _, ex) =>
            {
                var (entry, trackers) = GetCommonData(ctx);

                foreach(var tracker in trackers)
                    tracker.Ctx.SendFailure(ex);

                m_Waiters.Remove(entry.Id);
                    
                m_ReleaseResourceOutput.Send(new ReleaseResource(entry.Id));
            });
        }

        [NetInput]
        void OnReleaseUnityResource(NetContext<ReleaseUnityResource> ctx)
        {
            var resourceId = ctx.Data.ResourceId;
            var resource = m_LoadedResources[resourceId];

            --resource.Count;
            
            foreach (var dependency in resource.Dependencies)
                --m_LoadedResources[dependency].Count;
        }

        bool CompleteIfResourceInCache(RpcContext<AcquireUnityResource> ctx)
        {
            if (m_LoadedResources.TryGetValue(ctx.Data.ResourceData.Id, out var info))
            {
                ++info.Count;
                m_LoadedResources[ctx.Data.ResourceData.Id] = info;
                IncrementDependencies(info);
                ctx.SendSuccess(info.MainResource);
                return true;
            }

            return false;
        }

        (EntryData entry, List<Tracker> waiters) GetCommonData(RpcContext<AcquireUnityResource> ctx)
        {
            return (ctx.Data.ResourceData, m_Waiters[ctx.Data.ResourceData.Id]);
        }

        void IncrementDependencies(Resource resource)
        {
            foreach (var dependency in resource.Dependencies)
            {
                var pair = m_LoadedResources[dependency];
                ++pair.Count;
                m_LoadedResources[dependency] = pair;
            }
        }

        static bool CompleteIfStreamCanceled(RpcContext<AcquireUnityResource> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess<UnityEngine.Object>(null);
                return true;
            }

            return false;
        }

        class Tracker
        {
            public RpcContext<AcquireUnityResource> Ctx;
            public UnityEngine.Object MainResource;
        }

        class Resource
        {
            public UnityEngine.Object MainResource;
            public List<Guid> Dependencies;
            public int Count;
        }
    }

    public class ConvertedResource
    {
        public UnityEngine.Object MainResource;
        public List<Guid> Dependencies;
    }
}
