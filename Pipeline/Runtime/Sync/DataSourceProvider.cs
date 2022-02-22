using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect;
using Unity.Reflect.Data;

namespace UnityEngine.Reflect.Pipeline
{
    
    public static class DataSourceProvider<T>
    {
        static readonly List<Action<Dictionary<StreamKey, IDataInstance>>> s_Listeners = new List<Action<Dictionary<StreamKey, IDataInstance>>>();
        static readonly Dictionary<StreamKey, IDataInstance> s_DataInstances = new Dictionary<StreamKey, IDataInstance>();


        public static void AddListener(Action<Dictionary<StreamKey, IDataInstance>> action)
        {
            if (!s_Listeners.Contains(action))
            {
                s_Listeners.Add(action);
            }
        }
        public static void RemoveListener(Action<Dictionary<StreamKey, IDataInstance>> action)
        {
            if (s_Listeners.Contains(action))
            {
                s_Listeners.Remove(action);
            }
        }

        static void NotifyListeners()
        {
            var failedActions = new List<Action<Dictionary<StreamKey, IDataInstance>>>();
            foreach (var action in s_Listeners)
            {
                try
                {
                    action.Invoke(s_DataInstances);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug.LogError($"Notify IDataInstance Exception: {ex}");
                    failedActions.Add(action);
                }
            }
            // Clear any faulty Action
            foreach (var failedAction in failedActions) 
            {
                if (s_Listeners.Contains(failedAction)) 
                {
                    s_Listeners.Remove(failedAction);
                }
            }
        }

        public static void Update(StreamKey streamKey, IDataInstance dataInstance)
        {
            if (!s_DataInstances.ContainsKey(streamKey))
            {
                s_DataInstances.Add(streamKey, dataInstance);
            }
            else 
            {
                s_DataInstances[streamKey] = dataInstance;
            }

            NotifyListeners();
        }

        public static void Remove(StreamKey streamKey)
        {
            if (s_DataInstances.ContainsKey(streamKey))
            {
                s_DataInstances.Remove(streamKey);
            }
            NotifyListeners();
        }

    }
}
