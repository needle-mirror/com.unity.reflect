using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class StreamingReference : IComparable<StreamingReference>
    {
        SyncInstance m_SyncInstance;
        SyncObjectBinding.Identifier m_Identifier;
        Vector3 m_Position;
        float m_Score;

        public StreamingReference(SyncInstance syncInstance, SyncObjectBinding.Identifier identifier, Vector3 position, float score = -1f)
        {
            m_SyncInstance = syncInstance;
            m_Identifier = identifier;
            m_Position = position;
            m_Score = score;
        }

        public SyncInstance GetSyncInstance()
        {
            return m_SyncInstance;
        }

        public void UpdateScore(Transform camera, Transform syncRoot)
        {
            if ((m_Position - Vector3.zero).sqrMagnitude < 0.00001)
            {
                //  prevent procedural objects from flickering
                //  to remove once we use bounding boxes
                m_Score = 1000f + Math.Abs(m_Identifier.GetHashCode());
            }
            else
            {
                Vector3 direction = syncRoot.TransformPoint(m_Position) - camera.position;
                m_Score = Vector3.Dot(camera.forward, direction) / direction.sqrMagnitude;
            }
        }

        public int CompareTo(StreamingReference other)
        {
            if ((other == null) || (m_Score > other.m_Score))
            {
                return 1;
            }
            else if (m_Score < other.m_Score)
            {
                return -1;
            }
            return 0;
        }

        public SyncObjectBinding.Identifier GetIdentifier()
        {
            return m_Identifier;
        }
    }
}
