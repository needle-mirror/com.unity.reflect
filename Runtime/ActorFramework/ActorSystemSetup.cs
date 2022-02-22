using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Reflect.ActorFramework
{
    public interface IPortAttribute
    {
        string Id { get; }
        string DisplayName { get; }
    }

    public interface IInputAttribute : IPortAttribute
    {
        Type GetInputType(MethodInfo methodInfo);
        Type[] GetLinkTypes(MethodInfo methodInfo);
    }

    public interface IOutputAttribute : IPortAttribute
    {
        Type[] GetLinkTypes(FieldInfo fieldInfo);
    }

    public class TransientAttribute : Attribute
    {
        public string FieldName;

        public TransientAttribute(string fieldName)
        {
            FieldName = fieldName;
        }
    }
    
    public enum Multiplicity
    {
        Any,
        ZeroOrOne,
        ExactlyOne,
        OneOrMore,
        Zero
    }

    public static class MultiplicityValidator
    {
        public static bool IsValid(Multiplicity multiplicity, int nbConnections)
        {
            switch (multiplicity)
            {
                case Multiplicity.ZeroOrOne when nbConnections < 2:
                case Multiplicity.ExactlyOne when nbConnections == 1:
                case Multiplicity.OneOrMore when nbConnections > 0:
                case Multiplicity.Any:
                case Multiplicity.Zero when nbConnections == 0:
                    return true;
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public class ComponentConfig : IConfigIdentifier
    {
        [SerializeField]
        string m_Id;
        [SerializeField]
        bool m_IsGeneratedId;
        [SerializeField]
        string m_TypeNormalizedFullName;
        [SerializeField]
        string m_ConnectionValidatorFullName;

        public string Id { get => m_Id; set => m_Id = value; }
        public bool IsGeneratedId { get => m_IsGeneratedId; set => m_IsGeneratedId = value; }
        public string TypeNormalizedFullName { get => m_TypeNormalizedFullName; set => m_TypeNormalizedFullName = value; }
        public string ConnectionValidatorFullName { get => m_ConnectionValidatorFullName; set => m_ConnectionValidatorFullName = value; }

        public Multiplicity InputMultiplicity;
        public Multiplicity OutputMultiplicity;

        public string DisplayName;
        public bool IsRemoved;

        public ComponentConfig(string id, bool isGeneratedId, string typeNormalizedFullName, string connectionValidatorFullName,
            Multiplicity inputMultiplicity, Multiplicity outputMultiplicity, string displayName, bool isRemoved)
        {
            Id = id;
            IsGeneratedId = isGeneratedId;
            TypeNormalizedFullName = typeNormalizedFullName;
            ConnectionValidatorFullName = connectionValidatorFullName;

            InputMultiplicity = inputMultiplicity;
            OutputMultiplicity = outputMultiplicity;

            DisplayName = displayName;
            IsRemoved = isRemoved;
        }
    }

    [Serializable]
    public class ActorConfig : IConfigIdentifier
    {
        [SerializeField]
        string m_Id;
        [SerializeField]
        bool m_IsGeneratedId;
        [SerializeField]
        string m_TypeNormalizedFullName;

        public string Id { get => m_Id; set => m_Id = value; }
        public bool IsGeneratedId { get => m_IsGeneratedId; set => m_IsGeneratedId = value; }
        public string TypeNormalizedFullName { get => m_TypeNormalizedFullName; set => m_TypeNormalizedFullName = value; }
        
        public List<ActorPortConfig> InputConfigs;
        public List<ActorPortConfig> OutputConfigs;

        public bool IsBoundToMainThread;
        public string GroupName;
        public string DisplayName;
        public bool IsRemoved;

        public ActorConfig(string id, bool isGeneratedId, string typeNormalizedFullName,
            List<ActorPortConfig> inputConfigs, List<ActorPortConfig> outputConfigs, bool isBoundToMainThread, string groupName, string displayName, bool isRemoved)
        {
            Id = id;
            IsGeneratedId = isGeneratedId;
            TypeNormalizedFullName = typeNormalizedFullName;
            InputConfigs = inputConfigs;
            OutputConfigs = outputConfigs;

            IsBoundToMainThread = isBoundToMainThread;
            GroupName = groupName;
            DisplayName = displayName;
            IsRemoved = isRemoved;
        }
    }

    [Serializable]
    public enum PortType
    {
        Input,
        Output
    }

    [Serializable]
    public class ActorPortConfig : IConfigIdentifier
    {
        [SerializeField]
        string m_Id;
        [SerializeField]
        bool m_IsGeneratedId;
        [SerializeField]
        string m_TypeNormalizedFullName;
        [SerializeField]
        string m_MessageTypeNormalizedFullName;
        [SerializeField]
        string m_MemberName;
        [SerializeField]
        bool m_IsOptional;

        public string Id { get => m_Id; set => m_Id = value; }
        public bool IsGeneratedId { get => m_IsGeneratedId; set => m_IsGeneratedId = value; }
        public string TypeNormalizedFullName { get => m_TypeNormalizedFullName; set => m_TypeNormalizedFullName = value; }
        public string MessageTypeNormalizedFullName { get => m_MessageTypeNormalizedFullName; set => m_MessageTypeNormalizedFullName = value; }
        public string MemberName { get => m_MemberName; set => m_MemberName = value; }
        public bool IsOptional { get => m_IsOptional; set => m_IsOptional = value; }

        public PortType PortType;

        public string ComponentConfigId;
        public string DisplayName;
        public bool IsRemoved;

        public ActorPortConfig(string id, bool isGeneratedId, string componentConfigId, string typeNormalizedFullName,
            string messageTypeNormalizedFullName, string memberName, PortType portType, string displayName,
            bool isRemoved, bool isOptional)
        {
            Id = id;
            IsGeneratedId = isGeneratedId;
            ComponentConfigId = componentConfigId;
            TypeNormalizedFullName = typeNormalizedFullName;
            MessageTypeNormalizedFullName = messageTypeNormalizedFullName;
            MemberName = memberName;
            PortType = portType;
            DisplayName = displayName;
            IsRemoved = isRemoved;
            IsOptional = isOptional;
        }
    }

    public interface IConfigIdentifier
    {
        public string Id { get; set; }
        public bool IsGeneratedId { get; set; }
        public string TypeNormalizedFullName { get; set; }
    }

    [Serializable]
    public class ActorSetup
    {
        public string Id;
        public string ConfigId;

        [SerializeReference]
        public ActorSettings Settings;
        public List<ActorPort> Inputs;
        public List<ActorPort> Outputs;

        public string DisplayName;
        public Vector2 Position;
        public bool IsRemoved;
        public bool HasSettingsTypeChanged;

        public ActorSetup() { }
        public ActorSetup(string id, string configId, List<ActorPort> inputs, List<ActorPort> outputs, string displayName, Vector2 position, bool hasSettingsTypeChanged, bool isRemoved)
        {
            Id = id;
            ConfigId = configId;
            Inputs = inputs;
            Outputs = outputs;

            DisplayName = displayName;
            Position = position;
            HasSettingsTypeChanged = hasSettingsTypeChanged;
            IsRemoved = isRemoved;
        }
    }

    [Serializable]
    public class ActorSettings
    {
        [HideInInspector]
        public string Id;

        public ActorSettings(string id)
        {
            Id = id;
        }
    }

    [Serializable]
    public class ActorPort
    {
        public string Id;
        public string ConfigId;

        [SerializeReference]
        public List<ActorLink> Links;

        public bool IsValid;
        public bool IsRemoved;

        public ActorPort(string id, string configId, List<ActorLink> senders, bool isValid, bool isRemoved)
        {
            Id = id;
            ConfigId = configId;
            Links = senders;
            IsValid = isValid;
            IsRemoved = isRemoved;
        }
    }

    public class RuntimeOutput
    {
        public Type MessageType;
        public List<ActorHandle> Receivers;

        public RuntimeOutput(Type messageType, List<ActorHandle> receivers)
        {
            MessageType = messageType;
            Receivers = receivers;
        }
    }

    [Serializable]
    public class ActorLink
    {
        public string OutputId;
        public string InputId;
        
        public bool IsRemoved;

        public ActorLink(string outputId, string inputId, bool isRemoved)
        {
            OutputId = outputId;
            InputId = inputId;
            IsRemoved = isRemoved;
        }
    }
    
    public class ActorSystemSetup : ScriptableObject
    {
        public List<string> ExcludedAssemblies;

        [SerializeReference]
        public List<ComponentConfig> ComponentConfigs;

        [SerializeReference]
        public List<ActorConfig> ActorConfigs;

        [SerializeReference]
        public List<ActorSetup> ActorSetups;
    }
}
