using System;
using UnityEngine;
using UnityEngine.Reflect;
using Object = System.Object;

namespace UnityEditor.Reflect
{
    public class UnityReflectPostProcessor : AssetPostprocessor
    {
        void OnPostprocessGameObjectWithUserProperties(GameObject gameObject, string[] propNames, Object[] values)
        {
            if (propNames.Length > 0)
            {
                var model = gameObject.AddComponent<Metadata>();
                for (var i = 0; i < propNames.Length; i++)
                {
                    model.parameters.dictionary.Add(propNames[i], new Metadata.Parameter
                    {
                        group = "User Properties",
                        value = values[i].ToString(),
                        visible = true
                    });
                }
            }
        }
    }
}
