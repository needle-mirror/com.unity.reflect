using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Streaming
{
    public class NullData
    {
        public static NullData Null = new NullData();
    }

    public class PickFromRay
    {
        public Ray Ray;

        public PickFromRay(Ray ray)
        {
            Ray = ray;
        }
    }

    public class PickFromSamplePoints
    {
        public Vector3[] SamplePoints;
        public int Count;

        public PickFromSamplePoints(Vector3[] samplePoints, int count)
        {
            SamplePoints = samplePoints;
            Count = count;
        }
    }

    public class PickFromDistance
    {
        public int Distance;

        public PickFromDistance(int distance)
        {
            Distance = distance;
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

    public class SetHighlightVisibility
    {
        public string GroupKey;
        public string FilterKey;
        public bool IsVisible;

        public SetHighlightVisibility(string groupKey, string filterKey, bool isVisible)
        {
            GroupKey = groupKey;
            FilterKey = filterKey;
            IsVisible = isVisible;
        }
    }

    public class SetHighlightFilter
    {
        public string GroupKey;
        public string FilterKey;

        public SetHighlightFilter(string groupKey, string filterKey)
        {
            GroupKey = groupKey;
            FilterKey = filterKey;
        }
    }

    public class GetFilterStates
    {
        public string GroupKey;

        public GetFilterStates(string groupKey)
        {
            GroupKey = groupKey;
        }
    }

    public struct FilterState
    {
        public string Key;
        public bool IsVisible;
        public bool isHighlighted;
    }

    public class UpdateStreaming
    {
        /// <summary>
        ///     Currently visible instance based on filter query
        /// </summary>
        public List<Guid> AddedInstances;

        /// <summary>
        ///     Instances that were visible in the last update that are not visible in this update
        /// </summary>
        public List<Guid> RemovedInstances;

        public UpdateStreaming(List<Guid> addedInstances, List<Guid> removedInstances)
        {
            AddedInstances = addedInstances;
            RemovedInstances = removedInstances;
        }
    }

    public class UpdateVisibility
    {
        /// <summary>
        ///     Currently visible instance based on filter query
        /// </summary>
        public List<Guid> ShownInstances;

        /// <summary>
        ///     Instances that were visible in the last update that are not visible in this update
        /// </summary>
        public List<Guid> HiddenInstances;

        public UpdateVisibility(List<Guid> shownInstances, List<Guid> hiddenInstances)
        {
            ShownInstances = shownInstances;
            HiddenInstances = hiddenInstances;
        }
    }

    public class CreateGameObject
    {
        public StreamState Stream;
        public Guid InstanceId;

        public CreateGameObject(StreamState stream, Guid instanceId)
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

    public class ReleaseResource
    {
        public Guid ResourceId;

        public ReleaseResource(Guid resourceId)
        {
            ResourceId = resourceId;
        }
    }

    public class GetSyncModel
    {
        public EntryData EntryData;

        public GetSyncModel(EntryData entryData)
        {
            EntryData = entryData;
        }
    }

    public class GetManifests { }

    public class ConvertToGameObject
    {
        public StreamState Stream;
        public EntryData InstanceData;
        public SyncObjectInstance SyncInstance;
        public SyncObject SyncObject;

        public ConvertToGameObject(StreamState stream, EntryData instanceData, SyncObjectInstance syncInstance, SyncObject syncObject)
        {
            Stream = stream;
            InstanceData = instanceData;
            SyncInstance = syncInstance;
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

    public class ReleaseUnityResource
    {
        public Guid ResourceId;

        public ReleaseUnityResource(Guid resourceId)
        {
            ResourceId = resourceId;
        }
    }

    public class BuildGameObject
    {
        public StreamState Stream;
        public EntryData InstanceData;
        public SyncObjectInstance Instance;
        public SyncObject Object;

        // Todo: Should use EntryIdentifier later instead of StreamKey, when we rewrite the SyncObjectImporter
        public Dictionary<StreamKey, Mesh> Meshes;
        public Dictionary<StreamKey, Material> Materials;

        public BuildGameObject(StreamState stream, EntryData instanceData, SyncObjectInstance syncInstance, SyncObject syncObject, Dictionary<StreamKey, Mesh> meshes, Dictionary<StreamKey, Material> materials)
        {
            Stream = stream;
            Instance = syncInstance;
            Object = syncObject;
            Meshes = meshes;
            Materials = materials;
            InstanceData = instanceData;
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
        public List<EntryData> Added;
        public List<EntryData> Removed;
        public List<ModifiedEntry> Changed;

        public SpatialDataChanged(List<EntryData> added, List<EntryData> removed, List<ModifiedEntry> changed)
        {
            Added = added;
            Removed = removed;
            Changed = changed;
        }

        public struct ModifiedEntry
        {
            public EntryData OldInfo;
            public EntryData NewInfo;
        }
    }

    public class EntryDataChanged
    {
        public List<EntryData> Added;
        public List<EntryData> Removed;
        public List<ModifiedEntry> Changed;

        public EntryDataChanged(List<EntryData> added, List<EntryData> removed, List<ModifiedEntry> changed)
        {
            Added = added;
            Removed = removed;
            Changed = changed;
        }

        public struct ModifiedEntry
        {
            public EntryData OldInfo;
            public EntryData NewInfo;
        }
    }

    public class GameObjectCreated
    {
        public Guid InstanceId;
        public GameObject GameObject;

        public GameObjectCreated(Guid instanceId, GameObject gameObject)
        {
            InstanceId = instanceId;
            GameObject = gameObject;
        }
    }

    public class AcquireEntryData
    {
        public Guid EntryId;

        public AcquireEntryData(Guid entryId)
        {
            EntryId = entryId;
        }
    }

    public class AcquireEntryDataFromModelData
    {
        public Guid ManifestId;
        public PersistentKey IdInSource;

        public AcquireEntryDataFromModelData(Guid manifestId, PersistentKey idInSource)
        {
            ManifestId = manifestId;
            IdInSource = idInSource;
        }
    }

    public class ReleaseEntryData
    {
        public Guid EntryId;

        public ReleaseEntryData(Guid entryId)
        {
            EntryId = entryId;
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

    public class MetadataCategoriesChanged
    {
        public string GroupKey;
        public List<string> FilterKeys;

        public MetadataCategoriesChanged(string groupKey, List<string> filterKeys)
        {
            GroupKey = groupKey;
            FilterKeys = filterKeys;
        }
    }

    public class MetadataGroupsChanged
    {
        public List<string> GroupKeys;

        public MetadataGroupsChanged(List<string> groupKeys)
        {
            GroupKeys = groupKeys;
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
}
