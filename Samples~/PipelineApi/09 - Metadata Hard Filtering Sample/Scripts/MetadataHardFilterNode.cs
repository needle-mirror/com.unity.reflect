using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    [Serializable]
    class MetadataHardFilterNode : ReflectNode<MetadataHardFilter>
    {
        public StreamInstanceInput input = new StreamInstanceInput();
        public StreamInstanceOutput output = new StreamInstanceOutput();

        protected override MetadataHardFilter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var filter = new MetadataHardFilter(output);

            input.streamBegin = filter.OnBegin;
            input.streamEvent = filter.OnStreamEvent;
            input.streamEnd = filter.OnEnd;

            return filter;
        }
    }

    class MetadataHardFilter : IReflectNodeProcessor
    {
        readonly DataOutput<StreamInstance> m_StreamOutput;

        class FilterData
        {
            public bool visible = true;
            public HashSet<SyncedData<StreamInstance>> streams = new HashSet<SyncedData<StreamInstance>>();
        }
        
        Dictionary<string, FilterData> m_Instances = new Dictionary<string, FilterData>();
        
        public IEnumerable<string> categories
        {
            get { return m_Instances.Keys; }
        }

        public MetadataHardFilter(DataOutput<StreamInstance> output)
        {
            m_StreamOutput = output;
        }
        
        public void OnBegin()
        {
            m_StreamOutput.SendBegin();
        }

        public void OnStreamEvent(SyncedData<StreamInstance> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                OnStreamAdded(stream);
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                OnStreamChanged(stream);
            }
            else if (streamEvent == StreamEvent.Removed)
            {
                OnStreamRemoved(stream);
            }
        }

        void OnStreamAdded(SyncedData<StreamInstance> stream)
        {
            
            var metadata = stream.data.instance.Metadata;

            if (metadata != null && metadata.Parameters.TryGetValue("Category", out var category))
            {
                if (!m_Instances.TryGetValue(category.Value, out var filter))
                {
                    m_Instances[category.Value] = filter = new FilterData();
                }
                
                filter.streams.Add(stream);

                if (filter.visible)
                {
                    m_StreamOutput.SendStreamAdded(stream);
                }
            }
            else
            {
                m_StreamOutput.SendStreamAdded(stream);
            }
        }

        public void OnStreamChanged(SyncedData<StreamInstance> stream)
        {
            m_StreamOutput.SendStreamChanged(stream);
        }

        public void OnStreamRemoved(SyncedData<StreamInstance> stream)
        {
            var metadata = stream.data.instance.Metadata;

            if (metadata != null && metadata.Parameters.TryGetValue("Category", out var category))
            {
                if (m_Instances.TryGetValue(category.Value, out var filter))
                {
                    filter.streams.Remove(stream);
                }
            }
            
            m_StreamOutput.SendStreamRemoved(stream);
        }

        public void OnEnd()
        {
            m_StreamOutput.SendEnd();
        }

        public bool IsVisible(string category)
        {
            if (!m_Instances.TryGetValue(category, out var filter))
                return true;

            return filter.visible;
        }

        public void SetVisibility(string category, bool visible)
        {
            if (!m_Instances.TryGetValue(category, out var filter))
                return;

            if (filter.visible == visible)
                return;

            filter.visible = visible;

            foreach (var instance in filter.streams)
            {
                if (visible)
                {
                    m_StreamOutput.SendStreamAdded(instance);
                }
                else
                {
                    m_StreamOutput.SendStreamRemoved(instance);
                }
            }
        }

        public void OnPipelineInitialized()
        {
            // Nothing
        }

        public void OnPipelineShutdown()
        {
            m_Instances.Clear();
        }
    }
}
