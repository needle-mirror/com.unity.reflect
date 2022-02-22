using System;
using Unity.Reflect.ActorFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Reflect.Actors
{
    [Actor("db9f9157-acf6-4c89-aa42-2ce1d09a2928", true)]
    public class FpsActor
    {
#pragma warning disable 649
        Settings m_Settings;
        
        NetComponent m_Net;
        NetOutput<FpsDataChanged> m_FpsDataChangedOutput;
        EventOutput<DebugGui> m_DebugGuiOutput;
#pragma warning restore 649

        public void Initialize()
        {
            SendFpsData();
        }

        public TickResult Tick(TimeSpan endTime)
        {
            m_Net.Tick(TimeSpan.MaxValue);
            SendFpsData();
            return TickResult.Yield;
        }

        void SendFpsData()
        {
            var fps = (int) (1f / (m_Settings.UseSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime));
            m_FpsDataChangedOutput.Send(new FpsDataChanged(fps));
            
            const string fpsText = "FPS: ";
            m_DebugGuiOutput.Broadcast(new DebugGui(() => GUILayout.Label(fpsText + fps)));
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public bool UseSmoothDeltaTime;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
