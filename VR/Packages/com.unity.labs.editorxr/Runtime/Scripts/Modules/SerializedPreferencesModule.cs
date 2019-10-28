using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Modules
{
    sealed class SerializedPreferencesModule : IDelayedInitializationModule, IInterfaceConnector
    {
        [Serializable]
        internal class SerializedPreferences : ISerializationCallbackReceiver
        {
            [SerializeField]
            SerializedPreferenceItem[] m_Items;

            readonly Dictionary<Type, SerializedPreferenceItem> m_ItemDictionary = new Dictionary<Type, SerializedPreferenceItem>();

            public Dictionary<Type, SerializedPreferenceItem> items { get { return m_ItemDictionary; } }

            public void OnBeforeSerialize()
            {
                m_Items = m_ItemDictionary.Values.ToArray();
            }

            public void OnAfterDeserialize()
            {
                foreach (var item in m_Items)
                {
                    var type = Type.GetType(item.name);
                    if (type != null)
                    {
                        if (m_ItemDictionary.ContainsKey(type))
                            Debug.LogWarning("Multiple payloads of the same type on deserialization");

                        m_ItemDictionary[type] = item;
                    }
                }
            }

            public void Remove(Type type)
            {
                m_ItemDictionary.Remove(type);
                m_Items = m_ItemDictionary.Values.ToArray();
            }

            internal static SerializedPreferences Deserialize(string json)
            {
                return JsonUtility.FromJson<SerializedPreferences>(json);
            }
        }

        [Serializable]
        internal class SerializedPreferenceItem
        {
            [SerializeField]
            string m_Name;
            [SerializeField]
            string m_PayloadType;
            [SerializeField]
            string m_Payload;

            public string name
            {
                get { return m_Name; }
                set { m_Name = value; }
            }

            public string payloadType
            {
                get { return m_PayloadType; }
                set { m_PayloadType = value; }
            }

            public string payload
            {
                get { return m_Payload; }
                set { m_Payload = value; }
            }
        }

        public const string SerializedPreferencesKey = "EditorVR.SerializedPreferences";

        readonly List<ISerializePreferences> m_Serializers = new List<ISerializePreferences>();
        bool m_HasDeserialized;

        internal SerializedPreferences Preferences { get; private set; }

        internal static string serializedPreferences
        {
            get { return EditorPrefs.GetString(SerializedPreferencesKey, string.Empty); }
            set { EditorPrefs.SetString(SerializedPreferencesKey, value); }
        }

        public int initializationOrder { get { return -1; } }
        public int shutdownOrder { get { return 1; } }
        public int connectInterfaceOrder { get { return 1; } }

        public void Initialize()
        {
            Preferences = SerializedPreferences.Deserialize(serializedPreferences);
        }

        public void Shutdown()
        {
            if (m_HasDeserialized)
                serializedPreferences = SerializePreferences();

            m_HasDeserialized = false;
        }

        internal void DeserializePreferences()
        {
            if (Preferences != null)
            {
                foreach (var serializer in m_Serializers)
                {
                    Deserialize(Preferences, serializer);
                }
            }

            m_HasDeserialized = true;
        }

        internal string SerializePreferences()
        {
            if (Preferences == null)
                Preferences = new SerializedPreferences();

            var serializerTypes = new HashSet<Type>();

            foreach (var serializer in m_Serializers)
            {
                var payload = serializer.OnSerializePreferences();

                if (payload == null)
                    continue;

                var type = serializer.GetType();

                if (!serializerTypes.Add(type))
                    Debug.LogWarning(string.Format("Multiple payloads of type {0} on serialization", type));

                Preferences.items[type] = new SerializedPreferenceItem
                {
                    name = type.FullName,
                    payloadType = payload.GetType().FullName,
                    payload = JsonUtility.ToJson(payload)
                };
            }

            return JsonUtility.ToJson(Preferences);
        }

        static void Deserialize(SerializedPreferences preferences, ISerializePreferences serializer)
        {
            SerializedPreferenceItem item;
            if (preferences.items.TryGetValue(serializer.GetType(), out item))
            {
                var type = Type.GetType(item.payloadType);
                if (type == null)
                    return;

                var payload = JsonUtility.FromJson(item.payload, type);
                serializer.OnDeserializePreferences(payload);
            }
        }

        public void LoadModule() { }

        public void UnloadModule() { }

        public void ConnectInterface(object target, object userData = null)
        {
            var serializePreferences = target as ISerializePreferences;
            if (serializePreferences != null)
            {
                m_Serializers.Add(serializePreferences);
                if (Preferences != null)
                    Deserialize(Preferences, serializePreferences);
            }
        }

        public void DisconnectInterface(object target, object userData = null)
        {
        }
    }
}
