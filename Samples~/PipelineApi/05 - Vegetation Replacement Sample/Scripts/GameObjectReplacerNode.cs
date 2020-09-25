using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Reflect.Pipeline.Samples
{
    [Serializable]
    public class GameObjectReplacerNodeSettings
    {
        [Serializable]
        public class ReplacementEntry
        {
            public string category;
            public string family;
            public GameObject prefab;
        }

        public List<ReplacementEntry> entries;
    }


    public class GameObjectReplacerNode : ReflectNode<GameObjectReplacer>
    {
        public GameObjectInput input = new GameObjectInput();
        public GameObjectReplacerNodeSettings settings;

        protected override GameObjectReplacer Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var node = new GameObjectReplacer(settings);
            input.streamEvent = node.OnGameObjectEvent;
            return node;
        }
    }

    public class GameObjectReplacer : IReflectNodeProcessor
    {
        readonly GameObjectReplacerNodeSettings m_Settings;
        
        public GameObjectReplacer(GameObjectReplacerNodeSettings settings)
        {
            m_Settings = settings;
        }

        public void OnGameObjectEvent(SyncedData<GameObject> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                var gameObject = stream.data;
                if (!gameObject.TryGetComponent(out Metadata metadata))
                    return;

                if (!metadata.parameters.dictionary.TryGetValue("Category", out var category))
                    return;

                if (!metadata.parameters.dictionary.TryGetValue("Family", out var family))
                    return;

                foreach (var entry in m_Settings.entries)
                {
                    if (category.value.Contains(entry.category) && family.value.Contains(entry.family))
                    {
                        Object.Instantiate(entry.prefab, gameObject.transform);
                        return;
                    }
                }
            }
        }

        public void OnPipelineInitialized()
        {
            // not needed
        }

        public void OnPipelineShutdown()
        {
            // not needed
        }
    }
}
