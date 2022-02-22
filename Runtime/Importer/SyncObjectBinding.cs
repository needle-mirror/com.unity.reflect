using System;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using Unity.Reflect.Actors;

namespace UnityEngine.Reflect
{
    [DisallowMultipleComponent]
    public class SyncObjectBinding : MonoBehaviour, ISerializationCallbackReceiver
    {
        public static Action<GameObject> OnCreated;
        public static Action<GameObject> OnDestroyed;

        public EntryStableGuid stableId { get; set; }

        public StreamKey streamKey
        {
            get => m_StreamKey;
            set
            {
                m_StreamKey = value;
                var key = m_StreamKey.key;

                if (!(key.IsKeyFor<SyncObjectInstance>() || key.IsKeyFor<SyncNode>()) || string.IsNullOrEmpty(key.Name))
                {
                    Debug.LogWarning($"Setting an invalid {nameof(StreamKey)} on this {nameof(SyncObjectBinding)}: {m_StreamKey}");
                }

                m_SourceId = m_StreamKey.source;
                m_Id = m_StreamKey.key.Name;
            }
        }

        StreamKey m_StreamKey;
        
        [SerializeField]
        string m_SourceId;
        
        [SerializeField]
        string m_Id;
        
#if UNITY_EDITOR
        [HideInInspector]
        public Bounds bounds;
#endif
        protected void Start()
        {
            OnCreated?.Invoke(gameObject);
        }

        protected void OnDestroy()
        {
            OnDestroyed?.Invoke(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = Color.blue; 
            UnityEditor.Handles.DrawWireCube(bounds.center, bounds.size);
        }
#endif
        
        public void OnBeforeSerialize()
        {
            m_SourceId = m_StreamKey.source;
            m_Id = m_StreamKey.key.Name;
        }

        public void OnAfterDeserialize()
        {
            m_StreamKey = new StreamKey(m_SourceId, PersistentKey.GetKey<SyncObjectInstance>(m_Id));
        }
    }
}


