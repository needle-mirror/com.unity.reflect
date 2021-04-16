using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using UnityEngine;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class LightActor
    {
#pragma warning disable 649
        Settings m_Settings;
#pragma warning restore 649

        List<Light> m_Lights = new List<Light>();

        public void Inject()
        {
            // nothing to do here
        }

        public void Shutdown()
        {
            m_Lights.Clear();
        }

        [NetInput]
        void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            var gameObject = ctx.Data.GameObject;

            var lights = gameObject.GetComponentsInChildren<Light>(true);
            if (lights == null || lights.Length == 0)
                return;
            
            foreach (var light in lights)
            {
                light.enabled = m_Settings.EnableLights;
                m_Lights.Add(light);
            }
        }

        [EventInput]
        void OnUpdateSettings(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;

            if (ctx.Data.FieldName == nameof(Settings.EnableLights) && m_Settings.EnableLights != (bool)ctx.Data.NewValue)
            {
                m_Settings.EnableLights = (bool)ctx.Data.NewValue;
                RefreshLights();
            }
        }
        
        // TODO: OnGameObjectRemoved?

        public void RefreshLights()
        {
            for (var i = m_Lights.Count - 1; i >= 0; --i)
            {
                if (m_Lights[i] == null)
                {
                    m_Lights.RemoveAt(i);
                    continue;
                }

                m_Lights[i].enabled = m_Settings.EnableLights;
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public bool EnableLights;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
