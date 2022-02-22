using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.ActorFramework
{
    public static class ActorExtensions
    {
        public static ActorConfig GetActorConfig<T>(this ActorSystemSetup actorSystemSetup) => 
            actorSystemSetup.ActorConfigs.First(x => x.TypeNormalizedFullName == typeof(T).ToString());
        
        public static ActorConfig GetActorConfig(this ActorSystemSetup actorSystemSetup, ActorSetup actorSetup) => 
            actorSystemSetup.ActorConfigs.First(x => x.Id == actorSetup.ConfigId);

        public static ActorSetup CreateActorSetup(this ActorConfig actorConfig) => 
            ActorSystemSetupAnalyzer.CreateActorSetup(actorConfig);
        
        public static ActorSetup GetActorSetup<T>(this ActorSystemSetup actorSystemSetup) 
        {
            var actorConfig = actorSystemSetup.GetActorConfig<T>();
            return actorSystemSetup.ActorSetups.First(x => x.ConfigId == actorConfig.Id);
        }
        
        public static bool TryGetActorSetup<T>(this ActorSystemSetup actorSystemSetup, out ActorSetup setup) 
        {
            var actorConfig = actorSystemSetup.GetActorConfig<T>();
            setup = actorSystemSetup.ActorSetups.FirstOrDefault(x => x.ConfigId == actorConfig.Id);
            return setup != null;
        }
        
        public static T GetActorSettings<T>(this ActorSetup actorSetup) where T : ActorSettings => (T)actorSetup.Settings;

        public static ActorSetup CreateActorSetup<T>(this ActorSystemSetup actorSystemSetup)
        {
            var actorConfig = actorSystemSetup.GetActorConfig<T>();
            var actorSetup = actorConfig.CreateActorSetup();
            actorSystemSetup.ActorSetups.Add(actorSetup);
            foreach (var inputConfig in actorConfig.InputConfigs)
            {
                var isValid = MultiplicityValidator.IsValid(actorSystemSetup.ComponentConfigs.First(x => x.Id == inputConfig.ComponentConfigId).InputMultiplicity, 0);
                actorSetup.Inputs.Add(new ActorPort(Guid.NewGuid().ToString(), inputConfig.Id, new List<ActorLink>(), isValid, false));
            }
            foreach (var outputConfig in actorConfig.OutputConfigs)
            {
                var isValid = MultiplicityValidator.IsValid(actorSystemSetup.ComponentConfigs.First(x => x.Id == outputConfig.ComponentConfigId).OutputMultiplicity, 0);
                actorSetup.Outputs.Add(new ActorPort(Guid.NewGuid().ToString(), outputConfig.Id, new List<ActorLink>(), isValid, false));
            }
            return actorSetup;
        }
        
        public static ComponentConfig GetComponentConfig<T>(this ActorSystemSetup actorSystemSetup) => 
            actorSystemSetup.ComponentConfigs.First(x => x.TypeNormalizedFullName == typeof(T).ToString());

        public static void Connect<TComponent, TMessage>(this ActorSystemSetup actorSystemSetup, 
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            var connectionInfo = GetConnectionInfo<TComponent, TMessage>(actorSystemSetup, outputActorSetup, inputActorSetup);
            var (outputPort, inputPort) = (connectionInfo.OutputPort, connectionInfo.InputPort);

            if (outputPort.Links.Any(x => x.OutputId == outputPort.Id && x.InputId == inputPort.Id))
                return;

            var link = new ActorLink(outputPort.Id, inputPort.Id, false);
            outputPort.Links.Add(link);
            inputPort.Links.Add(link);
        }

        public static void ConnectNet<T>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Connect<NetComponent, T>(outputActorSetup, inputActorSetup);
        }

        public static void ConnectRpc<T>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Connect<RpcComponent, T>(outputActorSetup, inputActorSetup);
        }

        public static void ConnectPipe<T>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Connect<PipeComponent, T>(outputActorSetup, inputActorSetup);
        }

        public static void Disconnect<TComponent, TMessage>(this ActorSystemSetup actorSystemSetup, 
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            var connectionInfo = GetConnectionInfo<TComponent, TMessage>(actorSystemSetup, outputActorSetup, inputActorSetup);
            var (outputPort, inputPort) = (connectionInfo.OutputPort, connectionInfo.InputPort);
            outputPort.Links.RemoveAll(x => x.OutputId == outputPort.Id && x.InputId == inputPort.Id);
        }

        public static void DisconnectNet<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Disconnect<NetComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        public static void DisconnectRpc<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Disconnect<RpcComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        public static void DisconnectPipe<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Disconnect<PipeComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        public static void Intercept<TComponent, TMessage>(this ActorSystemSetup actorSystemSetup, ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            var curConnectionInfos = GetConnectionInfos<TComponent, TMessage>(actorSystemSetup, outputActorSetup);

            if (curConnectionInfos.Count > 0)
            {
                var newRightConnectionInfo = GetConnectionInfo<TComponent, TMessage>(actorSystemSetup, inputActorSetup, curConnectionInfos[0].InputSetup);
                var newId = newRightConnectionInfo.OutputPort.Id;

                foreach (var curConnection in curConnectionInfos)
                {
                    curConnection.OutputPort.Links.Remove(curConnection.Link);
                    curConnection.Link.OutputId = newId;
                    newRightConnectionInfo.OutputPort.Links.Add(curConnection.Link);
                }
            }

            Connect<TComponent, TMessage>(actorSystemSetup, outputActorSetup, inputActorSetup);
        }

        public static void InterceptNet<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Intercept<NetComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        public static void InterceptRpc<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Intercept<RpcComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        public static void InterceptPipe<TMessage>(this ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            actorSystemSetup.Intercept<PipeComponent, TMessage>(outputActorSetup, inputActorSetup);
        }

        /// <summary>
        ///     Replaces an existing setup in the asset by another one. Rewires existing connections to the
        ///     new setup, removes dangling connections and removes <see cref="oldSetup"/>. Dangling connections
        ///     are sent back to the caller.
        /// </summary>
        /// <param name="actorSystemSetup"></param>
        /// <param name="oldSetup"></param>
        /// <param name="newSetup"></param>
        /// <returns>The list of missing connections after this method is executed.</returns>
        public static List<MissingConnection> ReplaceActor(this ActorSystemSetup actorSystemSetup, ActorSetup oldSetup, ActorSetup newSetup)
        {
            var missing = new List<MissingConnection>();

            var oldInputPorts = oldSetup.Inputs;
            var oldInputConfigs = actorSystemSetup.ActorConfigs.First(x => x.Id == oldSetup.ConfigId).InputConfigs;

            foreach (var oldInputPort in oldInputPorts)
            {
                foreach (var link in oldInputPort.Links)
                {
                    var oldPortConfig = oldInputConfigs.First(x => x.Id == oldInputPort.ConfigId);

                    var otherSetup = actorSystemSetup.ActorSetups.First(x => x.Outputs.Any(x => x.Id == link.OutputId));
                    var otherPort = otherSetup.Outputs.First(x => x.Id == link.OutputId);
                    var otherPortConfig = actorSystemSetup.ActorConfigs.First(x => x.Id == otherSetup.ConfigId).OutputConfigs.First(x => x.Id == otherPort.ConfigId);

                    missing.Add(new MissingConnection
                    {
                        PortType = PortType.Input,
                        Link = link,
                        OldSetup = oldSetup,
                        OldPort = oldInputPort,
                        OldPortConfig = oldPortConfig,
                        OtherSetup = otherSetup,
                        OtherPort = otherPort,
                        OtherPortConfig = otherPortConfig
                    });
                }
            }

            var oldOutputPorts = oldSetup.Outputs;
            var oldOutputConfigs = actorSystemSetup.ActorConfigs.First(x => x.Id == oldSetup.ConfigId).OutputConfigs;

            foreach (var oldOutputPort in oldOutputPorts)
            {
                foreach (var link in oldOutputPort.Links)
                {
                    var oldPortConfig = oldOutputConfigs.First(x => x.Id == oldOutputPort.ConfigId);

                    var otherSetup = actorSystemSetup.ActorSetups.First(x => x.Inputs.Any(x => x.Id == link.InputId));
                    var otherPort = otherSetup.Inputs.First(x => x.Id == link.InputId);
                    var otherPortConfig = actorSystemSetup.ActorConfigs.First(x => x.Id == otherSetup.ConfigId).InputConfigs.First(x => x.Id == otherPort.ConfigId);

                    missing.Add(new MissingConnection
                    {
                        PortType = PortType.Output,
                        Link = link,
                        OldSetup = oldSetup,
                        OldPort = oldOutputPort,
                        OldPortConfig = oldPortConfig,
                        OtherSetup = otherSetup,
                        OtherPort = otherPort,
                        OtherPortConfig = otherPortConfig
                    });
                }
            }

            var newConfig = actorSystemSetup.ActorConfigs.First(x => x.Id == newSetup.ConfigId);
            newSetup.Position = oldSetup.Position;

            for (var i = missing.Count - 1; i >= 0; --i)
            {
                var m = missing[i];

                if (m.PortType == PortType.Input)
                {
                    var newPortConfig = newConfig.InputConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == m.OldPortConfig.TypeNormalizedFullName);
                    if (newPortConfig == null)
                        continue;

                    var newPort = newSetup.Inputs.First(x => x.ConfigId == newPortConfig.Id);
                    newPort.Links.Add(m.Link);
                    m.Link.InputId = newPort.Id;
                    missing.RemoveAt(i);
                }
                else if (m.PortType == PortType.Output)
                {
                    var newPortConfig = newConfig.OutputConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == m.OldPortConfig.TypeNormalizedFullName);
                    if (newPortConfig == null)
                        continue;

                    var newPort = newSetup.Outputs.First(x => x.ConfigId == newPortConfig.Id);
                    newPort.Links.Add(m.Link);
                    m.Link.OutputId = newPort.Id;
                    missing.RemoveAt(i);
                }
            }

            foreach (var m in missing)
                m.OtherPort.Links.Remove(m.Link);

            actorSystemSetup.ActorSetups.Remove(oldSetup);

            return missing;
        }

        public static void InstantiateAndStart(this ReflectBootstrapper reflectBootstrapper, 
            ActorSystemSetup actorSystemSetup, 
            IExposedPropertyTable resolver = null, 
            Project project = null, 
            UnityUser unityUser = null, 
            AccessToken accessToken = null)
        {
            var actorRunnerProxy = reflectBootstrapper.Systems.ActorRunner;
            actorRunnerProxy.Instantiate(actorSystemSetup, project, resolver, unityUser, accessToken, _ => { }, _ => { });
            actorRunnerProxy.StartActorSystem();
        }

        public static TActor GetActor<TActor>(this ReflectBootstrapper reflectBootstrapper)
            where TActor : class
        {
            var actorRunnerProxy = reflectBootstrapper.Systems.ActorRunner;
            return actorRunnerProxy.GetActor<TActor>();
        }

        static List<ConnectionInfo> GetConnectionInfos<TComponent, TMessage>(ActorSystemSetup actorSystemSetup,
            ActorSetup outputActorSetup)
        {
            var outputActorConfig = actorSystemSetup.GetActorConfig(outputActorSetup);

            var componentConfig = actorSystemSetup.GetComponentConfig<TComponent>();
            var messageTypeName = typeof(TMessage).ToString();
            
            var outputPortConfig = outputActorConfig.OutputConfigs.First(x => 
                x.ComponentConfigId == componentConfig.Id && x.MessageTypeNormalizedFullName == messageTypeName);
            var outputPort = outputActorSetup.Outputs.First(x => x.ConfigId == outputPortConfig.Id);
            
            var connectedSetups = outputPort.Links
                .Select(x => actorSystemSetup.ActorSetups.First(y => y.Inputs.Any(z => z.Id == x.InputId)))
                .Select(connectedSetup => GetConnectionInfo<TComponent, TMessage>(actorSystemSetup, outputActorSetup, connectedSetup))
                .ToList();

            return connectedSetups;
        }

        static ConnectionInfo GetConnectionInfo<TComponent, TMessage>(ActorSystemSetup actorSystemSetup, 
            ActorSetup outputActorSetup, ActorSetup inputActorSetup)
        {
            var outputActorConfig = actorSystemSetup.GetActorConfig(outputActorSetup);
            var inputActorConfig = actorSystemSetup.GetActorConfig(inputActorSetup);

            var componentConfig = actorSystemSetup.GetComponentConfig<TComponent>();
            var messageTypeName = typeof(TMessage).ToString();
            
            var outputPortConfig = outputActorConfig.OutputConfigs.First(x => 
                x.ComponentConfigId == componentConfig.Id && x.MessageTypeNormalizedFullName == messageTypeName);
            var inputPortConfig = inputActorConfig.InputConfigs.First(x => 
                x.ComponentConfigId == componentConfig.Id && x.MessageTypeNormalizedFullName == messageTypeName);
            
            var outputPort = outputActorSetup.Outputs.First(x => x.ConfigId == outputPortConfig.Id);
            var inputPort = inputActorSetup.Inputs.First(x => x.ConfigId == inputPortConfig.Id);

            var link = outputPort.Links.FirstOrDefault(x => x.InputId == inputPort.Id);

            return new ConnectionInfo
            {
                Link = link,
                OutputSetup = outputActorSetup,
                OutputPort = outputPort,
                OutputPortConfig = outputPortConfig,
                InputSetup = inputActorSetup,
                InputPort = inputPort,
                InputPortConfig = inputPortConfig
            };
        }

        public class MissingConnection
        {
            public PortType PortType;
            public ActorLink Link;

            public ActorSetup OldSetup;
            public ActorPort OldPort;
            public ActorPortConfig OldPortConfig;

            public ActorSetup OtherSetup;
            public ActorPort OtherPort;
            public ActorPortConfig OtherPortConfig;
        }

        public class ConnectionInfo
        {
            public ActorLink Link;

            public ActorSetup OutputSetup;
            public ActorPort OutputPort;
            public ActorPortConfig OutputPortConfig;

            public ActorSetup InputSetup;
            public ActorPort InputPort;
            public ActorPortConfig InputPortConfig;
        }
    }
}
