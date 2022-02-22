using System;
using System.Collections.Generic;

namespace Unity.Reflect.ActorFramework
{
    public class ActorSystemDescriptor
    {
        public List<ActorDescriptor> ActorDescriptors = new List<ActorDescriptor>();
        public List<ComponentDescriptor> ComponentDescriptors = new List<ComponentDescriptor>();
    }
    
    public class ActorDescriptor
    {
        /// <summary>
        ///     The unique id of the actor.
        /// </summary>
        public Guid Id;

        /// <summary>
        ///     The class type of this actor.
        /// </summary>
        public Type Type;

        /// <summary>
        ///     The port descriptors associated with this actor.
        /// </summary>
        public List<PortDescriptor> Ports;
    }
    
    public class PortDescriptor
    {
        /// <summary>
        ///     The unique id of the port
        /// </summary>
        public Guid Id;

        /// <summary>
        ///     Indicates if the id is generated or is coming from a stable source.
        /// </summary>
        public bool IsGeneratedId;

        /// <summary>
        ///     Indicates if the port has a mapping in the code (field, method, ...) or not (virtual).
        /// </summary>
        public bool IsVirtual;

        /// <summary>
        ///     The direction of the port.
        /// </summary>
        public PortType PortType;

        /// <summary>
        ///     The name of the mapped field, method. Contains a generated name if virtual.
        /// </summary>
        public string SyntaxTreeName;

        /// <summary>
        ///     A user friendly name
        /// </summary>
        public string DisplayName;

        /// <summary>
        ///     The full type of the port
        /// </summary>
        public Type Type;

        /// <summary>
        ///     All the types forming the constraints for linking this port to another one.
        /// </summary>
        public Type[] LinkTypes;
    }

    public class ComponentDescriptor
    {
        public Guid Id;
        public Type Type;
    }
}
