using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Collections;
using Unity.Reflect.Data;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    public class Boxed<T>
    {
        public T Value;

        public Boxed(T value)
        {
            Value = value;
        }
    }

    public class PreShutdown { }

    public class SyncConnectionStateChanged
    {
        public ConnectionStatus State;

        public SyncConnectionStateChanged(ConnectionStatus state)
        {
            State = state;
        }
    }

    public class RemoteManifestChanged
    {
        public string SourceId;

        public RemoteManifestChanged(string sourceId)
        {
            SourceId = sourceId;
        }
    }

    public class AddSpatialObjectFlag
    {
        public readonly string FlagId;
        public readonly IReadOnlyCollection<DynamicGuid> Objects;

        public AddSpatialObjectFlag(string flagId, IEnumerable<DynamicGuid> objects)
        {
            FlagId = flagId;
            if (objects != null)
                Objects = new HashSet<DynamicGuid>(objects);
            else
                Objects = new HashSet<DynamicGuid>();
        }
    }

    public class RemoveSpatialObjectFlag
    {
        public readonly string FlagId;
        public readonly IReadOnlyCollection<DynamicGuid> Objects;

        public RemoveSpatialObjectFlag(string flagId, IEnumerable<DynamicGuid> objects)
        {
            FlagId = flagId;
            if (objects != null)
                Objects = new HashSet<DynamicGuid>(objects);
            else
                Objects = new HashSet<DynamicGuid>();
        }
    }    
    public class RemoveVisibilityIgnoreFlag
    {
        public string[] FlagIds;
        public RemoveVisibilityIgnoreFlag(params string[] flagIds)
        {
            FlagIds = flagIds;
        }
    }
    public class AddVisibilityIgnoreFlag
    {
        public string[] FlagIds;
        public AddVisibilityIgnoreFlag(params string[] flagIds)
        {
            FlagIds = flagIds;
        }
    }

    public class SpatialPickingArguments
    {
        public ISet<string> ExcludedFlags;
        public readonly Func<ISpatialObject, float> GetDistance;
        public readonly Func<Bounds, ISpatialObject, bool> CheckIntersection;

        public SpatialPickingArguments(ISpatialPickingLogic pickingLogic, params string[] flags)
        {
            if (flags != null && flags.Length > 0)
                ExcludedFlags = new HashSet<string>(flags);
            else
                ExcludedFlags = new HashSet<string>();

            if (pickingLogic == null)
                throw new Exception("Trying to pick without any ISpatialPickingLogic");

            GetDistance = pickingLogic.GetDistance;
            CheckIntersection = pickingLogic.CheckIntersection;
        }
    }

    public class StopStreaming
    {

    }

    public class RestartStreaming
    {

    }

    public class StreamingError
    {
        public DynamicGuid Id;
        public Exception Exception;

        public StreamingError(DynamicGuid id, Exception ex)
        {
            Id = id;
            Exception = ex;
        }
    }

    public class UpdateSetting<TSettings>
        where TSettings : class
    {
        public string Id;
        public string FieldName;
        public object NewValue;

        public UpdateSetting(string id, string fieldName, object newValue)
        {
            Id = id;
            FieldName = fieldName;
            NewValue = newValue;
        }
    }

    public class CreateGameObjectLifecycle
    {
        public List<(DynamicGuid Id, GameObject GameObject)> GameObjectIdList;

        public CreateGameObjectLifecycle(List<(DynamicGuid Id, GameObject GameObject)> gameObjectIdList)
        {
            GameObjectIdList = gameObjectIdList;
        }

        public CreateGameObjectLifecycle(DynamicGuid id, GameObject gameObject)
        {
            GameObjectIdList = new List<(DynamicGuid Id, GameObject GameObject)> { (id, gameObject) };
        }
    }

    public class DestroyGameObjectLifecycle
    {
        public List<DynamicGuid> IdList;

        public DestroyGameObjectLifecycle(List<DynamicGuid> idList)
        {
            IdList = idList;
        }

        public DestroyGameObjectLifecycle(DynamicGuid id)
        {
            IdList = new List<DynamicGuid> { id };
        }
    }

    public class SetAutomaticCacheCleaning
    {
        public bool IsEnabled;

        public SetAutomaticCacheCleaning(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }

    public class MaxLoadedGameObjectsChanged
    {
        public int MaxNbLoadedGameObjects;

        public MaxLoadedGameObjectsChanged(int maxNbLoadedGameObjects)
        {
            MaxNbLoadedGameObjects = maxNbLoadedGameObjects;
        }
    }

    public class RunFuncOnGameObject
    {
        public DynamicGuid Id;
        public EntryStableGuid StableId;
        public Func<GameObject, object> Func;

        public RunFuncOnGameObject(DynamicGuid id, EntryStableGuid stableId, Func<GameObject, object> func)
        {
            Id = id;
            StableId = stableId;
            Func = func;
        }
    }

    public class UpdateStreaming
    {
        /// <summary>
        ///     Currently visible instance based on filter query
        /// </summary>
        public List<DynamicGuid> VisibleInstances;

        /// <summary>
        ///     Instances that were visible in the last update that are not visible in this update
        /// </summary>
        public List<DynamicGuid> HiddenInstancesSinceLastUpdate;

        public UpdateStreaming(List<DynamicGuid> visibleInstances, List<DynamicGuid> hiddenInstanceSinceLastUpdate)
        {
            VisibleInstances = visibleInstances;
            HiddenInstancesSinceLastUpdate = hiddenInstanceSinceLastUpdate;
        }
    }

    public class UpdateVisibility
    {
        /// <summary>
        ///     Currently visible instance based on filter query
        /// </summary>
        public List<DynamicGuid> ShownInstances;

        /// <summary>
        ///     Instances that were visible in the last update that are not visible in this update
        /// </summary>
        public List<DynamicGuid> HiddenInstances;

        public UpdateVisibility(List<DynamicGuid> shownInstances, List<DynamicGuid> hiddenInstances)
        {
            ShownInstances = shownInstances;
            HiddenInstances = hiddenInstances;
        }
    }

    public class CreateGameObject
    {
        public StreamState Stream;
        public DynamicGuid InstanceId;

        public CreateGameObject(StreamState stream, DynamicGuid instanceId)
        {
            Stream = stream;
            InstanceId = instanceId;
        }
    }

    public class AcquireResource
    {
        public StreamState Stream;
        public EntryData ResourceData;

        public AcquireResource(StreamState stream, EntryData resourceData)
        {
            Stream = stream;
            ResourceData = resourceData;
        }
    }
    
    public class AcquireTextureResource : AcquireResource
    {
        public AcquireTextureResource(StreamState stream, EntryData resourceData) : base(stream, resourceData)
        {
        }
    }
    
    public class AcquireMeshResource : AcquireResource
    {
        public AcquireMeshResource(StreamState stream, EntryData resourceData) : base(stream, resourceData)
        {
        }
    }

    public class ReleaseModelResource
    {
        public ISyncModel Model;

        public ReleaseModelResource(ISyncModel model)
        {
            Model = model;
        }
    }

    public class MessageWithEntryData
    {
        public EntryData EntryData;

        public MessageWithEntryData(EntryData entryData)
        {
            EntryData = entryData;
        }
    }

    public class DownloadSyncModel : MessageWithEntryData
    {
        public DownloadSyncModel(EntryData entryData)
            : base(entryData) { }
    }

    public class GetSyncModel : MessageWithEntryData
    {
        public GetSyncModel(EntryData entryData)
            : base(entryData) { }
    }

    public class RemoveAllManifestEntries
    {

    }

    public class ClearBeforeSyncUpdate
    {

    }

    public class GetSpatialManifest
    {
        public GetNodesOptions GetNodesOptions;
        public List<SyncId> NodeIds;
        
        public GetSpatialManifest(GetNodesOptions getNodesOptions) : this(getNodesOptions, new List<SyncId>()) { }

        public GetSpatialManifest(GetNodesOptions getNodesOptions, List<SyncId> nodeIds)
        {
            GetNodesOptions = getNodesOptions;
            NodeIds = nodeIds;
        }
    }

    public class GetManifests
    {
        public GetManifestOptions GetManifestOptions;

        public GetManifests(GetManifestOptions getManifestOptions)
        {
            GetManifestOptions = getManifestOptions;
        }
    }

    public class UpdateEntryDependencies
    {
        public EntryGuid Id;
        public ManifestGuid ManifestId;
        public List<EntryGuid> Dependencies;

        public UpdateEntryDependencies(EntryGuid id, ManifestGuid manifestId, List<EntryGuid> dependencies)
        {
            Id = id;
            ManifestId = manifestId;
            Dependencies = dependencies;
        }
    }

    public class DynamicEntryChanged
    {
        public Delta<DynamicEntry> Delta;

        public DynamicEntryChanged(Delta<DynamicEntry> delta)
        {
            Delta = delta;
        }
    }

    public class ConvertToGameObject
    {
        public StreamState Stream;
        public ManifestGuid ManifestId;
        public EntryData InstanceData;
        public SyncObjectInstance SyncInstance;
        public EntryData ObjectData;
        public SyncObject SyncObject;

        public ConvertToGameObject(StreamState stream, ManifestGuid manifestId, EntryData instanceData, SyncObjectInstance syncInstance, EntryData objectData, SyncObject syncObject)
        {
            Stream = stream;
            ManifestId = manifestId;
            InstanceData = instanceData;
            SyncInstance = syncInstance;
            ObjectData = objectData;
            SyncObject = syncObject;
        }
    }

    public class AcquireUnityResource
    {
        public StreamState Stream;
        public EntryData ResourceData;

        public AcquireUnityResource(StreamState stream, EntryData resourceData)
        {
            Stream = stream;
            ResourceData = resourceData;
        }
    }

    public class AcquireUnityMaterial : AcquireUnityResource
    {
        public ManifestGuid ManifestId;

        public AcquireUnityMaterial(StreamState stream, ManifestGuid manifestId, EntryData resourceData)
            : base(stream, resourceData)
        {
            ManifestId = manifestId;
        }
    }
    
    public class AcquireUnityTexture : AcquireUnityResource
    {
        public AcquireUnityTexture(StreamState stream, EntryData resourceData) : base(stream, resourceData)
        {
        }
    }
    
    public class AcquireUnityMesh : AcquireUnityResource
    {
        public AcquireUnityMesh(StreamState stream, EntryData resourceData) : base(stream, resourceData)
        {
        }
    }

    public class ReleaseUnityMaterial
    {
        public Material Material;

        public ReleaseUnityMaterial(Material material)
        {
            Material = material;
        }
    }

    public class ReleaseUnityMesh
    {
        public Mesh Resource;

        public ReleaseUnityMesh(Mesh unityResource)
        {
            Resource = unityResource;
        }
    }
    
    public class ReleaseUnityTexture
    {
        public Texture Resource;

        public ReleaseUnityTexture(Texture unityResource)
        {
            Resource = unityResource;
        }
    }

    public class GetProjectWideDefaultMaterial { }

    public class BuildGameObject
    {
        public StreamState Stream;
        public EntryData InstanceData;
        public SyncObjectInstance Instance;
        public SyncObject Object;
        public Material DefaultMaterial;

        // Todo: Should use Guid later instead of StreamKey, when we rewrite the internal SyncObjectImporter
        public Dictionary<StreamKey, Mesh> Meshes;
        public Dictionary<StreamKey, Material> Materials;

        public BuildGameObject(StreamState stream, EntryData instanceData, SyncObjectInstance syncInstance, SyncObject syncObject, Material defaultMaterial, Dictionary<StreamKey, Mesh> meshes, Dictionary<StreamKey, Material> materials)
        {
            Stream = stream;
            InstanceData = instanceData;
            Instance = syncInstance;
            Object = syncObject;
            DefaultMaterial = defaultMaterial;
            Meshes = meshes;
            Materials = materials;
        }
    }

    public class ConvertResource<TResource>
        where TResource : class
    {
        public EntryData Entry;
        public TResource Resource;

        public ConvertResource(EntryData entry, TResource resource)
        {
            Entry = entry;
            Resource = resource;
        }
    }

    public class ConvertSyncMaterial : ConvertResource<SyncMaterial>
    {
        public List<EntryData> TextureEntries;

        public ConvertSyncMaterial(EntryData entry, SyncMaterial syncMaterial, List<EntryData> textureEntries)
            : base(entry, syncMaterial)
        {
            TextureEntries = textureEntries;
        }
    }

    public class MaterialCreating
    {
        public EntryGuid EntryId;
        public ManifestGuid ManifestId;
        public Material Material;

        public MaterialCreating(EntryGuid entryId, ManifestGuid manifestId, Material material)
        {
            EntryId = entryId;
            ManifestId = manifestId;
            Material = material;
        }
    }

    public class MaterialDestroying
    {
        public EntryGuid EntryId;
        public ManifestGuid ManifestId;
        public Material Material;

        public MaterialDestroying(EntryGuid entryId, ManifestGuid manifestId, Material material)
        {
            EntryId = entryId;
            ManifestId = manifestId;
            Material = material;
        }
    }

    public class ResourceCreating<TResource>
    {
        public EntryGuid EntryId;
        public TResource Resource;

        public ResourceCreating(EntryGuid entryId, TResource resource)
        {
            EntryId = entryId;
            Resource = resource;
        }
    }

    public class ResourceDestroying<TResource>
    {
        public EntryGuid EntryId;
        public TResource Resource;

        public ResourceDestroying(EntryGuid entryId, TResource resource)
        {
            EntryId = entryId;
            Resource = resource;
        }
    }

    public class DelegateJob
    {
        public object JobInput;
        public Action<RpcContext<DelegateJob>, object> Job;

        public DelegateJob(object jobInput, Action<RpcContext<DelegateJob>, object> job)
        {
            JobInput = jobInput;
            Job = job;
        }
    }

    public class ForwardPressure
    {
        public int NbMaxItems;
    }

    public class UpdateManifests
    {
    }

    public class SpatialDataChanged
    {
        public Delta<DynamicEntry> Delta;

        public SpatialDataChanged(Delta<DynamicEntry> delta)
        {
            Delta = delta;
        }
    }

    public class SyncNodeDataChanged
    {
        public Delta<DynamicEntry> Delta;

        public SyncNodeDataChanged(Delta<DynamicEntry> delta)
        {
            Delta = delta;
        }
    }

    public class SyncNodesAdded
    {
        public List<SyncNode> SyncNodes;
        public string HlodSourceId;

        public SyncNodesAdded(List<SyncNode> syncNodes, string hlodSourceId)
        {
            SyncNodes = syncNodes;
            HlodSourceId = hlodSourceId;
        }
    }

    public class PatchEntryIds
    {
        public Delta<EntryData> Delta;
        public PatchEntryIds(Delta<EntryData> delta)
        {
            Delta = delta;
        }
    }

    public class CameraDataChanged
    {
        public Vector3 Position;
        public float NearClipPlane;
        public float FarClipPlane;
        public Matrix4x4 ViewProjectionMatrix;
        public Vector3 Forward;
        public Plane ForwardPlane;
        public Plane[] FrustumPlanes;
        public float FieldOfView;
        public int ScreenHeight;

        public float SqrFar => FarClipPlane * FarClipPlane;

        public CameraDataChanged(Vector3 position, float nearClipPlane, float farClipPlane, Matrix4x4 viewProjectionMatrix, 
            Vector3 forward, Plane[] frustumPlanes, float fieldOfView, int screenHeight)
        {
            Position = position;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
            ViewProjectionMatrix = viewProjectionMatrix;
            Forward = forward;
            ForwardPlane = new Plane(forward, position);
            FrustumPlanes = frustumPlanes;
            FieldOfView = fieldOfView;
            ScreenHeight = screenHeight;
        }
    }

    public class FpsDataChanged
    {
        public int Fps;

        public FpsDataChanged(int fps)
        {
            Fps = fps;
        }
    }

    public class SearchSpatialCollection
    {
        public Func<ISpatialObject, bool> Predicate;
        public Func<ISpatialObject, float> Prioritizer;

        public SearchSpatialCollection(Func<ISpatialObject, bool> predicate, Func<ISpatialObject, float> prioritizer)
        {
            Predicate = predicate;
            Prioritizer = prioritizer;
        }
    }

    public class EntryDataChanged
    {
        public Delta<EntryData> Delta;
        public List<NewManifest> NewManifestIds;

        public EntryDataChanged(Delta<EntryData> delta, List<NewManifest> newManifestIds)
        {
            Delta = delta;
            NewManifestIds = newManifestIds;
        }
    }

    public struct NewManifest
    {
        public ManifestStableGuid StableId;
        public ManifestGuid ManifestId;

        public NewManifest(ManifestStableGuid stableId, ManifestGuid manifestId)
        {
            StableId = stableId;
            ManifestId = manifestId;
        }
    }

    public class ExecuteSyncEvent
    {
        public object Event;

        public ExecuteSyncEvent(object e)
        {
            Event = e;
        }
    }

    public class ToggleGameObject
    {
        /// <summary>
        ///     A positive value of x enable the GameObject x times, a negative value of x disable the GameObject x times.
        ///     The state of the GameObject toggles when the sum of all requests pass through the value 1.
        ///     SetActive is called with true when the sum goes from 0 to 1, and false when the sum goes from 1 to 0.
        /// </summary>
        public List<(DynamicGuid Id, int NbEnabled)> ToggleGameObjectList;

        /// <summary>
        /// </summary>
        /// <param name="toggleGameObjectList">See <see cref="ToggleGameObjectList"/> description</param>
        public ToggleGameObject(List<(DynamicGuid Id, int NbEnabled)> toggleGameObjectList)
        {
            ToggleGameObjectList = toggleGameObjectList;
        }

        public ToggleGameObject(DynamicGuid id, int nbEnabled)
        {
            ToggleGameObjectList = new List<(DynamicGuid Id, int NbEnabled)> { (id, nbEnabled) };
        }
    }

    public class GameObjectMessage
    {
        public List<GameObjectIdentifier> GameObjectIds;

        public GameObjectMessage(List<GameObjectIdentifier> gameObjectIds)
        {
            GameObjectIds = gameObjectIds;
        }

        public GameObjectMessage(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject)
        {
            GameObjectIds = new List<GameObjectIdentifier> { new GameObjectIdentifier(id, stableId, gameObject) };
        }
    }

    public class GameObjectCreating : GameObjectMessage
    {
        public GameObjectCreating(List<GameObjectIdentifier> gameObjectIds) : base(gameObjectIds) { }
        public GameObjectCreating(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject): base(id, stableId, gameObject) { }
    }

    public class GameObjectDestroying : GameObjectMessage
    {
        public GameObjectDestroying(List<GameObjectIdentifier> gameObjectIds) : base(gameObjectIds) { }
        public GameObjectDestroying(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject): base(id, stableId, gameObject) { }
    }

    public class GameObjectEnabling : GameObjectMessage
    {
        public GameObjectEnabling(List<GameObjectIdentifier> gameObjectIds) : base(gameObjectIds) { }
        public GameObjectEnabling(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject): base(id, stableId, gameObject) { }
    }

    public class GameObjectDisabling : GameObjectMessage
    {
        public GameObjectDisabling(List<GameObjectIdentifier> gameObjectIds) : base(gameObjectIds) { }
        public GameObjectDisabling(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject): base(id, stableId, gameObject) { }
    }

    public class MemoryStateChanged
    {
        /// <summary>The amount of memory that is considered dangerous.</summary>
        public long CriticalThreshold;

        /// <summary>Threshold from which receivers should start reducing the total amount of memory.</summary>
        public long HighThreshold;

        /// <summary>Threshold from which receivers should start cleaning up unused resources.</summary>
        public long MediumThreshold;

        /// <summary>The amount of memory that is used by the application. The difference between this and <see cref="TotalAppMemory"/> is the reserved memory.</summary>
        public long UsedAppMemory;

        /// <summary>The total amount of memory consumed by the application.</summary>
        public long TotalAppMemory;

        /// <summary>Indicates if the receiver should try to remove all allocations to free up as much memory as it can.</summary>
        public bool IsMemoryLevelTooHigh;

        public MemoryStateChanged(long criticalThreshold, long highThreshold, long mediumThreshold, long usedAppMemory, long totalAppMemory, bool isMemoryLevelTooHigh)
        {
            CriticalThreshold = criticalThreshold;
            HighThreshold = highThreshold;
            MediumThreshold = mediumThreshold;
            UsedAppMemory = usedAppMemory;
            TotalAppMemory = totalAppMemory;
            IsMemoryLevelTooHigh = isMemoryLevelTooHigh;
        }
    }

    public class CleanAfterCriticalMemory
    {
    }

    public class SetGameObjectDependencies
    {
        public GameObject GameObject;
        public List<Mesh> Meshes;
        public List<Material> Materials;

        public SetGameObjectDependencies(GameObject gameObject, List<Mesh> meshes, List<Material> materials)
        {
            GameObject = gameObject;
            Meshes = meshes;
            Materials = materials;
        }
    }

    public class AcquireEntryData
    {
        public EntryGuid EntryId;

        public AcquireEntryData(EntryGuid entryId)
        {
            EntryId = entryId;
        }
    }

    public class AcquireEntryDataFromModelData
    {
        public ManifestGuid ManifestId;
        public PersistentKey IdInSource;

        public AcquireEntryDataFromModelData(ManifestGuid manifestId, PersistentKey idInSource)
        {
            ManifestId = manifestId;
            IdInSource = idInSource;
        }
    }

    public class GetStableId<TKey>
    {
        public TKey Key;

        public GetStableId(TKey key)
        {
            Key = key;
        }
    }

    public class AcquireDynamicEntry
    {
        public DynamicGuid DynamicId;

        public AcquireDynamicEntry(DynamicGuid dynamicId)
        {
            DynamicId = dynamicId;
        }
    }

    public class TransformObjectBounds
    {
        public List<DynamicGuid> Ids;
        public System.Numerics.Matrix4x4 TransformMatrix;

        public TransformObjectBounds(List<DynamicGuid> ids, System.Numerics.Matrix4x4 transformMatrix)
        {
            Ids = ids;
            TransformMatrix = transformMatrix;
        }
    }

    public class GlobalBoundsUpdated
    {
        public Bounds GlobalBounds;

        public GlobalBoundsUpdated(Bounds globalBounds)
        {
            GlobalBounds = globalBounds;
        }
    }

    public class SceneZonesChanged
    {
        public List<SceneZone> Zones;

        public SceneZonesChanged(List<SceneZone> zones)
        {
            Zones = zones;
        }
    }

    public struct SceneZone
    {
        public Aabb Bounds;
        public ZoneDensity Density;

        /// <summary>
        /// Indicates whether the system had enough information to generate a reliable
        /// data for this <see cref="SceneZone"/> or not. Unreliable data is there mainly
        /// as a fallback when no reliable data is available yet.
        /// </summary>
        public bool IsReliable;

        public SceneZone(Aabb bounds, ZoneDensity density, bool isReliable)
        {
            Bounds = bounds;
            Density = density;
            IsReliable = isReliable;
        }
    }
    
    public struct ZoneDensity
    {
        public double SpaceVolume;
        public double VolumeSum;

        public ZoneDensity(double spaceVolume, double volumeSum)
        {
            SpaceVolume = spaceVolume;
            VolumeSum = volumeSum;
        }
    }

    public class GetDynamicIds
    {
        public EntryStableGuid StableId;

        public GetDynamicIds(EntryStableGuid stableId)
        {
            StableId = stableId;
        }
    }

    public class AssetCountChanged
    {
        public ItemCount ItemCount;

        public AssetCountChanged(ItemCount itemCount)
        {
            ItemCount = itemCount;
        }
    }

    public class InstanceCountChanged
    {
        public ItemCount ItemCount;

        public InstanceCountChanged(ItemCount itemCount)
        {
            ItemCount = itemCount;
        }
    }

    public class GameObjectCountChanged
    {
        public ItemCount ItemCount;

        public GameObjectCountChanged(ItemCount itemCount)
        {
            ItemCount = itemCount;
        }
    }

    public class DownloadProgressed
    {
        public int NbInCache;
        public int Total;

        public DownloadProgressed(int nbInCache, int total)
        {
            NbInCache = nbInCache;
            Total = total;
        }
    }

    public class StreamingProgressed
    {
        public int NbStreamed;
        public int Total;

        public StreamingProgressed(int nbStreamed, int total)
        {
            NbStreamed = nbStreamed;
            Total = total;
        }
    }

    public class ReloadProject
    {

    }

    public class CommandMessage
    {
        public Action Command;

        public CommandMessage(Action command)
        {
            Command = command;
        }
    }

    public class DebugDrawGizmos : CommandMessage
    {
        public DebugDrawGizmos(Action command) : base(command) { }
    }

    public class DebugGui : CommandMessage
    {
        public DebugGui(Action command) : base(command) { }
    }
    
    public struct ItemCount : IEquatable<ItemCount>
    {
        public int NbAdded;
        public int NbRemoved;
        public int NbChanged;

        public static bool operator ==(ItemCount a, ItemCount b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ItemCount a, ItemCount b)
        {
            return !(a == b);
        }

        public bool Equals(ItemCount other)
        {
            return NbAdded == other.NbAdded &&
                NbChanged == other.NbChanged &&
                NbRemoved == other.NbRemoved;
        }

        public override bool Equals(object obj)
        {
            return obj is ItemCount other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = NbAdded;
                hashCode = (hashCode * 397) ^ NbChanged;
                hashCode = (hashCode * 397) ^ NbRemoved;
                return hashCode;
            }
        }
    }

    public class Delta<T>
    {
        public List<T> Added = new List<T>();
        public List<T> Removed = new List<T>();
        public List<(T Prev, T Next)> Changed = new List<(T, T)>();

        public bool IsEmpty()
        {
            return Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
        }
    }

    public class Diff<T>
    {
        public List<T> Added = new List<T>();
        public List<T> Removed = new List<T>();

        public bool IsEmpty()
        {
            return Added.Count == 0 && Removed.Count == 0;
        }
    }

    public class HashDelta<TKey, TValue>
    {
        public Dictionary<TKey, TValue> Added;
        public Dictionary<TKey, TValue> Removed;
        public Dictionary<TKey, (TValue Prev, TValue Next)> Changed;
    }

    public class GameObjectIdentifier
    {
        public DynamicGuid Id;
        public EntryStableGuid StableId;
        public GameObject GameObject;

        public GameObjectIdentifier(DynamicGuid id, EntryStableGuid stableId, GameObject gameObject)
        {
            Id = id;
            StableId = stableId;
            GameObject = gameObject;
        }
    }
}
