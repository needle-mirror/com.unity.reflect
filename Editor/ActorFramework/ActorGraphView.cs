using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Unity.Reflect.ActorFramework
{
    public class ActorGraphView : GraphView
    {
        public ActorSystemSetup Asset { get; set; }

        public ActorGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            // FIXME: add a coordinator so that ContentDragger and SelectionDragger cannot be active at the same time.
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            Insert(0, new GridBackground());
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var validPorts = new List<Port>();

            var startActorPort = (ActorPort)startPort.userData;
            var actorConfig = Asset.ActorConfigs.FirstOrDefault(x => x.InputConfigs.Concat(x.OutputConfigs).Any(x => x.Id == startActorPort.ConfigId));
            if (actorConfig == null)
                return validPorts;
            var portConfig = actorConfig.InputConfigs.Concat(actorConfig.OutputConfigs).First(x => x.Id == startActorPort.ConfigId);

            var vPorts = ports.ToList();
            foreach (var port in vPorts)
            {
                if (startPort.direction == port.direction)
                    continue;

                var endPort = (ActorPort)port.userData;
                var endActorConfig = Asset.ActorConfigs.FirstOrDefault(x => x.InputConfigs.Concat(x.OutputConfigs).Any(x => x.Id == endPort.ConfigId));
                if (endActorConfig == null)
                    continue;

                var alreadyConnected = startActorPort.Links.Any(x =>
                    (x.InputId == startActorPort.Id || x.OutputId == startActorPort.Id) &&
                    x.InputId == endPort.Id || x.OutputId == endPort.Id);

                if (alreadyConnected)
                    continue;

                var endPortConfig = endActorConfig.InputConfigs.Concat(endActorConfig.OutputConfigs).First(x => x.Id == endPort.ConfigId);

                if (portConfig.MessageTypeNormalizedFullName != endPortConfig.MessageTypeNormalizedFullName ||
                    portConfig.ComponentConfigId != endPortConfig.ComponentConfigId)
                    continue;

                var componentConfig = Asset.ComponentConfigs.FirstOrDefault(x => x.Id == portConfig.ComponentConfigId);

                if (componentConfig == null ||
                    string.IsNullOrEmpty(componentConfig.ConnectionValidatorFullName))
                    continue;

                var validatorType = ReflectionUtils.GetClosedTypeFromAnyAssembly(componentConfig.ConnectionValidatorFullName);
                var validator = (IActorGraphConnectionValidator)Activator.CreateInstance(validatorType);

                var p1 = portConfig.PortType == PortType.Output ? startActorPort : endPort;
                var p2 = portConfig.PortType == PortType.Input ? startActorPort : endPort;
                if (!validator.WouldBeValid(p1, p2, Asset))
                    continue;

                validPorts.Add(port);
            }

            return validPorts;
        }
    }
}
