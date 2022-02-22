using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    public class DynamicEntryHandler
    {
        HashRecords m_Records = new HashRecords();

        public void OnUpdateEntryDependencies(RpcContext<UpdateEntryDependencies> ctx, NetOutput<DynamicEntryChanged> dynamicEntryChangedOutput)
        {
            var data = ctx.Data;
            var manifestId = data.ManifestId;
            var entryId = data.Id;

            var hierarchy = m_Records.GetOrCreateHierarchy(manifestId);

            if (hierarchy.Exists(entryId))
            {
                ctx.SendSuccess(NullData.Null);
                return;
            }

            hierarchy.Add(entryId, data.Dependencies);

            var manifestRecord = m_Records.GetManifestRecord(manifestId);
            var latestManifestIdInChildren = manifestId;
            foreach (var childId in data.Dependencies)
            {
                var entryRecord = m_Records.GetEntryRecord(childId);
                var relations = entryRecord.DynamicRelations;

                var latestDepManifestId = relations[relations.Count - 1].ManifestId;
                if (latestDepManifestId != manifestId)
                    latestManifestIdInChildren =
                        manifestRecord.GetMostRecentManifestBetween(latestManifestIdInChildren, latestDepManifestId);
            }

            if (latestManifestIdInChildren != manifestId)
            {
                var entryRecord = m_Records.GetEntryRecord(entryId);

                if (entryRecord.DynamicRelations.Any(x => x.ManifestId == latestManifestIdInChildren))
                {
                    ctx.SendSuccess(null);
                    return;
                }

                var (prev, next) = m_Records.AddDynamicEntry(entryRecord, latestManifestIdInChildren);

                var delta = new Delta<DynamicEntry>();
                delta.Changed.Add((prev, next));
                var hashDelta = new HashDelta<DynamicGuid, DynamicEntry>
                {
                    Added = new Dictionary<DynamicGuid, DynamicEntry>(),
                    Removed = new Dictionary<DynamicGuid, DynamicEntry>(),
                    Changed = new Dictionary<DynamicGuid, (DynamicEntry Prev, DynamicEntry Next)>()
                };
                hashDelta.Changed.Add(prev.Id, (prev, next));

                m_Records.GenerateNewDynamicIdForAncestors(prev, latestManifestIdInChildren, delta, hashDelta);
                dynamicEntryChangedOutput.Send(new DynamicEntryChanged(delta));
            }

            ctx.SendSuccess(NullData.Null);
        }

        public void OnEntryDataChanged(NetContext<EntryDataChanged> ctx, NetOutput<DynamicEntryChanged> dynamicEntryChangedOutput)
        {
            OnEntryDataChanged(ctx.Data, dynamicEntryChangedOutput);
        }

        internal void OnEntryDataChanged(EntryDataChanged data, NetOutput<DynamicEntryChanged> dynamicEntryChangedOutput)
        {
            foreach (var changed in data.NewManifestIds)
            {
                m_Records.GetOrCreateHierarchy(changed.ManifestId);
                var manifestRecord = m_Records.GetOrCreateManifestRecord(changed.StableId);
                m_Records.AddManifestVersion(changed.ManifestId, manifestRecord);
            }

            var delta = data.Delta;
            var dynamicDelta = new Delta<DynamicEntry>();
            foreach (var added in delta.Added)
            {
                var entryRecord = new EntryRecord(added.StableId, new List<DynamicEntry>());
                var dynamicEntry = m_Records.AddEntry(entryRecord, added);
                dynamicDelta.Added.Add(dynamicEntry);
            }

            foreach (var removed in delta.Removed)
            {
                var dynamicEntry = m_Records.MarkAsRemoved(removed);
                dynamicDelta.Removed.Add(dynamicEntry);
            }

            foreach (var changed in delta.Changed)
            {
                var (prev, next) = m_Records.AddChangedEntry(changed.Prev, changed.Next);
                dynamicDelta.Changed.Add((prev, next));
            }

            var hashDelta = new HashDelta<DynamicGuid, DynamicEntry>
            {
                Added = dynamicDelta.Added.ToDictionary(x => x.Id, x => x),
                Removed = dynamicDelta.Removed.ToDictionary(x => x.Id, x => x),
                Changed = dynamicDelta.Changed.ToDictionary(x => x.Prev.Id, x => (x.Prev, x.Next))
            };

            var nbChanged = dynamicDelta.Changed.Count;
            for (var i = 0; i < nbChanged; ++i)
            {
                var (prev, next) = dynamicDelta.Changed[i];
                m_Records.GenerateNewDynamicIdForAncestors(prev, next.ManifestId, dynamicDelta, hashDelta);
            }

            dynamicEntryChangedOutput.Send(new DynamicEntryChanged(dynamicDelta));
        }

        public void OnAcquireDynamicEntry(RpcContext<AcquireDynamicEntry> ctx)
        {
            var dynamicEntry = m_Records.GetDynamicEntry(ctx.Data.DynamicId);
            ctx.SendSuccess(dynamicEntry);
        }

        public void OnGetDynamicIds(RpcContext<GetDynamicIds> ctx)
        {
            // Copy list to remove race condition
            var dynamicIds = m_Records.GetDynamicIds(ctx.Data.StableId).ToList();
            ctx.SendSuccess(dynamicIds);
        }

        public void OnClearBeforeSyncUpdate(PipeContext<ClearBeforeSyncUpdate> ctx)
        {
            m_Records = new HashRecords();
            ctx.Continue();
        }

        public void OnGetStableId(RpcContext<GetStableId<DynamicGuid>> ctx)
        {
            var entry = m_Records.GetDynamicEntry(ctx.Data.Key);
            ctx.SendSuccess(new Boxed<EntryStableGuid>(entry.Data.StableId));
        }

        class HashRecords
        {
            Dictionary<ManifestStableGuid, ManifestRecord> m_StableManifestRecords =
                new Dictionary<ManifestStableGuid, ManifestRecord>();

            Dictionary<ManifestGuid, ManifestRecord> m_ManifestRecords = new Dictionary<ManifestGuid, ManifestRecord>();

            Dictionary<EntryGuid, EntryRecord> m_EntryRecords = new Dictionary<EntryGuid, EntryRecord>();
            Dictionary<DynamicGuid, EntryRecord> m_DynamicRecords = new Dictionary<DynamicGuid, EntryRecord>();

            Dictionary<ManifestGuid, Hierarchy> m_ManifestHierarchies = new Dictionary<ManifestGuid, Hierarchy>();
            Dictionary<DynamicGuid, Hierarchy> m_DynamicHierarchies = new Dictionary<DynamicGuid, Hierarchy>();

            Dictionary<DynamicGuid, DynamicEntry> m_DynamicEntries = new Dictionary<DynamicGuid, DynamicEntry>();

            Dictionary<EntryStableGuid, List<DynamicGuid>> m_StableIdToDynamicIds =
                new Dictionary<EntryStableGuid, List<DynamicGuid>>();

            public Hierarchy GetOrCreateHierarchy(ManifestGuid manifestId)
            {
                if (!m_ManifestHierarchies.TryGetValue(manifestId, out var hierarchy))
                {
                    hierarchy = new Hierarchy();
                    m_ManifestHierarchies.Add(manifestId, hierarchy);
                }

                return hierarchy;
            }

            public ManifestRecord GetOrCreateManifestRecord(ManifestStableGuid stableId)
            {
                if (!m_StableManifestRecords.TryGetValue(stableId, out var manifestRecord))
                {
                    manifestRecord = new ManifestRecord(stableId);
                    m_StableManifestRecords.Add(stableId, manifestRecord);
                }

                return manifestRecord;
            }

            public void AddManifestVersion(ManifestGuid newVersion, ManifestRecord manifestRecord)
            {
                manifestRecord.Versions.Add(newVersion);
                m_ManifestRecords.Add(newVersion, manifestRecord);
            }

            public (DynamicEntry Prev, DynamicEntry Next) AddDynamicEntry(EntryRecord entryRecord,
                ManifestGuid newManifestId)
            {
                var dynamicPrev = entryRecord.DynamicRelations.Last();
                var dynamicNext = GenerateDynamicEntry(entryRecord, dynamicPrev.Data, newManifestId);

                return (dynamicPrev, dynamicNext);
            }

            public (DynamicEntry Prev, DynamicEntry Next) AddChangedEntry(EntryData prev, EntryData next)
            {
                var entryRecord = GetEntryRecord(prev.Id);

                var dynamicPrev = m_DynamicEntries[entryRecord.DynamicRelations.Last().Id];
                var dynamicNext = AddEntry(entryRecord, next);

                return (dynamicPrev, dynamicNext);
            }

            public DynamicEntry AddEntry(EntryRecord entryRecord, EntryData entry)
            {
                if (m_EntryRecords.TryGetValue(entry.Id, out var oldRecord))
                {
                    entryRecord = oldRecord;
                    entryRecord.IsRemoved = false;
                }
                else
                {
                    m_EntryRecords.Add(entry.Id, entryRecord);
                }

                // Bug: Cannot track added like this, need to request an acquisition from ManifestActor
                return GenerateDynamicEntry(entryRecord, entry, entry.ManifestId);
            }

            public DynamicEntry GetLatestDynamicEntry(EntryData entry)
            {
                var entryRecord = GetEntryRecord(entry.Id);
                return m_DynamicEntries[entryRecord.DynamicRelations.Last().Id];
            }

            public ManifestRecord GetManifestRecord(ManifestGuid manifestId) => m_ManifestRecords[manifestId];
            public EntryRecord GetEntryRecord(EntryGuid entryId) => m_EntryRecords[entryId];
            public DynamicEntry GetDynamicEntry(DynamicGuid dynamicId) => m_DynamicEntries[dynamicId];
            public List<DynamicGuid> GetDynamicIds(EntryStableGuid stableId) => m_StableIdToDynamicIds[stableId];

            public void GenerateNewDynamicIdForAncestors(DynamicEntry oldEntry, ManifestGuid newManifestId,
                Delta<DynamicEntry> entryDelta, HashDelta<DynamicGuid, DynamicEntry> hashDelta)
            {
                var hierarchy = m_ManifestHierarchies[oldEntry.ManifestId];
                if (!hierarchy.EntryToParents.TryGetValue(oldEntry.Data.Id, out var initialParents))
                    return;

                var parents = new Stack<EntryGuid>(initialParents);
                while (parents.Count > 0)
                {
                    var parentId = parents.Pop();

                    var entryRecord = GetEntryRecord(parentId);

                    // this parent is already part of the removed ones.
                    if (entryRecord.IsRemoved)
                        continue;

                    // Todo: Should also check if a newer version has been generated
                    var dynamicRelation =
                        entryRecord.DynamicRelations.FirstOrDefault(x => x.ManifestId == newManifestId);

                    // this parent is already regenerated for this manifest version.
                    if (dynamicRelation != null)
                        continue;

                    var prev = entryRecord.DynamicRelations.Last();
                    var next = GenerateDynamicEntry(entryRecord, prev.Data, newManifestId);

                    entryDelta.Changed.Add((prev, next));
                    hashDelta.Changed.Add(prev.Id, (prev, next));

                    if (hierarchy.EntryToParents.TryGetValue(parentId, out var nextParents))
                    {
                        foreach (var nextParent in nextParents)
                            parents.Push(nextParent);
                    }
                }
            }

            public DynamicEntry MarkAsRemoved(EntryData entryData)
            {
                var entryRecord = GetEntryRecord(entryData.Id);
                entryRecord.IsRemoved = true;
                return GetLatestDynamicEntryFromRecord(entryRecord);
            }

            DynamicEntry GetLatestDynamicEntryFromRecord(EntryRecord entryRecord) =>
                m_DynamicEntries[entryRecord.DynamicRelations.Last().Id];

            DynamicEntry GenerateDynamicEntry(EntryRecord entryRecord, EntryData entry, ManifestGuid newManifestId)
            {
                // Bug: Cannot track entry like this, need to request an acquisition from ManifestActor
                var dynamicEntry = new DynamicEntry(DynamicGuid.NewGuid(), newManifestId, entry);
                AddDynamicEntry(dynamicEntry, entryRecord);
                return dynamicEntry;
            }

            void AddDynamicEntry(DynamicEntry dynamicEntry, EntryRecord entryRecord)
            {
                m_DynamicRecords.Add(dynamicEntry.Id, entryRecord);
                m_DynamicEntries.Add(dynamicEntry.Id, dynamicEntry);
                if (!m_StableIdToDynamicIds.TryGetValue(entryRecord.StableId, out var mapping))
                {
                    mapping = new List<DynamicGuid>();
                    m_StableIdToDynamicIds.Add(entryRecord.StableId, mapping);
                }

                mapping.Add(dynamicEntry.Id);

                entryRecord.DynamicRelations.Add(dynamicEntry);
            }
        }

        class ManifestRecord
        {
            public ManifestStableGuid StableId;
            public List<ManifestGuid> Versions = new List<ManifestGuid>();

            public ManifestRecord(ManifestStableGuid stableId)
            {
                StableId = stableId;
            }

            public ManifestGuid GetMostRecentManifestBetween(ManifestGuid a, ManifestGuid b)
            {
                var aIndex = Versions.IndexOf(a);
                var bIndex = Versions.IndexOf(b);

                return aIndex > bIndex ? a : b;
            }
        }

        class EntryRecord
        {
            public EntryStableGuid StableId;
            public bool IsRemoved;
            public List<DynamicEntry> DynamicRelations;

            public EntryRecord(EntryStableGuid stableId, List<DynamicEntry> dynamicRelations)
            {
                StableId = stableId;
                DynamicRelations = dynamicRelations;
            }
        }

        class Hierarchy
        {
            public Dictionary<EntryGuid, List<EntryGuid>> EntryToParents = new Dictionary<EntryGuid, List<EntryGuid>>();

            public Dictionary<EntryGuid, List<EntryGuid>>
                EntryToChildren = new Dictionary<EntryGuid, List<EntryGuid>>();

            public bool Exists(EntryGuid entryId) => EntryToChildren.ContainsKey(entryId);

            public void Add(EntryGuid entryId, List<EntryGuid> children)
            {
                EntryToChildren.Add(entryId, children);

                foreach (var child in children)
                {
                    if (!EntryToParents.TryGetValue(child, out var parents))
                    {
                        parents = new List<EntryGuid>();
                        EntryToParents.Add(child, parents);
                    }

                    if (!parents.Contains(entryId))
                        parents.Add(entryId);
                }
            }
        }
    }
    
    public class DynamicEntry
    {
        public DynamicGuid Id;
        public ManifestGuid ManifestId;
        public EntryData Data;

        public DynamicEntry(DynamicGuid id, ManifestGuid manifestId, EntryData data)
        {
            Id = id;
            ManifestId = manifestId;
            Data = data;
        }
    }
}
