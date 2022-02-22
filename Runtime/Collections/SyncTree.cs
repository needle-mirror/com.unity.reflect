using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;
using Unity.Reflect.Actors;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Collections
{
	public sealed class SyncTree
	{
		const string HLOD_SUFFIX = "_instance";
		
        readonly PriorityHeap<IObject> m_PriorityHeap;
        readonly HashSet<SyncId> m_ExploredNodes;
        readonly HashSet<Node> m_PotentialRootNodes;
		readonly Dictionary<SyncId, IObject> m_IdToObjects;
		readonly Dictionary<SyncId, SyncNode> m_IdToSyncNodes;
		readonly Dictionary<SyncId, Node> m_UnlinkedChildrenToNodes;
		readonly List<Object> m_HiddenObjects;

		Bounds m_Bounds = new Bounds();

        public bool IsDirty { get; set; }
		public int ObjectCount { get; private set; }
		public int SyncNodeCount { get; private set; }
		public int Depth { get; private set; }
        public Node RootNode { get; private set; }
		public bool HasRootSyncNode => RootNode != null;
		public HlodMode DelayMode { get; set; }
		public bool UseHlods { get; set; }
		public bool UsePreloading { get; set; }
		public bool IsPreloading { get; set; }

		public Bounds Bounds
		{
			get
			{
				m_Bounds.SetMinMax(RootNode.Bounds.Min.ToUnity(), RootNode.Bounds.Max.ToUnity());
				return m_Bounds;
			}
		}

		public SyncTree()
		{
			m_PriorityHeap = new PriorityHeap<IObject>(comparer: Comparer<IObject>.Create((a, b) => a.Priority.CompareTo(b.Priority)));
			m_ExploredNodes = new HashSet<SyncId>();
			m_PotentialRootNodes = new HashSet<Node>();
			m_IdToObjects = new Dictionary<SyncId, IObject>();
			m_IdToSyncNodes = new Dictionary<SyncId, SyncNode>();
			m_UnlinkedChildrenToNodes = new Dictionary<SyncId, Node>();
			m_HiddenObjects = new List<Object>();
			ObjectCount = SyncNodeCount = 0;
		}

		public void Search(Vector3 origin, 
			Func<Node, bool> predicate,
			Func<IObject, float> prioritizer,
			List<Object> objectOutput,
			List<Node> nodeOutput,
			List<(DynamicGuid, HlodState)> hlodStateOutput,
			int maxResultsCount = int.MaxValue)
		{
			objectOutput.Clear();
			nodeOutput.Clear();
			m_PriorityHeap.Clear();
			m_HiddenObjects.Clear();

			if (RootNode == null)
			{
				foreach (var objKvp in m_IdToObjects)
				{
					var obj = objKvp.Value;
					if (obj.IsHlodInstance)
						continue;
					
					obj.IsVisible = true;
					obj.Priority = prioritizer(obj);
					m_PriorityHeap.Push(obj);
				}

				while (objectOutput.Count < maxResultsCount && !m_PriorityHeap.isEmpty)
				{
					if (!m_PriorityHeap.TryPop(out var obj))
						break;
					
					objectOutput.Add((Object)obj);
				}
				
				return;
			}
			
			m_ExploredNodes.Clear();

			var startNode = FindLeaf(origin);
			startNode.IsVisible = true;
			m_PriorityHeap.Push(startNode);
			m_ExploredNodes.Add(startNode.SyncId);

			var objectCount = 0;
			var hiddenObjectCount = 0;

			var useHlodDelay = UseHlods && DelayMode != HlodMode.None;
			var waitForObjects = DelayMode == HlodMode.Nodes;
			var isPreloading = UsePreloading && IsPreloading;

			while (!m_PriorityHeap.isEmpty)
			{
				if (!m_PriorityHeap.TryPop(out var obj) || !(obj is Node node))
					break;
				
				nodeOutput.Add(node);

				node.ShouldExpand = objectCount + (isPreloading ? hiddenObjectCount : 0) < maxResultsCount && predicate(node);

				if (node.ShouldExpand)
				{
					objectCount += node.Objects.Count;
					
					if (UseHlods && isPreloading)
						hiddenObjectCount += node.Hlods.Count;
					
					if (useHlodDelay)
						node.SetVisibleRecursive(node.CanExpand(DelayMode));
					
					foreach (var childNode in node.ChildNodes)
					{
						if (m_ExploredNodes.Contains(childNode.SyncId))
							continue;
						
						childNode.Priority = prioritizer(childNode);
						m_PriorityHeap.Push(childNode);
						m_ExploredNodes.Add(childNode.SyncId);
					}
				}
				else
				{
					if (UseHlods)
						objectCount += node.Hlods.Count;
					
					if (isPreloading)
						hiddenObjectCount += node.Objects.Count;
				}

				var parent = node.Parent;
				if (parent == null || m_ExploredNodes.Contains(parent.SyncId)) 
					continue;

				parent.IsVisible = true;
				parent.Priority = prioritizer(node.Parent);
				m_PriorityHeap.Push(parent);
				m_ExploredNodes.Add(parent.SyncId);
			}

			foreach (var node in nodeOutput)
			{
				if (useHlodDelay && !node.IsVisible)
				{
					// even if not visible, hlods need to be in the main output here
					var isParentVisible = node.Parent == null || node.Parent.IsVisible;
					ProcessObjects(node.Hlods, isParentVisible, objectOutput, hlodStateOutput, HlodState.Loading);
					ProcessObjects(node.Objects, false, waitForObjects || isPreloading ? (waitForObjects ? objectOutput : m_HiddenObjects) : null);
				}
				else if (node.ShouldExpand)
				{
					ProcessObjects(node.Objects, true, objectOutput);
					ProcessObjects(node.Hlods, false, UseHlods && isPreloading ? m_HiddenObjects : null, hlodStateOutput, HlodState.Default);
				}
				else
				{
					ProcessObjects(node.Hlods, true, UseHlods ? objectOutput : null, hlodStateOutput, HlodState.Default);
					ProcessObjects(node.Objects, false, isPreloading ? m_HiddenObjects : null);
				}
			}

			if (isPreloading)
				objectOutput.AddRange(m_HiddenObjects);
		}

		static void ProcessObjects(IEnumerable<Object> objects, bool isVisible, ICollection<Object> objectOutput, 
			ICollection<(DynamicGuid, HlodState)> hlodStateOutput = null, HlodState? hlodState = null)
		{
			foreach (var obj in objects)
			{
				obj.IsVisible = isVisible;
				objectOutput?.Add(obj);
				if (objectOutput != null && hlodStateOutput != null && hlodState.HasValue)
					hlodStateOutput.Add((obj.Entry.Id, hlodState.Value));
			}
		}

		Node FindLeaf(Vector3 origin)
		{
			return FindLeaf(RootNode, origin.ToNumerics());
		}

		static Node FindLeaf(Node node, System.Numerics.Vector3 origin)
		{
			foreach (var child in node.ChildNodes)
			{
				if (child.TightBounds.Contains(origin))
					return FindLeaf(child, origin);
			}

			return node;
		}

		public void Add(SyncNode syncNode)
		{
            IsDirty = true;
			m_IdToSyncNodes.Add(syncNode.Id, syncNode);
			++SyncNodeCount;
			TryLinkNode(syncNode.Id);
		}

		public void Add(IObject obj)
		{
            IsDirty = true;
			Insert(obj);
			// hlods not included in object count
			if (!obj.IsHlodInstance)
				++ObjectCount;
		}

		void Insert(IObject obj)
		{
			m_IdToObjects.Add(obj.SyncId, obj);
			
			// try linking object to its parent regardless of type
			TryLinkObject(obj.SyncId);
			
			// nodes must also try linking to their children
			if (!(obj is Node node))
				return;

			m_PotentialRootNodes.Add(node);
			TryLinkNode(node.SyncId);
		}

		bool TryLinkNode(SyncId syncId)
		{
			if (m_IdToSyncNodes.Count == 0)
				return false;
			
			if (!m_IdToObjects.TryGetValue(syncId, out var obj))
				return false;

			if (!(obj is Node node))
				return false;
			
			if (!m_IdToSyncNodes.TryGetValue(syncId, out var syncNode))
				return false;

			node.SyncNode = syncNode;
			
			ProcessChildren(node, syncNode.ChildNodeIds, node.ChildNodes);
			ProcessChildren(node, syncNode.HlodInstanceIds, node.Hlods);
			ProcessChildren(node, syncNode.NodeInstanceIds, node.Objects);

			Depth = Mathf.Max(Depth, node.Depth);

			if (m_PotentialRootNodes.Count == 1)
				RootNode = m_PotentialRootNodes.First();
			
			return true;
		}

		void ProcessChildren<T>(Node node, IEnumerable<SyncId> children, ICollection<T> target) where T : IObject
		{
			foreach (var childId in children)
			{
				if (!m_IdToObjects.TryGetValue(childId, out var childObj))
				{
					m_UnlinkedChildrenToNodes.Add(childId, node);
					continue;
				}
				
				target.Add((T)childObj);
				childObj.Parent = node;
				if (childObj is Node n)
					m_PotentialRootNodes.Remove(n);
			}
		}

		bool TryLinkObject(SyncId syncId)
		{
			if (m_IdToSyncNodes.Count == 0)
				return false;
			
			if (!m_IdToObjects.TryGetValue(syncId, out var o))
				return false;

			if (!(o is Object obj))
				return false;

			if (!m_UnlinkedChildrenToNodes.TryGetValue(syncId, out var node))
				return false;
			
			if (node.SyncNode.ChildNodeIds.Contains(syncId))
				node.ChildNodes.Add(obj as Node);
			else if (node.SyncNode.HlodInstanceIds.Contains(syncId))
				node.Hlods.Add(obj);
			else // if (node.SyncNode.NodeInstanceIds.Contains(syncId))
				node.Objects.Add(obj);
			
			obj.Parent = node;

			m_UnlinkedChildrenToNodes.Remove(syncId);
			if (obj is Node n)
				m_PotentialRootNodes.Remove(n);

			Depth = Mathf.Max(Depth, obj.Depth);

			return true;
		}

		public void Remove(SyncNode syncNode)
		{
            IsDirty = true;
			m_IdToSyncNodes.Remove(syncNode.Id);
			
			if (!m_IdToObjects.TryGetValue(syncNode.Id, out var obj)) 
				return;

			if (!(obj is Node node)) 
				return;
			
			node.SyncNode = null;
			node.ChildNodes.Clear();
			node.Hlods.Clear();
			node.Objects.Clear();
		}

		public void Remove(IObject obj)
		{
            IsDirty = true;
			if (!Delete(obj))
				return;

			--ObjectCount;
		}

		bool Delete(IObject obj)
		{
			return m_IdToObjects.Remove(obj.SyncId);
		}

		public void Clear()
		{
			m_PriorityHeap.Clear();
			m_ExploredNodes.Clear();
			m_PotentialRootNodes.Clear();
			m_IdToObjects.Clear();
			m_IdToSyncNodes.Clear();
			m_UnlinkedChildrenToNodes.Clear();
			m_HiddenObjects.Clear();
			RootNode = null;
			ObjectCount = SyncNodeCount = 0;
		}

		public static bool TryCreateObject(DynamicEntry dynamicEntry, out IObject obj)
		{
			obj = null;

			if (dynamicEntry.Data.EntryType == typeof(SyncNode))
			{
				obj = new Node(dynamicEntry);
				return true;
			}
			
			if (dynamicEntry.Data.EntryType == typeof(SyncObjectInstance))
			{
				obj = new Object(dynamicEntry);
				return true;
			}

			return false;
		}

		public interface IObject
		{
			float Priority { get; set; }
			DynamicEntry Entry { get; }
			Aabb Bounds { get; }
            SyncId SyncId { get; }
			Node Parent { get; set; }
			bool IsLoaded { get; set; }
			bool IsVisible { get; set; }
			bool IsHlodInstance { get; }
			int Depth { get; }
		}

		public class Object : IObject
		{
			public float Priority { get; set; }
			public DynamicEntry Entry { get; }
			public Aabb Bounds => Entry.Data.Spatial.Box;
            public SyncId SyncId { get; }
            
			public Node Parent { get; set; }
			public virtual bool IsLoaded { get; set; }
			public bool IsVisible { get; set; }
			public bool IsHlodInstance => SyncId.Value.EndsWith(HLOD_SUFFIX);
			public int Depth => Parent?.Depth + 1 ?? 0;

			public Object(DynamicEntry entry)
			{
				Entry = entry;
                SyncId = new SyncId(Entry.Data.IdInSource.Name, Entry.Data.SourceId);
			}
		}

		public class Node : Object
		{
			public SyncNode SyncNode;
			public readonly List<Node> ChildNodes = new List<Node>();
			public readonly List<Object> Hlods = new List<Object>();
			public readonly List<Object> Objects = new List<Object>();
			public bool ShouldExpand;
			
			public Aabb TightBounds { get; }

			// should never be zero, if so then geometric error wasn't exported and must be calculated
			// assuming a FeatureSizeFactor of 0.01, x0.5 since we want the real node bounds (not the loose bounds)
			// if there are no hlods the error should be max value to ensure we always expand
			public float GeometricError => SyncNode.GeometricError > 0f ? SyncNode.GeometricError : 
				HasHlods ? Bounds.Size.X * 0.005f : 
				float.MaxValue;

			public override bool IsLoaded => AreHlodsLoaded && AreObjectsLoaded;

			public bool HasChildNodes => SyncNode.ChildNodeIds.Count > 0;
			public bool AreChildNodeEntriesCreated => ChildNodes.Count == SyncNode.ChildNodeIds.Count;
			public bool AreChildNodesLoaded => AreChildNodeEntriesCreated && ChildNodes.TrueForAll(x => x.IsLoaded);

			public bool HasHlods => SyncNode.HlodInstanceIds.Count > 0;
			public bool AreHlodEntriesCreated => Hlods.Count == SyncNode.HlodInstanceIds.Count;
			public bool AreHlodsLoaded => AreHlodEntriesCreated && Hlods.TrueForAll(x => x.IsLoaded);

			public bool HasObjects => SyncNode.NodeInstanceIds.Count > 0;
			public bool AreObjectEntriesCreated => Objects.Count == SyncNode.NodeInstanceIds.Count;
			public bool AreObjectsLoaded => AreObjectEntriesCreated && Objects.TrueForAll(x => x.IsLoaded);
			

			public Node(DynamicEntry entry) : base(entry)
			{
				TightBounds = new Aabb(entry.Data.Spatial.Box.Center, entry.Data.Spatial.Box.Extents / 2f, Aabb.FromCenterTag);
			}

			public bool CanExpand(HlodMode delayMode)
			{
				return (delayMode != HlodMode.Nodes || AreObjectsLoaded) && 
				       AreChildNodeEntriesCreated && 
				       ChildNodes.TrueForAll(x => x.AreHlodsLoaded || x.CanExpand(delayMode));
			}

			public void SetVisibleRecursive(bool isVisible)
			{
				IsVisible = isVisible;
				foreach (var node in ChildNodes)
					node.SetVisibleRecursive(isVisible);
			}
		}
	}

	public enum HlodMode
	{
		None,
		Nodes, 
		Hlods
	}

	public enum HlodState
	{
		Default, 
		Loading
	}
}
