using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Actors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    [Actor("de19bd9f-0bf2-4ad5-8f84-89b84d741d5e", true)]
    public class DebugActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetComponent m_Net;
#pragma warning restore 649
        
        DebugActorComponent m_Component;
        readonly Dictionary<Type, Action> m_DrawGizmosCommands = new Dictionary<Type, Action>();
        readonly Dictionary<Type, Action> m_GuiCommands = new Dictionary<Type, Action>();
        readonly Dictionary<Type, bool> m_GuiToggles = new Dictionary<Type, bool>();

        Vector2 m_ScrollPosition;
        Rect m_ScreenRect;

        void Inject()
        {
            m_Component = new GameObject($"[{nameof(DebugActor)}]").AddComponent<DebugActorComponent>();
            m_Component.DrawGizmosCommand = OnDrawGizmos;
            m_Component.GuiCommand = OnGUI;
            
            m_ScreenRect = Rect.zero;
        }

        void Shutdown()
        {
            m_DrawGizmosCommands.Clear();
            m_GuiCommands.Clear();
            Object.Destroy(m_Component.gameObject);
        }

        public TickResult Tick(TimeSpan _)
        {
            return m_Net.Tick(TimeSpan.MaxValue);
        }

        [EventInput]
        void OnDebugDrawGizmos(EventContext<DebugDrawGizmos> ctx)
        {
            m_DrawGizmosCommands[ctx.Message.SourceId.Type] = ctx.Data.Command;
        }

        [EventInput]
        void OnDebugGui(EventContext<DebugGui> ctx)
        {
            var type = ctx.Message.SourceId.Type;
            m_GuiCommands[type] = ctx.Data.Command;
            if (!m_GuiToggles.ContainsKey(type))
                m_GuiToggles.Add(type, true);
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            if (fieldName != nameof(Settings.ShowGui)) 
                return;
                
            m_Settings.ShowGui = (bool)newValue;
        }

        void OnDrawGizmos()
        {
            if (!m_Settings.ShowGizmos)
                return;
            
            var originalColor = Gizmos.color;

            foreach (var command in m_DrawGizmosCommands.Values)
                command();

            Gizmos.color = originalColor;
        }

        void OnGUI()
        {
            if (!m_Settings.ShowGui)
                return;
            
            var originalColor = GUI.color;
            m_ScreenRect.Set(0, 0, Screen.width, Screen.height);
            GUILayout.BeginArea(m_ScreenRect);
            {
                m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.BeginVertical(GUI.skin.box);
                            {
                                foreach (var command in m_GuiCommands)
                                {
                                    if (!m_GuiToggles.TryGetValue(command.Key, out var enabled))
                                        continue;
                                    
                                    m_GuiToggles[command.Key] = GUILayout.Toggle(enabled, command.Key.Name);
                                    
                                    GUILayout.BeginHorizontal();
                                    {
                                        GUILayout.Space(20);
                                        GUILayout.BeginVertical();
                                        {
                                            if (enabled)
                                                command.Value();
                                        }
                                        GUILayout.EndVertical();
                                    }
                                    GUILayout.EndHorizontal();
                                }
                            }
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            GUI.color = originalColor;
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public bool ShowGizmos;
            public bool ShowGui;
            
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
