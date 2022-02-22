using System;
using System.Linq;

namespace Unity.Reflect.ActorFramework
{
    public class ComponentAttribute : Attribute
    {
        public string Id { get; }
        public Type InputAttributeType { get; }
        public Type OutputAttributeType { get; }
        public Type OutputType { get; }
        public Type ConnectionValidatorType { get; }

        public Multiplicity InputMultiplicity { get; }
        public Multiplicity OutputMultiplicity { get; }

        public string DisplayName { get; }

        public bool IsExcludedFromGraph { get; }

        public ComponentAttribute(string guid = null, Type inputAttributeType = null, Type outputAttributeType = null, Type outputType = null, Type connectionValidatorType = null,
            Multiplicity inputMultiplicity = default, Multiplicity outputMultiplicity = default, string displayName = null, bool isExcludedFromGraph = false)
        {
            Id = guid;
            DisplayName = displayName;
            InputAttributeType = inputAttributeType;
            OutputAttributeType = outputAttributeType;
            OutputType = outputType;
            ConnectionValidatorType = connectionValidatorType ?? typeof(DefaultValidator);
            if (!ConnectionValidatorType.GetInterfaces().Contains(typeof(IActorGraphConnectionValidator)))
                throw new ArgumentException($"'{ConnectionValidatorType}' must implement {nameof(IActorGraphConnectionValidator)}");

            InputMultiplicity = inputMultiplicity;
            OutputMultiplicity = outputMultiplicity;

            IsExcludedFromGraph = isExcludedFromGraph;

            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public ComponentAttribute(Type inputAttributeType, Type outputAttributeType, Type outputType)
             : this(null, inputAttributeType, outputAttributeType, outputType)
        {
        }
        
        class DefaultValidator : IActorGraphConnectionValidator
        {
            public bool WouldBeValid(ActorPort source, ActorPort destination, ActorSystemSetup asset) => true;
            public bool IsValid(ActorPort source, ActorPort destination, ActorSystemSetup asset) => true;
        }
    }

    public interface IActorGraphConnectionValidator
    {
        bool WouldBeValid(ActorPort source, ActorPort destination, ActorSystemSetup asset);
        bool IsValid(ActorPort source, ActorPort destination, ActorSystemSetup asset);
    }
}
