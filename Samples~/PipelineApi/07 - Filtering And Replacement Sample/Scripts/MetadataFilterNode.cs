using System;
using System.Collections.Generic;

namespace UnityEngine.Reflect.Pipeline.Samples
{
    [Serializable]
    public class MetadataFilterSettings
    {
        [Serializable]
        public class ParameterEntry
        {
            public string key;
            public string value;
        }

        public List<ParameterEntry> entries;
    }

    public class MetadataFilterNode : ReflectNode<MetadataFilter>
    {
        public StreamInstanceInput input = new StreamInstanceInput();
        
        public StreamInstanceOutput outputTrue = new StreamInstanceOutput();
        public StreamInstanceOutput outputFalse = new StreamInstanceOutput();
        
        public MetadataFilterSettings settings;
        
        protected override MetadataFilter Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var node = new MetadataFilter(settings, outputTrue, outputFalse);
            input.streamEvent = node.OnStreamInstanceEvent;

            return node;
        }
    }

    public class MetadataFilter : IReflectNodeProcessor
    {
        readonly DataOutput<StreamInstance> m_OutputTrue;
        readonly DataOutput<StreamInstance> m_OutputFalse;

        readonly MetadataFilterSettings m_Settings;

        public MetadataFilter(MetadataFilterSettings settings,
            DataOutput<StreamInstance> outputTrue, DataOutput<StreamInstance> outputFalse)
        {
            m_Settings = settings;
            m_OutputTrue = outputTrue;
            m_OutputFalse = outputFalse;
        }

        public void OnStreamInstanceEvent(SyncedData<StreamInstance> stream, StreamEvent streamEvent)
        {
            GetStreamOutput(stream).SendStreamEvent(stream, streamEvent);
        }

        DataOutput<StreamInstance> GetStreamOutput(SyncedData<StreamInstance> stream)
        {
            return CheckMetadata(stream.data) ? m_OutputTrue : m_OutputFalse;
        }

        bool CheckMetadata(StreamInstance stream)
        {
            var parameters = stream.instance.Metadata?.Parameters;

            if (parameters == null)
                return false;

            foreach (var entry in m_Settings.entries)
            {
                if (!parameters.TryGetValue(entry.key, out var parameter) || !parameter.Value.Contains(entry.value))
                    return false;
            }

            return true;
        }

        public void OnPipelineInitialized()
        {
            // Not needed
        }

        public void OnPipelineShutdown()
        {
            // Not needed
        }
    }
}

