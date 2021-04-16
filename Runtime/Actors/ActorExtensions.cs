using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actor
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
            var componentConfig = actorSystemSetup.GetComponentConfig<TComponent>();
            var messageTypeName = typeof(TMessage).ToString();

            var outputActorConfig = actorSystemSetup.GetActorConfig(outputActorSetup);
            var inputActorConfig = actorSystemSetup.GetActorConfig(inputActorSetup);
            
            var outputPortConfig = outputActorConfig.OutputConfigs.First(x => 
                x.ComponentConfigId == componentConfig.Id && x.MessageTypeNormalizedFullName == messageTypeName);
            var inputPortConfig = inputActorConfig.InputConfigs.First(x => 
                x.ComponentConfigId == componentConfig.Id && x.MessageTypeNormalizedFullName == messageTypeName);
            
            var outputPort = outputActorSetup.Outputs.First(x => x.ConfigId == outputPortConfig.Id);
            var inputPort = inputActorSetup.Inputs.First(x => x.ConfigId == inputPortConfig.Id);
            
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

        public static void InstantiateAndStart(this ReflectBootstrapper reflectBootstrapper, 
            ActorSystemSetup actorSystemSetup, 
            IExposedPropertyTable resolver = null, 
            UnityProject unityProject = null, 
            UnityUser unityUser = null)
        {
            var actorRunnerProxy = reflectBootstrapper.systems.ActorRunner;
            actorRunnerProxy.Instantiate(actorSystemSetup, unityProject, resolver, unityUser);
            actorRunnerProxy.StartActorSystem();
        }

        public static T FindActor<T>(this ReflectBootstrapper reflectBootstrapper) where T : class
        {
            var actorRunnerProxy = reflectBootstrapper.systems.ActorRunner;
            return actorRunnerProxy.FindActorState<T>();
        }
    }
}
