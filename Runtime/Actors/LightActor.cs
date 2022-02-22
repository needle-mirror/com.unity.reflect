using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("3a2c4a94-b134-4563-ae49-875a4bb0abb3", true)]
    public class LightActor
    {
#pragma warning disable 649
        Settings m_Settings;

        RpcOutput<RunFuncOnGameObject> m_RunFuncOnGameObjectOutput;
#pragma warning restore 649

        Dictionary<DynamicGuid, List<Light>> m_Lights = new Dictionary<DynamicGuid, List<Light>>();
        const float k_Tolerance = 0.0001f;

        public void Shutdown()
        {
            m_Lights.Clear();
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
                AddGameObjectLights(go.Id);
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
                m_Lights.Remove(go.Id);
                
            ctx.Continue();
        }

        [EventInput]
        void OnUpdateSettings(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;

            var isChanged = false;

            if (ctx.Data.FieldName == nameof(Settings.EnableLights) && m_Settings.EnableLights != (bool)ctx.Data.NewValue)
            {
                m_Settings.EnableLights = (bool)ctx.Data.NewValue;
                isChanged = true;
            }

            if (ctx.Data.FieldName == nameof(Settings.LightIntensity) && Math.Abs(m_Settings.LightIntensity - (float)ctx.Data.NewValue) > k_Tolerance)
            {
                m_Settings.LightIntensity = (float)ctx.Data.NewValue;
                isChanged = true;
            }
            
            if(isChanged)
                RefreshLights();
        }

        void AddGameObjectLights(DynamicGuid id)
        {
            var rpc = m_RunFuncOnGameObjectOutput.Call(this, (object)null,
                new Boxed<DynamicGuid>(id),
                new RunFuncOnGameObject(id, default, go => go.GetComponentsInChildren<Light>(true).ToList()));
            rpc.Success<List<Light>>((self, ctx, userCtx, lights) =>
            {
                if (lights.Count == 0)
                    return;
            
                self.m_Lights.Add(userCtx.Value, lights);

                foreach (var light in lights)
                {
                    light.enabled = m_Settings.EnableLights;
                    light.intensity = m_Settings.LightIntensity;
                }
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                if (!(ex is OperationCanceledException))
                    Debug.LogException(ex);
            });
        }

        void RefreshLights()
        {
            var removed = new List<DynamicGuid>();
            foreach (var kv in m_Lights)
            {
                if (kv.Value[0] == null)
                {
                    removed.Add(kv.Key);
                    continue;
                }

                foreach (var light in kv.Value)
                {
                    light.enabled = m_Settings.EnableLights;
                    light.intensity = m_Settings.LightIntensity;
                }
            }

            foreach (var removedId in removed)
                m_Lights.Remove(removedId);
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public bool EnableLights;
            public float LightIntensity;

            public Settings()
                : base(Guid.NewGuid().ToString())
            {
                LightIntensity = 1;
            }
        }
    }
}
