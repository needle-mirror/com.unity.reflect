using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Reflect.Unity.Actor;
using Unity.Reflect.Streaming;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    public static class ActorSystemSetupAnalyzer
    {
        static List<Type> s_ComponentTypes;
        static List<Type> s_SerializableComponentTypes;
        static List<Type> s_ActorTypes;
        static List<Type> s_PossibleInputAttributeTypes;
        static List<Type> s_PossibleOutputTypes;

        /// <summary>
        ///     Migrate the asset to match the code.
        /// </summary>
        /// <param name="asset"></param>
        public static void MigrateInPlace(ActorSystemSetup asset)
        {
            try
            {
                BeginInteractiveMigration(asset);

                foreach (var actorSetup in asset.ActorSetups)
                {
                    if (actorSetup.IsRemoved)
                        ThrowMigrationError(asset, $"{nameof(ActorSetup)} {actorSetup.DisplayName} has " +
                            $"been marked as removed during migration process.");
                    
                    if (actorSetup.HasSettingsTypeChanged)
                        ThrowMigrationError(asset, $"Settings type for {nameof(ActorSetup)} {actorSetup.DisplayName} has been marked as changed.");

                    ValidatePorts(asset, actorSetup, actorSetup.Inputs);
                    ValidatePorts(asset, actorSetup, actorSetup.Outputs);
                }

                CompleteInteractiveMigration(asset);
            }
            catch (Exception)
            {
                CancelInteractiveMigration(asset);
                throw;
            }
        }
        
        public static void BeginInteractiveMigration(ActorSystemSetup asset)
        {
            CancelInteractiveMigration(asset);

            var componentConfigs = GenerateComponentConfigs();
            var componentsDelta = BuildConfigsDelta(asset.ComponentConfigs, componentConfigs);
            ApplyComponentConfigsDelta(asset, componentsDelta);

            var actorConfigs = GenerateActorConfigs(asset.ComponentConfigs);
            var actorsDelta = BuildConfigsDelta(asset.ActorConfigs, actorConfigs);
            ApplyActorConfigsDelta(asset, actorsDelta);

            MarkChangedSettingsTypes(asset);
            ComputePortStates(asset);
        }

        public static void CancelInteractiveMigration(ActorSystemSetup asset)
        {
            ResetRemovedFlags(asset);
            ResetChangedFlags(asset);
            ComputePortStates(asset);
        }

        public static void CompleteInteractiveMigration(ActorSystemSetup asset)
        {
            RemoveAllRemovedElements(asset);
            UpdateSettingsTypes(asset);
            ComputePortStates(asset);
        }

        public static void InitializeActorSystemSetup(ActorSystemSetup asset)
        {
            asset.ComponentConfigs = new List<ComponentConfig>();
            asset.ActorConfigs = new List<ActorConfig>();
            asset.ActorSetups = new List<ActorSetup>();
            
            var componentConfigs = GenerateComponentConfigs();
            var actorConfigs = GenerateActorConfigs(componentConfigs);
            asset.ComponentConfigs = componentConfigs;
            asset.ActorConfigs = actorConfigs;
        }
		
        public static ActorSetup CreateActorSetup(ActorConfig actorConfig)
        {
            var actorSettingsType = GetActorSettingsType(actorConfig);
            var actorSetup = new ActorSetup
            {
                Id = Guid.NewGuid().ToString(),
                ConfigId = actorConfig.Id,
                Settings = new ActorSettings(Guid.NewGuid().ToString()),
                Inputs = new List<ActorPort>(),
                Outputs = new List<ActorPort>(),
                DisplayName = actorConfig.DisplayName
            };

            if (actorSettingsType == null)
            {
                actorSetup.Settings = new ActorSettings(Guid.NewGuid().ToString());
                return actorSetup;
            }
            
            actorSetup.Settings = (ActorSettings)Activator.CreateInstance(actorSettingsType);

            return actorSetup;
        }

        public static void Instantiate(ActorSystem system, ActorSystemSetup asset, IExposedPropertyTable resolver, UnityProject project, UnityUser user, ProjectServerClient client, Scheduler scheduler)
        {
            var externalDependencies = new Dictionary<Type, object>
            {
                { typeof(UnityProject), project },
                { typeof(UnityUser), user },
                { typeof(ProjectServerClient), client },
                { typeof(Scheduler), scheduler }
            };

            system.Dependencies = externalDependencies;

            var actorSockets = new Dictionary<ActorRef, NetComponent>();

            var actorRefs = new List<ActorRef>(asset.ActorConfigs.Count);
            foreach (var setup in asset.ActorSetups)
            {
                var actorType = GetActorType(asset, setup);
                actorRefs.Add(new ActorRef { Type = actorType });
            }

            if (actorRefs.All(x => x.Type != typeof(PubSubActor)))
                actorRefs.Add(new ActorRef { Type = typeof(PubSubActor) });

            var actors = new List<object>(asset.ActorSetups.Count);
            for (var i = 0; i < asset.ActorSetups.Count; ++i)
            {
                var setup = asset.ActorSetups[i];
                var actorType = actorRefs[i].Type;

                var actor = Activator.CreateInstance(actorType);
                actors.Add(actor);

                // Inject components
                var components = new Dictionary<Type, object>();
                var componentFields = GetReferencedComponents(actorType);
                foreach (var componentField in componentFields)
                {
                    var componentType = componentField.FieldType;
                    var component = InstantiateComponent(componentType, actorRefs[i], components, scheduler, actorSockets);
                    componentField.SetValue(actor, component);
                }

                // Always add a NetComponent if not referenced, it's required to be an actor
                // Should be automatically detected later by indirect usage of it (other components or inputs/outputs)
                if (!components.TryGetValue(typeof(NetComponent), out var netComponent))
                    netComponent = InstantiateComponent(typeof(NetComponent), actorRefs[i], components, scheduler, actorSockets);

                actorSockets.Add(actorRefs[i], (NetComponent)netComponent);

                // Inject outputs
                var fields = GetOutputFields(actorType);
                foreach (var field in fields)
                {
                    var port = GetOutputPort(asset, setup, field);
                    var otherPorts = GetConnectedInputPorts(asset, port);
                    var otherSetups = GetActorSetupsFromPorts(asset, otherPorts);
                    var receivers = otherSetups.Select(x => actorRefs[asset.ActorSetups.IndexOf(x)]).ToList();
                    var attr = GetOutputAttribute(field);

                    var runtimeOutput = new RuntimeOutput(attr.GetOutputMessageType(field), receivers);

                    var output = InstantiateOutput(runtimeOutput, actorRefs[i], field.FieldType, components, scheduler, actorSockets);
                    
                    field.SetValue(actor, output);
                }

                // Inject initial settings values
                var settingsField = GetSettingsField(actorType);
                if (settingsField != null)
                {
                    if (resolver != null)
                        CopyTransientFields(setup.Settings, resolver);
                    settingsField.SetValue(actor, setup.Settings);
                }

                var runnableComponents = components
                    .Select(x => x.Value)
                    .Where(x => x.GetType().GetInterfaces().Contains(typeof(IRunnableComponent)))
                    .Cast<IRunnableComponent>()
                    .ToArray();

                var a = CreateActorWithLifecycle(actorRefs[i], actor, runnableComponents);
                system.Add(a, components);
            }

            // Copy/paste of below. Need to refactor this function to stack the search results (methods, fields, ...) to not compute them many times
            for (var i = 0; i < asset.ActorSetups.Count; ++i)
            {
                var actorType = actorRefs[i].Type;

                // Automate Register/Subscribe calls for inputs
                var methods = GetInputMethods(actorType);
                foreach (var method in methods)
                {
                    var attr = GetInputAttribute(method);
                    var componentType = GetComponentType(attr.GetType());
                    var componentMethod = componentType.GetMethod("Register", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (componentMethod == null)
                        componentMethod = componentType.GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (componentMethod == null)
                        throw new Exception($"Component {componentType.Name} must contain a method named Register or Subscribe");

                    var components = system.RefToComponents[actorRefs[i]];
                    
                    InstantiateComponent(componentType, actorRefs[i], components, scheduler, actorSockets);
                }
            }

            // Manually add PubSubActor reference for now, need a generic way to let users plug pre-processing and post-processing
            // Initialize must be called before automatic Register calls (see below)
            var pubSubRef = actorRefs.First(x => x.Type == typeof(PubSubActor));
            foreach (var kv in system.RefToComponents)
            {
                var components = kv.Value;
                var eventComponent = components.FirstOrDefault(x => x.Key == typeof(EventComponent)).Value as EventComponent;
                eventComponent?.Initialize(pubSubRef);
            }

            for (var i = 0; i < asset.ActorSetups.Count; ++i)
            {
                var actorType = actorRefs[i].Type;

                // Automate Register/Subscribe calls for inputs
                var methods = GetInputMethods(actorType);
                foreach (var method in methods)
                {
                    var attr = GetInputAttribute(method);
                    var componentType = GetComponentType(attr.GetType());
                    var componentMethod = componentType.GetMethod("Register", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (componentMethod == null)
                        componentMethod = componentType.GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (componentMethod == null)
                        throw new Exception($"Component {componentType.Name} must contain a method named Register or Subscribe");

                    var components = system.RefToComponents[actorRefs[i]];

                    var component = components.First(x => x.Key == componentType).Value;
                    var registerFunc = componentMethod.MakeGenericMethod(attr.GetInputMessageType(method));

                    var inputDelegate = method.CreateDelegate(typeof(Action<>).MakeGenericType(attr.GetInputType(method)), actors[i]);
                    registerFunc.Invoke(component, new object[]{ inputDelegate });
                }
            }

            // Make sure bridge exists before risking to throw when IPlayerClient has been created
            // TODO: find better way to enforce bridge in Viewer
            // var bridge = system.FindActorState<BridgeActor>();
            // if (bridge == null)
            //     throw new Exception($"{nameof(BridgeActor)} is missing in {nameof(ActorSystemSetup)} {asset.name}");

            if (project != null && user != null)
                externalDependencies.Add(typeof(IPlayerClient), Player.CreateClient(project, user, client));

            // iterate on all actors, find inject method, validate that args can be added and call it. Else Dispose IPlayerClient and throw exception
            foreach (var actor in actors)
            {
                var methods = actor.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(x => x.Name == "Inject")
                    .ToList();

                if (methods.Count == 0)
                    continue;

                if (methods.Count > 1)
                    DisposeAndThrow(system, new NotSupportedException($"Actor {actor.GetType().Name} has more than one Inject method. This is not supported yet."));

                var method = methods[0];

                if (method.IsGenericMethod)
                    DisposeAndThrow(system, new NotSupportedException($"Inject method in actor {actor.GetType()} is generic, which is not supported."));

                var parameters = method.GetParameters();
                var paramValues = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; ++i)
                {
                    var p = parameters[i];
                    if (!externalDependencies.TryGetValue(p.ParameterType, out var dependency))
                        DisposeAndThrow(system, new Exception($"Actor {actor.GetType()} requires a parameter of type {p.ParameterType}, which is not available as a dependency."));
                    paramValues[i] = dependency;
                }

                method.Invoke(actor, paramValues);
            }
        }

        static Actor<object> CreateActorWithLifecycle(ActorRef actorRef, object actor, IRunnableComponent[] runnableComponents)
        {
            // Todo: memorize last executed index
            Func<object, TimeSpan, CancellationToken, bool> tick = (state, endTime, token) =>
            {
                var isCompleted = true;
                foreach (var component in runnableComponents)
                    isCompleted &= component.Tick(endTime, token);

                return isCompleted;
            };

            return new Actor<object>(actorRef, actor, new Lifecycle<object>(state => { }, state => { }, state => { }, state => { }, tick));
        }

        static void DisposeAndThrow(ActorSystem system, Exception ex)
        {
            system.DisposeDependencies();
            throw ex;
        }

        static Type GetComponentType(Type inputAttributeType)
        {
            return GetSerializableComponentTypes().FirstOrDefault(x => x.GetCustomAttribute<ComponentAttribute>().InputAttributeType == inputAttributeType);
        }

        static ActorPort GetOutputPort(ActorSystemSetup asset, ActorSetup setup, FieldInfo field)
        {
            var attr = GetOutputAttribute(field);
            var port = setup.Outputs.FirstOrDefault(x => x.Id == attr.Id);
            if (port == null)
            {
                var config = asset.ActorConfigs.First(x => x.Id == setup.ConfigId);
                var portConfig = config.OutputConfigs.First(x => x.TypeNormalizedFullName == field.FieldType.ToString());
                port = setup.Outputs.First(x => x.ConfigId == portConfig.Id);
            }

            return port;
        }

        static List<ActorPort> GetConnectedInputPorts(ActorSystemSetup asset, ActorPort port)
        {
            var result = new List<ActorPort>(port.Links.Count);
            foreach (var link in port.Links)
            {
                var otherSetup = asset.ActorSetups.First(x => x.Inputs.Any(x => x.Id == link.InputId));
                var otherPort = otherSetup.Inputs.First(x => x.Id == link.InputId);
                result.Add(otherPort);
            }

            return result;
        }

        static List<ActorSetup> GetActorSetupsFromPorts(ActorSystemSetup asset, List<ActorPort> ports)
        {
            var result = new List<ActorSetup>(ports.Count);
            foreach (var setup in asset.ActorSetups)
            {
                if (setup.Inputs.Concat(setup.Outputs).Any(x => ports.Any(y => y.Id == x.Id)))
                    result.Add(setup);
            }

            return result;
        }

        static object InstantiateOutput(RuntimeOutput runtimeOutput, ActorRef self, Type outputType, Dictionary<Type, object> components, Scheduler scheduler, Dictionary<ActorRef, NetComponent> actorSockets)
        {
            var ctors = outputType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .ToList();

            var ctor = ctors[0];
            if (ctors.Count > 1)
                ctor = ctors.FirstOrDefault(x => x.GetCustomAttribute<OutputCtorAttribute>() != null);

            if (ctor == null)
                throw new Exception($"{outputType.Name} has more than one constructor, but none of them has the {nameof(OutputCtorAttribute)}.");

            var parameters = ctor.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; ++i)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(RuntimeOutput))
                    args[i] = runtimeOutput;
                else if (GetSerializableComponentTypes().Contains(p.ParameterType))
                    args[i] = InstantiateComponent(p.ParameterType, self, components, scheduler, actorSockets);
                else
                    throw new Exception($"{p.ParameterType} is not a valid dependency for an output field.");
            }

            var output = Activator.CreateInstance(outputType, args);
            return output;
        }

        static object InstantiateComponent(Type componentType, ActorRef self, Dictionary<Type, object> components, Scheduler scheduler, Dictionary<ActorRef, NetComponent> actorSockets)
        {
            if (components.TryGetValue(componentType, out var c))
            {
                if (c == null)
                    throw new Exception($"Circular dependencies detected between component constructors (one of them is {componentType}).");
                return c;
            }
            
            components.Add(componentType, null);

            var ctors = componentType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .ToList();

            var ctor = ctors[0];
            if (ctors.Count > 1)
                ctor = ctors.FirstOrDefault(x => x.GetCustomAttribute<ComponentCtorAttribute>() != null);

            if (ctor == null)
                throw new Exception($"{componentType.Name} has more than one constructor, but none of them has the {nameof(ComponentCtorAttribute)}.");

            var parameters = ctor.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; ++i)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(ActorRef))
                    args[i] = self;
                else if (p.ParameterType == typeof(Dictionary<ActorRef, NetComponent>))
                    args[i] = actorSockets;
                else if (p.ParameterType == typeof(Scheduler))
                    args[i] = scheduler;
                else if (GetAllComponentTypes().Contains(p.ParameterType))
                    args[i] = InstantiateComponent(p.ParameterType, self, components, scheduler, actorSockets);
                else
                    throw new Exception($"{p.ParameterType} is not a valid dependency.");
            }

            var component = Activator.CreateInstance(componentType, args);
            components[componentType] = component;
            return component;
        }

        static Type GetActorType(ActorSystemSetup asset, ActorSetup setup)
        {
            var config = GetActorConfig(asset, setup);
            if (config == null)
                return null;

            return GetActorTypes().FirstOrDefault(x => x.ToString() == config.TypeNormalizedFullName);
        }

        static ActorConfig GetActorConfig(ActorSystemSetup asset, ActorSetup setup)
        {
            return asset.ActorConfigs.FirstOrDefault(x => x.Id == setup.ConfigId);
        }

        static void ThrowMigrationError(ActorSystemSetup asset, string append)
        {
            throw new Exception($"Automatic migration failed for {nameof(ActorSystemSetup)} {asset.name}. {append}");
        }

        static void ValidatePorts(ActorSystemSetup asset, ActorSetup actorSetup, List<ActorPort> ports)
        {
            foreach (var output in ports)
            {
                if (output.IsRemoved && output.Links.Count > 0)
                    ThrowMigrationError(asset, $"{nameof(ActorPort)} in {nameof(ActorSetup)} {actorSetup.DisplayName} has " +
                        $"been marked as removed during migration process, but {output.Links.Count} link(s) are connected to it.");

                if (!output.IsValid)
                    ThrowMigrationError(asset, $"{nameof(ActorPort)} in {nameof(ActorSetup)} {actorSetup.DisplayName} has " +
                        $"been marked as invalid during migration process.");

                foreach (var link in output.Links)
                    if (link.IsRemoved)
                        ThrowMigrationError(asset, $"{nameof(ActorLink)} in {nameof(ActorSetup)} {actorSetup.DisplayName} " +
                            $"has been marked as removed during migration process.");
            }
        }

        static void ResetRemovedFlags(ActorSystemSetup asset)
        {
            asset.ComponentConfigs.ForEach(x => x.IsRemoved = false);
            asset.ActorConfigs.ForEach(x =>
            {
                x.IsRemoved = false;
                foreach (var portConfig in x.InputConfigs.Concat(x.OutputConfigs))
                    portConfig.IsRemoved = false;
            });
            asset.ActorSetups.ForEach(x =>
            {
                x.IsRemoved = false;
                foreach (var port in x.Inputs.Concat(x.Outputs))
                {
                    port.IsRemoved = false;
                    port.Links.ForEach(x => x.IsRemoved = false);
                }
            });
        }

        static void ResetChangedFlags(ActorSystemSetup asset)
        {
            foreach (var setup in asset.ActorSetups)
                setup.HasSettingsTypeChanged = false;
        }

        static List<ComponentConfig> GenerateComponentConfigs()
        {
            var componentTypes = GetSerializableComponentTypes();
            var result = new List<ComponentConfig>(componentTypes.Count);

            foreach (var componentType in componentTypes)
            {
                var attr = componentType.GetCustomAttribute<ComponentAttribute>();
                var id = attr.Id;
                var isGeneratedId = false;
                var displayName = attr.DisplayName;
                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                    isGeneratedId = true;
                }

                if (displayName == null)
                    displayName = componentType.Name;

                result.Add(new ComponentConfig(id, isGeneratedId, componentType.ToString(), attr.InputMultiplicity, attr.OutputMultiplicity, displayName, false));
            }

            return result;
        }

        static List<Type> GetAllComponentTypes()
        {
            if (s_ComponentTypes == null)
            {
                s_ComponentTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                    .Where(type =>
                        !type.IsAbstract &&
                        type.GetCustomAttribute<ComponentAttribute>() != null ||
                        type.GetInterfaces().Contains(typeof(IRunnableComponent)))
                    .ToList();
            }

            return s_ComponentTypes;
        }

        static List<Type> GetSerializableComponentTypes()
        {
            if (s_SerializableComponentTypes == null)
            {
                s_SerializableComponentTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                    .Where(type =>
                        !type.IsAbstract &&
                        type.GetCustomAttribute<ComponentAttribute>() != null &&
                        !type.GetCustomAttribute<ComponentAttribute>().IsExcludedFromGraph)
                    .ToList();
            }

            return s_SerializableComponentTypes;
        }

        static Delta<T> BuildConfigsDelta<T>(List<T> oldConfigs, List<T> newConfigs)
            where T : class, IConfigIdentifier
        {
            var diff = new Delta<T>();
            var oldCopy = new List<T>(oldConfigs);

            foreach (var newConfig in newConfigs)
            {
                var oldConfig = oldConfigs.FirstOrDefault(x => x.Id == newConfig.Id) ??
                    oldConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == newConfig.TypeNormalizedFullName);
                
                if (oldConfig == null)
                    diff.Added.Add(newConfig);
                else
                {
                    diff.Changed.Add((oldConfig, newConfig));
                    oldCopy.Remove(oldConfig);
                }
            }

            foreach (var oldConfig in oldCopy)
            {
                var newConfig = newConfigs.FirstOrDefault(x => x.Id == oldConfig.Id) ??
                    newConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == oldConfig.TypeNormalizedFullName);
                
                if (newConfig != null)
                {
                    if (diff.Changed.Any(x => x.NewValue == newConfig))
                        diff.Removed.Add(oldConfig);
                }
                else
                    diff.Removed.Add(oldConfig);
            }

            return diff;
        }
        
        static void ApplyComponentConfigsDelta(ActorSystemSetup asset, Delta<ComponentConfig> delta)
        {
            foreach (var added in delta.Added)
                asset.ComponentConfigs.Add(added);

            foreach (var removed in delta.Removed)
            {
                removed.IsRemoved = true;

                // Remove PortConfig attached to this ComponentConfig
                foreach (var actorConfig in asset.ActorConfigs)
                {
                    var portConfigs = actorConfig.OutputConfigs.Concat(actorConfig.InputConfigs);
                    foreach (var portConfig in portConfigs)
                    {
                        if (portConfig.ComponentConfigId == removed.Id)
                            portConfig.IsRemoved = true;
                    }
                }
                
                // Remove detached ActorPort
                foreach (var setup in asset.ActorSetups)
                {
                    var actorConfig = asset.ActorConfigs.First(x => x.Id == setup.ConfigId);
                    if (actorConfig == null)
                    {
                        MarkActorSetupAsRemoved(setup);
                        continue;
                    }

                    var ports = setup.Outputs.Concat(setup.Inputs);
                    foreach (var port in ports)
                    {
                        var portConfig = actorConfig.InputConfigs.Concat(actorConfig.OutputConfigs).FirstOrDefault(x => x.Id == port.ConfigId);
                        if (portConfig == null || portConfig.IsRemoved)
                            MarkPortAsRemoved(port);
                    }
                }
            }

            foreach (var changed in delta.Changed)
            {
                var oldVal = changed.OldValue;
                var newVal = changed.NewValue;

                oldVal.IsGeneratedId = newVal.IsGeneratedId;
                oldVal.TypeNormalizedFullName = newVal.TypeNormalizedFullName;
                oldVal.DisplayName = newVal.DisplayName;

                if (oldVal.InputMultiplicity != newVal.InputMultiplicity)
                {
                    var affectedPortConfigs = new List<ActorPortConfig>();
                    foreach (var actorConfig in asset.ActorConfigs)
                        affectedPortConfigs.AddRange(actorConfig.InputConfigs.Where(x => x.ComponentConfigId == oldVal.Id));

                    foreach (var setup in asset.ActorSetups)
                    {
                        var affectedPorts = setup.Inputs.Where(x => affectedPortConfigs.Any(y => y.Id == x.ConfigId));
                        foreach (var affectedPort in affectedPorts)
                        {
                            if (MultiplicityValidator.IsValid(newVal.InputMultiplicity, affectedPort.Links.Count))
                            {
                                affectedPort.Links.ForEach(x => x.IsRemoved = true);
                            }
                        }
                    }
                }
                if (oldVal.OutputMultiplicity != newVal.OutputMultiplicity)
                {
                    var affectedPortConfigs = new List<ActorPortConfig>();
                    foreach (var actorConfig in asset.ActorConfigs)
                        affectedPortConfigs.AddRange(actorConfig.OutputConfigs.Where(x => x.ComponentConfigId == oldVal.Id));

                    foreach (var setup in asset.ActorSetups)
                    {
                        var affectedPorts = setup.Outputs.Where(x => affectedPortConfigs.Any(y => y.Id == x.ConfigId));
                        foreach (var affectedPort in affectedPorts)
                        {
                            if (MultiplicityValidator.IsValid(newVal.InputMultiplicity, affectedPort.Links.Count))
                            {
                                affectedPort.Links.ForEach(x => x.IsRemoved = true);
                            }
                        }
                    }
                }
                oldVal.InputMultiplicity = newVal.InputMultiplicity;
                oldVal.OutputMultiplicity = newVal.OutputMultiplicity;

                if (!newVal.IsGeneratedId && oldVal.Id != newVal.Id)
                {
                    foreach (var actorConfig in asset.ActorConfigs)
                    {
                        var portConfigs = actorConfig.InputConfigs.Concat(actorConfig.OutputConfigs);
                        foreach (var portConfig in portConfigs)
                        {
                            if (portConfig.ComponentConfigId == oldVal.Id)
                                portConfig.ComponentConfigId = newVal.Id;
                            // Todo: must force an update on port validation, IsValid may be false after this change in a port
                        }
                    }

                    oldVal.Id = newVal.Id;
                }
            }
        }

        static void MarkActorSetupAsRemoved(ActorSetup setup)
        {
            setup.IsRemoved = true;
            var ports = setup.Outputs.Concat(setup.Inputs);
            foreach (var port in ports)
                MarkPortAsRemoved(port);
        }

        static void MarkPortAsRemoved(ActorPort port)
        {
            port.IsRemoved = true;
            foreach (var link in port.Links)
                link.IsRemoved = true;
        }

        static void ApplyActorConfigsDelta(ActorSystemSetup asset, Delta<ActorConfig> delta)
        {
            foreach (var added in delta.Added)
                asset.ActorConfigs.Add(added);
            
            foreach (var removed in delta.Removed)
            {
                removed.IsRemoved = true;

                foreach (var setup in asset.ActorSetups)
                {
                    if (setup.ConfigId == removed.Id)
                        MarkActorSetupAsRemoved(setup);
                }
            }

            foreach (var changed in delta.Changed)
            {
                var oldVal = changed.OldValue;
                var newVal = changed.NewValue;

                var inputDelta = BuildPortConfigsDelta(oldVal.InputConfigs, newVal.InputConfigs);
                ApplyPortConfigsDelta(asset, oldVal, inputDelta);

                var outputDelta = BuildPortConfigsDelta(oldVal.OutputConfigs, newVal.OutputConfigs);
                ApplyPortConfigsDelta(asset, oldVal, outputDelta);

                oldVal.IsGeneratedId = newVal.IsGeneratedId;
                oldVal.TypeNormalizedFullName = newVal.TypeNormalizedFullName;
                oldVal.IsBoundToMainThread = newVal.IsBoundToMainThread;
                oldVal.GroupName = newVal.GroupName;

                if (oldVal.DisplayName != newVal.DisplayName)
                {
                    foreach (var setup in asset.ActorSetups)
                    {
                        if (setup.ConfigId == oldVal.Id)
                            setup.DisplayName = newVal.DisplayName;
                    }

                    oldVal.DisplayName = newVal.DisplayName;
                }

                if (!newVal.IsGeneratedId && oldVal.Id != newVal.Id)
                {
                    foreach (var setup in asset.ActorSetups)
                    {
                        if (setup.ConfigId == oldVal.Id)
                            setup.ConfigId = newVal.Id;
                    }

                    oldVal.Id = newVal.Id;
                }
            }
        }

        static Delta<ActorPortConfig> BuildPortConfigsDelta(List<ActorPortConfig> oldConfigs, List<ActorPortConfig> newConfigs)
        {
            var diff = new Delta<ActorPortConfig>();
            var oldCopy = new List<ActorPortConfig>(oldConfigs);

            foreach (var newConfig in newConfigs)
            {
                var oldConfig = oldConfigs.FirstOrDefault(x => x.Id == newConfig.Id) ??
                    oldConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == newConfig.TypeNormalizedFullName) ??
                    oldConfigs.FirstOrDefault(x => x.MemberName == newConfig.MemberName);

                if (oldConfig == null)
                    diff.Added.Add(newConfig);
                else
                {
                    diff.Changed.Add((oldConfig, newConfig));
                    oldCopy.Remove(oldConfig);
                }
            }

            foreach (var oldConfig in oldCopy)
            {
                var newConfig = newConfigs.FirstOrDefault(x => x.Id == oldConfig.Id) ??
                    newConfigs.FirstOrDefault(x => x.TypeNormalizedFullName == oldConfig.TypeNormalizedFullName) ??
                    newConfigs.FirstOrDefault(x => x.MemberName == oldConfig.MemberName);

                if (newConfig != null)
                {
                    if (diff.Changed.Any(x => x.NewValue == newConfig))
                        diff.Removed.Add(oldConfig);
                }
                else
                    diff.Removed.Add(oldConfig);
            }

            return diff;
        }

        static void ApplyPortConfigsDelta(ActorSystemSetup asset, ActorConfig actorConfig, Delta<ActorPortConfig> delta)
        {
            foreach (var added in delta.Added)
            {
                if (added.PortType == PortType.Input)
                    actorConfig.InputConfigs.Add(added);
                else if (added.PortType == PortType.Output)
                    actorConfig.OutputConfigs.Add(added);

                foreach (var actorSetup in asset.ActorSetups)
                {
                    if (actorSetup.ConfigId == actorConfig.Id)
                    {
                        var port = new ActorPort(Guid.NewGuid().ToString(), added.Id, new List<ActorLink>(), true, false);
                        if (added.PortType == PortType.Input)
                            actorSetup.Inputs.Add(port);
                        else if (added.PortType == PortType.Output)
                            actorSetup.Outputs.Add(port);
                    }
                }
            }

            foreach (var removed in delta.Removed)
            {
                removed.IsRemoved = true;

                foreach (var actorSetup in asset.ActorSetups)
                {
                    if (actorSetup.ConfigId == actorConfig.Id)
                    {
                        var port = actorSetup.Inputs.Concat(actorSetup.Outputs).FirstOrDefault(x => x.ConfigId == removed.Id);
                        if (port != null)
                            MarkPortAsRemoved(port);
                    }
                }
            }

            foreach (var changed in delta.Changed)
            {
                var oldVal = changed.OldValue;
                var newVal = changed.NewValue;

                oldVal.IsGeneratedId = newVal.IsGeneratedId;
                oldVal.TypeNormalizedFullName = newVal.TypeNormalizedFullName;
                oldVal.MemberName = newVal.MemberName;
                oldVal.PortType = newVal.PortType;
                oldVal.DisplayName = newVal.DisplayName;

                // Update links for all port for this PortConfig when something that participate in the connection constraints changes
                var attributeChanged = oldVal.ComponentConfigId != newVal.ComponentConfigId;
                var messageTypeChanged = oldVal.MessageTypeNormalizedFullName != newVal.MessageTypeNormalizedFullName;
                if (attributeChanged || messageTypeChanged)
                {
                    foreach (var setup in asset.ActorSetups)
                    {
                        if (setup.ConfigId != actorConfig.Id)
                            continue;

                        var port = setup.Inputs.Concat(setup.Outputs).FirstOrDefault(x => x.ConfigId == oldVal.Id);
                        if (port == null)
                            continue;

                        var portConfig = actorConfig.InputConfigs.Concat(actorConfig.OutputConfigs).FirstOrDefault(x => x.Id == port.ConfigId);
                        if (portConfig == null)
                            continue;

                        foreach (var link in port.Links)
                        {
                            ActorSetup otherActorSetup;
                            ActorPort otherActorPort;
                            if (portConfig.PortType == PortType.Input)
                            {
                                otherActorSetup = asset.ActorSetups.FirstOrDefault(x => x.Outputs.Any(x => x.Id == link.OutputId));
                                if (otherActorSetup == null)
                                {
                                    link.IsRemoved = true;
                                    continue;
                                }

                                otherActorPort = otherActorSetup.Outputs.FirstOrDefault(x => x.Id == link.OutputId);
                            }
                            else
                            {
                                otherActorSetup = asset.ActorSetups.FirstOrDefault(x => x.Inputs.Any(x => x.Id == link.InputId));
                                if (otherActorSetup == null)
                                {
                                    link.IsRemoved = true;
                                    continue;
                                }

                                otherActorPort = otherActorSetup.Inputs.FirstOrDefault(x => x.Id == link.InputId);
                            }

                            if (otherActorPort == null)
                            {
                                link.IsRemoved = true;
                                continue;
                            }

                            var otherActorConfig = asset.ActorConfigs.FirstOrDefault(x => x.Id == otherActorSetup.ConfigId);

                            var otherPortConfig = otherActorConfig?.InputConfigs.Concat(otherActorConfig.OutputConfigs).FirstOrDefault(x => x.Id == otherActorPort.ConfigId);
                            if (otherPortConfig == null)
                            {
                                link.IsRemoved = true;
                                continue;
                            }

                            if (otherPortConfig.ComponentConfigId != newVal.ComponentConfigId ||
                                otherPortConfig.MessageTypeNormalizedFullName != newVal.MessageTypeNormalizedFullName)
                                link.IsRemoved = true;
                            else
                                link.IsRemoved = false;
                        }
                    }

                    oldVal.ComponentConfigId = newVal.ComponentConfigId;
                    oldVal.MessageTypeNormalizedFullName = newVal.MessageTypeNormalizedFullName;
                }

                if (!newVal.IsGeneratedId && oldVal.Id != newVal.Id)
                {
                    foreach (var actorSetup in asset.ActorSetups)
                    {
                        if (actorSetup.ConfigId == actorConfig.Id)
                            continue;

                        var port = actorSetup.Inputs.Concat(actorSetup.Outputs).FirstOrDefault(x => x.ConfigId == oldVal.Id);
                        if (port != null)
                            port.ConfigId = newVal.Id;
                    }

                    oldVal.Id = newVal.Id;
                }
            }
        }

        static void MarkChangedSettingsTypes(ActorSystemSetup asset)
        {
            foreach (var actorSetup in asset.ActorSetups)
            {
                var actorConfig = asset.ActorConfigs.First(x => x.Id == actorSetup.ConfigId);
                var actorSettingsType = GetActorSettingsType(actorConfig);

                if (actorSettingsType == null)
                {
                    actorSettingsType = typeof(ActorSettings);
                    var oldId = Guid.NewGuid().ToString();
                    if (actorSetup.Settings != null &&
                        !string.IsNullOrEmpty(actorSetup.Settings.Id) &&
                        actorSetup.Settings.Id != Guid.Empty.ToString())
                        oldId = actorSetup.Settings.Id;

                    actorSetup.Settings = new ActorSettings(Guid.NewGuid().ToString());
                    actorSetup.Settings.Id = oldId;
                }

                actorSetup.HasSettingsTypeChanged = false;
                
                if (actorSetup.Settings?.GetType() != actorSettingsType)
                    actorSetup.HasSettingsTypeChanged = true;
            }
        }

        static void ComputePortStates(ActorSystemSetup asset)
        {
            foreach (var setup in asset.ActorSetups)
            {
                var actorConfig = asset.ActorConfigs.First(x => x.Id == setup.ConfigId);
                foreach (var port in setup.Inputs)
                {
                    var portConfig = actorConfig.InputConfigs.First(x => x.Id == port.ConfigId);
                    var componentConfig = asset.ComponentConfigs.First(x => x.Id == portConfig.ComponentConfigId);
                    var multiplicity = componentConfig.InputMultiplicity;
                    port.IsValid = MultiplicityValidator.IsValid(multiplicity, port.Links.Count(x => !x.IsRemoved));
                }

                foreach (var port in setup.Outputs)
                {
                    var portConfig = actorConfig.OutputConfigs.First(x => x.Id == port.ConfigId);
                    var componentConfig = asset.ComponentConfigs.First(x => x.Id == portConfig.ComponentConfigId);
                    var multiplicity = componentConfig.OutputMultiplicity;
                    port.IsValid = MultiplicityValidator.IsValid(multiplicity, port.Links.Count(x => !x.IsRemoved));
                }
            }
        }

        static void UpdateSettingsTypes(ActorSystemSetup asset)
        {
            foreach (var actorSetup in asset.ActorSetups)
            {
                var actorConfig = asset.ActorConfigs.First(x => x.Id == actorSetup.ConfigId);
                var actorSettingsType = GetActorSettingsType(actorConfig);

                if (actorSettingsType == null)
                {
                    actorSettingsType = typeof(ActorSettings);
                    var oldId = Guid.NewGuid().ToString();
                    if (actorSetup.Settings != null &&
                        !string.IsNullOrEmpty(actorSetup.Settings.Id) &&
                        actorSetup.Settings.Id != Guid.Empty.ToString())
                        oldId = actorSetup.Settings.Id;

                    actorSetup.Settings = new ActorSettings(Guid.NewGuid().ToString());
                    actorSetup.Settings.Id = oldId;
                }

                var settings = actorSetup.Settings;
                actorSetup.HasSettingsTypeChanged = false;
                
                if (settings?.GetType() != actorSettingsType)
                    actorSetup.Settings = (ActorSettings)Activator.CreateInstance(actorSettingsType);
            }
        }

        static void RemoveAllRemovedElements(ActorSystemSetup asset)
        {
            asset.ComponentConfigs.RemoveAll(x => x.IsRemoved);
            asset.ActorConfigs.RemoveAll(x => x.IsRemoved);
            asset.ActorSetups.RemoveAll(x => x.IsRemoved);

            foreach (var actorConfig in asset.ActorConfigs)
            {
                actorConfig.OutputConfigs.RemoveAll(x => x.IsRemoved);
                actorConfig.InputConfigs.RemoveAll(x => x.IsRemoved);
            }

            foreach (var actorSetup in asset.ActorSetups)
            {
                actorSetup.Outputs.RemoveAll(x => x.IsRemoved);
                actorSetup.Inputs.RemoveAll(x => x.IsRemoved);

                foreach (var port in actorSetup.Outputs)
                    port.Links.RemoveAll(x => x.IsRemoved);

                foreach (var port in actorSetup.Inputs)
                    port.Links.RemoveAll(x => x.IsRemoved);
            }
        }
        
        static List<ActorConfig> GenerateActorConfigs(List<ComponentConfig> componentConfigs)
        {
            var actorTypes = GetActorTypes();

            var result = new List<ActorConfig>(actorTypes.Count);
            foreach (var actorType in actorTypes)
            {
                var id = TryGetActorId(actorType);
                var isGenerated = false;
                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                    isGenerated = true;
                }

                var attr = GetActorAttribute(actorType);
                
                var config = new ActorConfig(id, isGenerated, actorType.ToString(), GetInputPortConfigs(actorType, componentConfigs),
                    GetOutputPortConfigs(actorType, componentConfigs), attr.IsBoundToMainThread, GetActorGroupName(actorType), GetActorDisplayName(actorType), false);

                result.Add(config);
            }

            return result;
        }

        static ActorAttribute GetActorAttribute(Type actorType)
        {
            return actorType.GetCustomAttribute<ActorAttribute>() ?? new ActorAttribute();
        }

        static List<Type> GetActorTypes()
        {
            if (s_ActorTypes == null)
            {
                s_ActorTypes = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(x => x.GetTypes())
                    .Where(type => !type.IsAbstract && type.GetCustomAttribute<ActorAttribute>() != null)
                    .ToList();
            }

            return s_ActorTypes;
        }

        public static Type GetActorSettingsType(ActorConfig actorConfig)
        {
            var actorType = GetActorTypes()
                .FirstOrDefault(x => x.ToString() == actorConfig.TypeNormalizedFullName);

            if (actorType == null)
                return null;

            return GetSettingsField(actorType)?.FieldType;
        }

        static void CopyTransientFields(object settings, IExposedPropertyTable resolver)
        {
            var transientFields = settings.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => x.GetCustomAttribute<TransientAttribute>() != null)
                .ToList();

            foreach (var transientField in transientFields)
            {
                var destinationField = settings.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(x => x.Name == transientField.GetCustomAttribute<TransientAttribute>().FieldName);

                if (destinationField == null)
                    throw new Exception($"Missing destination field for transient field {transientField.Name}. Field with " +
                        $"name {transientField.GetCustomAttribute<TransientAttribute>().FieldName} does not exist in class {settings.GetType().Name}.");
                
                if (transientField.FieldType.IsGenericType)
                {
                    if (transientField.FieldType.GetGenericTypeDefinition() == typeof(ExposedReference<>))
                    {
                        var method = transientField.FieldType
                            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                            .First();

                        var result = method.Invoke(transientField.GetValue(settings), new object[] { resolver });
                        destinationField.SetValue(settings, result);
                        continue;
                    }
                }

                throw new NotSupportedException($"Transient field {transientField.Name} is not of type {typeof(ExposedReference<>).Name}. Only this type is supported.");
            }
        }

        static FieldInfo GetSettingsField(Type actorType)
        {
            var field = actorType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(x => x.FieldType.IsSubclassOf(typeof(ActorSettings)));

            return field;
        }

        static string TryGetActorId(Type actorType)
        {
            return actorType.GetCustomAttribute<ActorAttribute>()?.Id;
        }

        static string GetActorGroupName(Type actorType)
        {
            return actorType.GetCustomAttribute<ActorAttribute>()?.GroupName ?? "Others";
        }

        static string GetActorDisplayName(Type actorType)
        {
            return actorType.GetCustomAttribute<ActorAttribute>()?.DisplayName ?? actorType.Name;
        }

        static List<ActorPortConfig> GetInputPortConfigs(Type actorType, List<ComponentConfig> componentConfigs)
        {
            var methods = GetInputMethods(actorType);

            var configs = new List<ActorPortConfig>(methods.Count);
            foreach (var method in methods)
            {
                var id = TryGetInputId(method);
                var isGenerated = false;

                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                    isGenerated = true;
                }

                var attr = GetInputAttribute(method);
                
                var componentTypes = GetSerializableComponentTypes();
                var componentType = componentTypes.First(x => x.GetCustomAttribute<ComponentAttribute>().InputAttributeType == attr.GetType());
                var componentConfig = componentConfigs.First(x => x.TypeNormalizedFullName == componentType.ToString());

                var componentName = componentConfig.DisplayName ?? componentType.Name;
                var componentConfigId = componentConfig.Id;
                var msgType = attr.GetInputMessageType(method);
                var inputType = attr.GetInputType(method);
                var displayName = attr.DisplayName;
                if (displayName == null)
                {
                    displayName = method.Name;
                    if (displayName.StartsWith("On") && displayName.Length > 2)
                        displayName = displayName.Substring(2, displayName.Length - 2);
                }
                //displayName = $"{displayName}  ({componentName} -> {PrettyTypeName(msgType)})"; Todo: Additional info must be displayed in tooltip

                configs.Add(new ActorPortConfig(id, isGenerated, componentConfigId, inputType.ToString(), msgType.ToString(), method.Name, PortType.Input, displayName, false));
            }

            return configs;
        }

        static IInputAttribute GetInputAttribute(MethodInfo method)
        {
            var attr = (IInputAttribute)method
                .GetCustomAttributes()
                .First(x => x.GetType().GetInterfaces().Contains(typeof(IInputAttribute)));

            return attr;
        }

        static List<ActorPortConfig> GetOutputPortConfigs(Type actorType, List<ComponentConfig> componentConfigs)
        {
            var fields = GetOutputFields(actorType);

            var configs = new List<ActorPortConfig>(fields.Count);
            foreach (var field in fields)
            {
                var id = TryGetOutputId(field);
                var isGenerated = false;

                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                    isGenerated = true;
                }

                var attr = GetOutputAttribute(field);

                var componentTypes = GetSerializableComponentTypes();
                var componentType = componentTypes.First(x => x.GetCustomAttribute<ComponentAttribute>().OutputAttributeType == attr.GetType());
                var componentConfig = componentConfigs.First(x => x.TypeNormalizedFullName == componentType.ToString());

                var componentName = componentConfig.DisplayName ?? componentType.Name;
                var componentConfigId = componentConfig.Id;
                var msgType = attr.GetOutputMessageType(field);
                var displayName = attr.DisplayName;
                if (displayName == null)
                {
                    displayName = $"{PrettyFieldName(field.Name)}";
                    if (displayName.EndsWith("Output") && displayName.Length > 6)
                        displayName = displayName.Substring(0, displayName.Length - 6);
                }
                //displayName = $"({componentName} -> {PrettyTypeName(msgType)}) {displayName}"; // Todo: additional info should be displayed in tooltip
                
                configs.Add(new ActorPortConfig(id, isGenerated, componentConfigId, field.FieldType.ToString(),
                    msgType.ToString(), field.Name, PortType.Output, displayName, false));
            }

            return configs;
        }

        static IOutputAttribute GetOutputAttribute(FieldInfo field)
        {
            var attr = (IOutputAttribute)field
                .GetCustomAttributes()
                .FirstOrDefault(x => x.GetType().GetInterfaces().Contains(typeof(IOutputAttribute)));

            if (attr == null)
            {
                var fieldType = field.FieldType;
                if (fieldType.IsGenericType)
                    fieldType = fieldType.GetGenericTypeDefinition();
                    
                var attrType = GetSerializableComponentTypes()
                    .First(x => x.GetCustomAttribute<ComponentAttribute>().OutputType == fieldType)
                    .GetCustomAttribute<ComponentAttribute>()
                    .OutputAttributeType;

                attr = (IOutputAttribute)Activator.CreateInstance(attrType);
            }

            return attr;
        }

        static string PrettyTypeName(Type type)
        {
            var sb = new StringBuilder();
            PrettyTypeName(sb, type);
            return sb.ToString();
        }

        static void PrettyTypeName(StringBuilder sb, Type type)
        {
            if (!type.IsGenericType)
            {
                sb.Append(type.Name);
                return;
            }

            sb.Append(type.Name.Substring(0, type.Name.IndexOf("`")));
            sb.Append("<");

            var genericArgs = type.GetGenericArguments();
            foreach (var arg in genericArgs)
            {
                PrettyTypeName(sb, arg);
                sb.Append(", ");
            }

            sb.Remove(sb.Length - 2, 2);

            sb.Append(">");
        }

        static string PrettyFieldName(string fieldName)
        {
            var prefixLength = 0;
            if (fieldName.StartsWith("m_"))
                prefixLength = 2;
            else if (fieldName.StartsWith("_"))
                prefixLength = 1;

            return fieldName.Substring(prefixLength);
        }

        static string TryGetInputId(MethodInfo method)
        {
            var attr = GetInputAttribute(method);
            return attr?.Id;
        }

        static string TryGetOutputId(FieldInfo field)
        {
            var attr = (IOutputAttribute)field
                .GetCustomAttributes()
                .FirstOrDefault(x => x.GetType().GetInterfaces().Contains(typeof(IOutputAttribute)));

            return attr?.Id;
        }

        static List<MethodInfo> GetInputMethods(Type actorType)
        {
            var possibleInputAttributeTypes = GetPossibleInputAttributeTypes();

            var methods = actorType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                .Where(x => x.GetCustomAttributes().Any(x => possibleInputAttributeTypes.Contains(x.GetType())))
                .ToList();

            if (actorType.BaseType != null &&
                actorType.BaseType.GetCustomAttribute<ActorAttribute>() != null)
            {
                methods.AddRange(GetInputMethods(actorType.BaseType));
            }

            return methods;
        }

        static List<FieldInfo> GetOutputFields(Type actorType)
        {
            var possibleOutputTypes = GetPossibleOutputTypes();

            var fields = actorType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x =>
                {
                    var fieldType = x.FieldType;
                    if (fieldType.IsGenericType)
                        fieldType = fieldType.GetGenericTypeDefinition();

                    return possibleOutputTypes.Contains(fieldType);
                })
                .ToList();

            if (actorType.BaseType != null &&
                actorType.BaseType.GetCustomAttribute<ActorAttribute>() != null)
            {
                fields.AddRange(GetOutputFields(actorType.BaseType));
            }

            return fields;
        }

        static List<FieldInfo> GetReferencedComponents(Type actorType)
        {
            return actorType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => GetAllComponentTypes().Contains(x.FieldType))
                .ToList();
        }

        static List<Type> GetPossibleInputAttributeTypes()
        {
            if (s_PossibleInputAttributeTypes == null)
            {
                s_PossibleInputAttributeTypes = GetSerializableComponentTypes()
                    .Select(x => x.GetCustomAttribute<ComponentAttribute>().InputAttributeType)
                    .ToList();
            }

            return s_PossibleInputAttributeTypes;
        }

        static List<Type> GetPossibleOutputTypes()
        {
            if (s_PossibleOutputTypes == null)
            {
                s_PossibleOutputTypes = GetSerializableComponentTypes()
                    .Select(x => x.GetCustomAttribute<ComponentAttribute>().OutputType)
                    .ToList();
            }

            return s_PossibleOutputTypes;
        }

        class Delta<T>
        {
            public List<T> Added = new List<T>();
            public List<T> Removed = new List<T>();
            public List<(T OldValue, T NewValue)> Changed = new List<(T, T)>();
        }
    }
}
