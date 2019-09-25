using System.Collections.Generic;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class StreamingCamera : MonoBehaviour
    {
        public int m_MaximumObjects = 0;
        private const int k_AverageObjectSize = 512;
        public SyncManager m_SyncManager;

        List<StreamingReference> m_References = new List<StreamingReference>();

        HashSet<SyncObjectBinding.Identifier> m_VisibilityFilter = new HashSet<SyncObjectBinding.Identifier>();

        Dictionary<SyncInstance, SyncPrefab> m_Instances = new Dictionary<SyncInstance, SyncPrefab>();

        Vector3 m_LastPosition;
        Quaternion m_LastRotation;

        float m_LastUpdate;
        const double k_UpdateElapse = 0.25;

        void Start()
        {
            m_LastPosition = Vector3.zero;
            m_LastRotation = Quaternion.identity;

            if (m_SyncManager != null)
            {
                m_SyncManager.onInstanceAdded += OnInstanceAdded;
            }

            //    dynamically set the maximum size
            var memory = SystemInfo.systemMemorySize;
            Debug.Log(memory + " bytes of available memory.");
            if (m_MaximumObjects == 0)
            {
                m_MaximumObjects = (memory == 0) ? 10000 : memory / k_AverageObjectSize * 1024 * 1024;
                Debug.Log("Setting maximum to " + m_MaximumObjects + " objects");
            }
        }

        private void OnInstanceAdded(SyncInstance instance)
        {
            instance.onPrefabLoaded += OnPrefabLoaded;
            instance.SetVisibilityFilter(m_VisibilityFilter);
        }

        private void UpdateCapacity()
        {
            var capacity = 0;
            foreach (var instance in m_Instances)
            {
                capacity += instance.Key.GetPrefab().Instances.Count;
            }
            m_References.Capacity = capacity;
        }

        private void OnSyncRootChanged()
        {
            m_References.Clear();            
        }
        
        private void OnPrefabLoaded(SyncInstance instance, SyncPrefab prefab)
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
                var position = new Vector3(entity.Transform.Position.X, entity.Transform.Position.Y, entity.Transform.Position.Z);
                var reference = new StreamingReference(instance, new SyncObjectBinding.Identifier(entity), position);
                m_References.Add(reference);
            }

            m_Instances[instance] = prefab;
        }

        private void Update()
        {
            if ((transform.position != m_LastPosition) || (transform.rotation != m_LastRotation))
            {
                var now = Time.time;
                if (now - m_LastUpdate > k_UpdateElapse)
                {
                    m_LastUpdate = now;

                    //  update the camera position and rotation
                    m_LastPosition = transform.position;
                    m_LastRotation = transform.rotation;

                    UpdateScores();
                }
            }
        }

        private void UpdateScores()
        {
                    
            //  update score of all streaming references
            var root = m_SyncManager.syncRoot;
            foreach (var reference in m_References)
            {
                reference.UpdateScore(transform, root);
            }

            m_References.Sort();

            m_VisibilityFilter.Clear();
            var start = (m_References.Count > m_MaximumObjects) ? m_References.Count - m_MaximumObjects : 0;
            for (var r = start; r < m_References.Count; ++r)
            {
                m_VisibilityFilter.Add(m_References[r].GetIdentifier());
            }

            m_SyncManager.ApplyPrefabChanges();
        }

        private void LoadReferences()
        {
            m_Instances.Clear();
            foreach (var instance in m_SyncManager.syncInstances)
            {
                OnInstanceAdded(instance.Value);
                OnPrefabLoaded(instance.Value, instance.Value.GetPrefab());
            }
        }
        
        private void Clear()
        {
            foreach (var instance in m_Instances)
            {
                instance.Key.RemoveVisibilityFilter(m_VisibilityFilter);
            }
            m_SyncManager.ApplyPrefabChanges();
            m_Instances.Clear();
        }
        
        private void OnEnable()
        {
            LoadReferences();
            UpdateScores();
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
