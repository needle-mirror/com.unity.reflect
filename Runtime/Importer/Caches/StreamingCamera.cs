using System.Collections.Generic;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class StreamingCamera : MonoBehaviour
    {
        public int m_MaximumObjects = 0;
        
        SyncManager m_SyncManager;

        List<StreamingReference> m_References = new List<StreamingReference>();

        HashSet<SyncObjectBinding.Identifier> m_VisibilityFilter = new HashSet<SyncObjectBinding.Identifier>();

        Dictionary<SyncInstance, SyncPrefab> m_Instances = new Dictionary<SyncInstance, SyncPrefab>();

        Vector3 m_LastPosition;
        Quaternion m_LastRotation;

        float m_LastUpdate;
        const double k_UpdateElapse = 0.25;

        const int k_MemoryWarningOxygen = 10 * 1024 * 1024;
        static char[] s_MemoryWarningOxygen;
        static int s_MaximumObjects;

        static StreamingCamera()
        {
            //    keep oxygen for later
            s_MemoryWarningOxygen = new char[k_MemoryWarningOxygen];
            s_MaximumObjects = 0;
        }

        private void Awake()
        {
            m_SyncManager = FindObjectOfType<SyncManager>();
        }

        void Start()
        {
            m_LastPosition = Vector3.zero;
            m_LastRotation = Quaternion.identity;

            if (m_SyncManager != null)
            {
                m_SyncManager.onInstanceAdded += OnInstanceAdded;
                m_SyncManager.onSyncUpdateEnd += OnSyncUpdateEnd;
                m_SyncManager.onProjectOpened += OnProjectOpened;
                m_SyncManager.onProjectClosed += OnProjectClosed;
            }
             
            Application.lowMemory += OnLowMemory;
            if (s_MaximumObjects == 0)
            {
                s_MaximumObjects = m_MaximumObjects;
            }
        }

        void OnInstanceAdded(SyncInstance instance)
        {
            instance.onPrefabLoaded += OnPrefabLoaded;
            instance.SetVisibilityFilter(m_VisibilityFilter);
        }

        void UpdateCapacity()
        {
            var capacity = 0;
            foreach (var instance in m_Instances)
            {
                capacity += instance.Key.GetPrefab().Instances.Count;
            }

            if (m_References.Count < capacity)
                m_References.Capacity = capacity;
        }

        void OnPrefabLoaded(SyncInstance instance, SyncPrefab prefab)
        {
            for (var r = m_References.Count - 1; r >= 0; --r)
            {
                if (m_References[r].GetSyncInstance() == instance)
                {
                    m_References.RemoveAt(r);
                }
            }

            UpdateCapacity();

            foreach (var entity in prefab.Instances)
            {
                var pos = entity.Transform.Position;
                var reference = new StreamingReference(instance, new SyncObjectBinding.Identifier(entity), new Vector3(pos.X, pos.Y, pos.Z));
                m_References.Add(reference);
            }

            m_Instances[instance] = prefab;
        }

        void OnSyncUpdateEnd(bool hasChanged)
        {
            if (hasChanged)
            {
                UpdateScores();
            }
        }

        void OnLowMemory()
        {
            //    use oxygen reserve now
            s_MemoryWarningOxygen = null;
            System.GC.Collect();
            
            int count = 0;
            foreach (var instance in m_Instances)
            {
                count += instance.Key.GetInstanceCount();
            }

            s_MaximumObjects = (int)(count * 0.8f);
            Debug.Log("Setting maximum object count to " + s_MaximumObjects + " due to memory warning.");
            UpdateScores();
        }

        void OnProjectOpened()
        {
            UpdateScores();
        }

        void OnProjectClosed()
        {
            Clear();
        }

        void Update()
        {
            var t = transform;
            if (t.position != m_LastPosition || t.rotation != m_LastRotation)
            {
                var now = Time.time;
                if (now - m_LastUpdate > k_UpdateElapse)
                {
                    m_LastUpdate = now;

                    //  update the camera position and rotation
                    m_LastPosition = t.position;
                    m_LastRotation = t.rotation;

                    UpdateScores();
                }
            }
        }

        void UpdateScores()
        {
            if (s_MaximumObjects == 0)
            {
                Clear();
            }
            else
            {
                //  update score of all streaming references
                var root = m_SyncManager.syncRoot;
                foreach (var reference in m_References)
                {
                    reference.UpdateScore(transform, root);
                }

                m_References.Sort();

                m_VisibilityFilter.Clear();
                var start = m_References.Count > s_MaximumObjects ? m_References.Count - s_MaximumObjects : 0;
                for (var r = start; r < m_References.Count; ++r)
                {
                    m_VisibilityFilter.Add(m_References[r].GetIdentifier());
                }

                m_SyncManager.ApplyPrefabChanges();
            }
        }

        void LoadReferences()
        {
            m_Instances.Clear();
            foreach (var instance in m_SyncManager.syncInstances)
            {
                OnInstanceAdded(instance.Value);
                OnPrefabLoaded(instance.Value, instance.Value.GetPrefab());
            }
        }

        void Clear()
        {
            if (m_Instances.Count > 0)
            {
                foreach (var instance in m_Instances)
                {
                    instance.Key.RemoveVisibilityFilter(m_VisibilityFilter);
                }

                m_Instances.Clear();
                m_References.Clear();
            }
        }

        void OnEnable()
        {
            LoadReferences();
            UpdateScores();
        }

        void OnDisable()
        {
            Clear();
        }
    }
}
