using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Reflect
{

    [Serializable]
    class StringArray
    {
        public string key;
        public string[] value;
    }

    // Json serialize format mapping to Dictionary<string, string[]>
    [Serializable]
    class ArrayOfStringArray
    {
        public StringArray[] items;
    }

    // Unity Runtime Json Serializer Utility
    class JsonSerializer
    {
        public static void Save(string filePath, Dictionary<string, string[]> dictionary)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
            List<StringArray> dictionaryItemsList = new List<StringArray>();
            foreach (KeyValuePair<string, string[]> kvp in dictionary)
            {
                dictionaryItemsList.Add(new StringArray() { key = kvp.Key, value = kvp.Value });
            }
            ArrayOfStringArray dictionaryArray = new ArrayOfStringArray() { items = dictionaryItemsList.ToArray() };
            System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(dictionaryArray));
        }
        
        public static T Load<T>(string filePath) where T : class
        {
            var genericClass = Activator.CreateInstance<T>();
            if (System.IO.File.Exists(filePath))
            {   
                // TODO: If needed create other Type reader
                if (genericClass.GetType().IsInstanceOfType(new Dictionary<string, string[]>()))
                {
                    ArrayOfStringArray loadedData = JsonUtility.FromJson<ArrayOfStringArray>(System.IO.File.ReadAllText(filePath));
                    Dictionary<string, string[]> dictionary = genericClass as Dictionary<string, string[]>;
                    for (int i = 0; i < loadedData.items.Length; i++)
                    {
                        dictionary.Add(loadedData.items[i].key, loadedData.items[i].value);
                    }
                    return (T)Convert.ChangeType(dictionary, typeof(T)); ;
                }
            }
            // Return non-null instance
            return genericClass;
        }
    }
}
