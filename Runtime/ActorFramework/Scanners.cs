using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.Reflect.ActorFramework
{
    public interface IActorSystemDescriptorScanner
    {
        ActorSystemDescriptor Scan(List<Assembly> searchAssemblies);
    }

    public class ActorSystemDescriptorScanner : IActorSystemDescriptorScanner
    {
        protected List<IActorDescriptorScanner> m_ActorDescriptorScanners;
        protected List<IComponentDescriptorScanner> m_ComponentDescriptorScanners;

        public ActorSystemDescriptorScanner(List<IActorDescriptorScanner> actors, List<IComponentDescriptorScanner> components)
        {
            m_ActorDescriptorScanners = actors;
            m_ComponentDescriptorScanners = components;
        }

        public virtual ActorSystemDescriptor Scan(List<Assembly> searchAssemblies)
        {
            var descriptor = new ActorSystemDescriptor();
            
            descriptor.ComponentDescriptors.AddRange(m_ComponentDescriptorScanners.SelectMany(x => x.Scan(searchAssemblies)));
            descriptor.ActorDescriptors.AddRange(m_ActorDescriptorScanners.SelectMany(x => x.Scan(searchAssemblies, descriptor.ComponentDescriptors)));

            return descriptor;
        }
    }

    public interface IActorDescriptorScanner
    {
        List<ActorDescriptor> Scan(List<Assembly> searchAssemblies, List<ComponentDescriptor> components);
    }

    public class ActorDescriptorScanner : IActorDescriptorScanner
    {
        protected List<IPortDescriptorScanner> m_PortDescriptorScanners;

        public ActorDescriptorScanner(List<IPortDescriptorScanner> ports)
        {
            m_PortDescriptorScanners = ports;
        }
        
        public List<ActorDescriptor> Scan(List<Assembly> searchAssemblies, List<ComponentDescriptor> components)
        {
            var actors = searchAssemblies
                .SelectMany(x => x.GetTypes())
                .Where(x => x.GetCustomAttribute<ActorAttribute>() != null)
                .Select(x => new ActorDescriptor
                {
                    Id = GetActorId(x),
                    Type = x
                })
                .ToList();

            foreach (var actor in actors)
                actor.Ports = m_PortDescriptorScanners
                    .SelectMany(y => y.Scan(actor, components))
                    .ToList();
            
            return actors;
        }

        static Guid GetActorId(Type actorType)
        {
            var id = actorType.GetCustomAttribute<ActorAttribute>().Id;
            return id == null ? Guid.NewGuid() : Guid.Parse(id);
        }
    }
    
    public interface IPortDescriptorScanner
    {
        List<PortDescriptor> Scan(ActorDescriptor actor, List<ComponentDescriptor> components);
    }

    public class PortDescriptorScanner : IPortDescriptorScanner
    {
        List<ComponentDescriptor> m_Components;
        List<Type> m_OutputTypes;
        List<Type> m_InputAttrTypes;

        public List<PortDescriptor> Scan(ActorDescriptor actor, List<ComponentDescriptor> components)
        {
            if (m_Components == null || !m_Components.SequenceEqual(components))
            {
                m_Components = components;
                m_OutputTypes = null;
                m_InputAttrTypes = null;
            }
            
            var fields = GetOutputFields(actor.Type);
            var methods = GetInputMethods(actor.Type);

            var ports = fields
                .Select(x =>
                {
                    var attr = GetOutputAttribute(x);
                    var id = GetOutputPortId(x, out var isGenerated);
                    return new PortDescriptor
                    {
                        Id = id,
                        IsGeneratedId = isGenerated,
                        IsVirtual = false,
                        PortType = PortType.Output,
                        SyntaxTreeName = x.Name,
                        DisplayName = attr.DisplayName ?? Prettify(x.Name),
                        Type = x.FieldType,
                        LinkTypes = attr.GetLinkTypes(x)
                    };
                })
                .Concat(methods.Select(x =>
                {
                    var attr = GetInputAttribute(x);
                    var id = GetInputPortId(x, out var isGenerated);
                    return new PortDescriptor
                    {
                        Id = id,
                        IsGeneratedId = isGenerated,
                        IsVirtual = false,
                        PortType = PortType.Input,
                        SyntaxTreeName = x.Name,
                        DisplayName = attr.DisplayName ?? Prettify(x.Name),
                        Type = attr.GetInputType(x),
                        LinkTypes = attr.GetLinkTypes(x)
                    };
                }))
                .ToList();

            return ports;
        }
        
        List<FieldInfo> GetOutputFields(Type actorType)
        {
            var fields = actorType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => IsOutputType(x.FieldType))
                .ToList();

            if (actorType.BaseType != null &&
                actorType.BaseType.GetCustomAttribute<ActorAttribute>() != null)
            {
                fields.AddRange(GetOutputFields(actorType.BaseType));
            }

            return fields;
        }

        bool IsOutputType(Type fieldType)
        {
            return TryGetMatchingOutputType(fieldType, out _);
        }

        bool TryGetMatchingOutputType(Type type, out Type outputType)
        {
            if (type.IsGenericType)
                type = type.GetGenericTypeDefinition();
            
            outputType = GetOutputTypes().FirstOrDefault(x => x == type);
            return outputType != null || type.BaseType != null && TryGetMatchingOutputType(type.BaseType, out outputType);
        }

        List<Type> GetOutputTypes()
        {
            return m_OutputTypes ??= m_Components
                .Select(x => x.Type.GetCustomAttribute<ComponentAttribute>().OutputType)
                .ToList();
        }
        
        List<MethodInfo> GetInputMethods(Type actorType)
        {
            var inputAttrTypes = GetInputAttributeTypes();

            var methods = actorType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                .Where(x => x.GetCustomAttributes().Any(x => inputAttrTypes.Contains(x.GetType())))
                .ToList();

            if (actorType.BaseType != null &&
                actorType.BaseType.GetCustomAttribute<ActorAttribute>() != null)
            {
                methods.AddRange(GetInputMethods(actorType.BaseType));
            }

            return methods;
        }

        List<Type> GetInputAttributeTypes()
        {
            return m_InputAttrTypes ??= m_Components
                .Select(x => x.Type.GetCustomAttribute<ComponentAttribute>().InputAttributeType)
                .ToList();
        }

        Guid GetOutputPortId(FieldInfo field, out bool isGenerated)
        {
            var attr = (IOutputAttribute)field
                .GetCustomAttributes()
                .FirstOrDefault(x => x.GetType().GetInterfaces().Contains(typeof(IOutputAttribute)));

            if (attr == null || attr.Id == null)
            {
                isGenerated = true;
                return Guid.NewGuid();
            }

            isGenerated = false;
            return Guid.Parse(attr.Id);
        }

        IOutputAttribute GetOutputAttribute(FieldInfo field)
        {
            var attr = (IOutputAttribute)field
                .GetCustomAttributes()
                .FirstOrDefault(x => x.GetType().GetInterfaces().Contains(typeof(IOutputAttribute)));

            if (attr == null)
            {
                var attrType = m_Components
                    .First(x =>
                    {
                        var outputType = x.Type.GetCustomAttribute<ComponentAttribute>().OutputType;
                        return TryGetMatchingOutputType(field.FieldType, out var matchingOutputType) &&
                            outputType == matchingOutputType;
                    })
                    .Type.GetCustomAttribute<ComponentAttribute>()
                    .OutputAttributeType;

                attr = (IOutputAttribute)Activator.CreateInstance(attrType);
            }

            return attr;
        }

        internal static IInputAttribute GetInputAttribute(MethodInfo method)
        {
            var attr = (IInputAttribute)method
                .GetCustomAttributes()
                .First(x => x.GetType().GetInterfaces().Contains(typeof(IInputAttribute)));

            return attr;
        }

        internal static Guid GetInputPortId(MethodInfo method, out bool isGenerated)
        {
            var attr = (IOutputAttribute)method
                .GetCustomAttributes()
                .FirstOrDefault(x => x.GetType().GetInterfaces().Contains(typeof(IOutputAttribute)));

            if (attr == null || attr.Id == null)
            {
                isGenerated = true;
                return Guid.NewGuid();
            }

            isGenerated = false;
            return Guid.Parse(attr.Id);
        }

        internal static string Prettify(string name)
        {
            if (name.StartsWith("On") || name.StartsWith("m_"))
                name = name.Substring(2, name.Length - 2);
            if (name.EndsWith("Output"))
                name = name.Substring(0, name.Length - 6);

            return name;
        }
    }

    public interface IComponentDescriptorScanner
    {
        List<ComponentDescriptor> Scan(List<Assembly> searchAssemblies);
    }

    public class ComponentDescriptorScanner : IComponentDescriptorScanner
    {
        public List<ComponentDescriptor> Scan(List<Assembly> searchAssemblies)
        {
            var components = searchAssemblies
                .SelectMany(x => x.GetTypes())
                .Where(x => x.GetCustomAttribute<ComponentAttribute>() != null)
                .Select(x =>
                {
                    var attr = x.GetCustomAttribute<ComponentAttribute>();
                    var id = attr.Id ?? Guid.NewGuid().ToString();
                    return new ComponentDescriptor
                    {
                        Id = Guid.Parse(id),
                        Type = x
                    };
                })
                .ToList();

            return components;
        }
    }
}
