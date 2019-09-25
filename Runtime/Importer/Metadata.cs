using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Reflect
{
    [DisallowMultipleComponent]
    public class Metadata : MonoBehaviour
    {
        [Serializable]
        public class Parameter
        {
            public string group;
            public string value;
            public bool visible;
        }
        
        [Serializable]
        public class Parameters : SerializedDictionary<string, Parameter>
        {
        }
        
        public Parameters parameters = new Parameters();
        
        public Tag modelTag = Tag.Default;

        public enum Tag
        {
            Default,
            Door
        }
        
        public Dictionary<string, Parameter> GetParameters()
        {
            return parameters.dictionary;
        }
        
        public string GetParameter(string key)
        {
            return parameters.dictionary.TryGetValue(key, out var parameter) ? parameter.value : string.Empty;
        }
        
        public Dictionary<string, Dictionary<string, Parameter>> SortedByGroup()
        {
            var parameterGroups = new Dictionary<string, Dictionary<string, Parameter>>();
            foreach (var parameter in parameters.dictionary)
            {
                var group = parameter.Value.group;
                if (!parameterGroups.ContainsKey(group))
                {
                    parameterGroups.Add(group, new Dictionary<string, Parameter>());
                }
                if (parameter.Value.visible)
                {
                    parameterGroups[group].Add(parameter.Key, parameter.Value);
                }
            }

            return parameterGroups;
        }
    }
}