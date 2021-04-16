using System;

namespace Unity.Reflect.Actor
{
    public class ComponentAttribute : Attribute
    {
        public string Id { get; }
        public Type InputAttributeType { get; }
        public Type OutputAttributeType { get; }
        public Type OutputType { get; }

        public Multiplicity InputMultiplicity { get; }
        public Multiplicity OutputMultiplicity { get; }

        public string DisplayName { get; }

        public bool IsExcludedFromGraph { get; }

        public ComponentAttribute(string guid = null, Type inputAttributeType = null, Type outputAttributeType = null, Type outputType = null,
            Multiplicity inputMultiplicity = default, Multiplicity outputMultiplicity = default, string displayName = null, bool isExcludedFromGraph = false)
        {
            Id = guid;
            DisplayName = displayName;
            InputAttributeType = inputAttributeType;
            OutputAttributeType = outputAttributeType;
            OutputType = outputType;

            InputMultiplicity = inputMultiplicity;
            OutputMultiplicity = outputMultiplicity;

            IsExcludedFromGraph = isExcludedFromGraph;

            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public ComponentAttribute(Type inputAttributeType, Type outputAttributeType, Type outputType)
             : this(null, inputAttributeType, outputAttributeType, outputType, Multiplicity.Any, Multiplicity.Any, null)
        {
        }
    }
}
