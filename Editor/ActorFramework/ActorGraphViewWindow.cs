using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Reflect.Actors;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Reflect.ActorFramework
{
    public class ActorGraphViewWindow : EditorWindow, ISearchWindowProvider
    {
        ActorGraphView m_GraphView;
        MiniMap m_MiniMap;
        ActorSystemSetup m_Asset;
        List<GraphElement> m_CurrentElements = new List<GraphElement>();

        [OnOpenAsset]
        public static bool OpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as ActorSystemSetup;
            if (asset == null)
                return false;

            OpenWindow(asset);
            return true;
        }

        [MenuItem("Actor System Setup", menuItem = "Window/Reflect/Open Actor System Graph View", priority = 1200)]
        public static void OpenActorSystemSetupWindow()
        {
            OpenWindow(null);
        }

        static void OpenWindow(ActorSystemSetup asset)
        {
            var window = GetWindow(asset);
            window.Reload();
        }

        static ActorGraphViewWindow GetWindow(ActorSystemSetup asset)
        {
            var openedWindows = Resources.FindObjectsOfTypeAll<ActorGraphViewWindow>().ToList();

            var window = openedWindows.FirstOrDefault(x => x.m_Asset == asset);
            if (window == null)
                window = openedWindows.FirstOrDefault(x => x.m_Asset == null);
            if (window == null)
                window = CreateWindow<ActorGraphViewWindow>("Actor: empty", typeof(SceneView));

            window.Focus();
            window.AssignAsset(asset);
            
            if (asset != null)
                window.titleContent = new GUIContent($"Actor: {asset.name}");

            return window;
        }

        void Awake()
        {
            EditorApplication.quitting += SaveAsset;
        }

        void OnEnable()
        {
            m_GraphView = new ActorGraphView();
            AssignAsset(m_Asset);

            m_GraphView.name = "Actor System Setup Graph View";
            m_GraphView.StretchToParentSize();
            m_GraphView.graphViewChanged = GraphViewChanged;
            m_GraphView.nodeCreationRequest += OnRequestNodeCreation;

            rootVisualElement.Add(m_GraphView);

            var layout = new VisualElement();
            layout.style.maxWidth = 200;

            rootVisualElement.Add(layout);

            var field = new TextField();
            field.value = "ActorSystemSetupGraphView";
            layout.Add(field);

            var createAssetBtn = new Button(() => CreateSetupAsset(field.value)) { text = "Create Asset" };
            createAssetBtn.style.width = 200;
            layout.Add(createAssetBtn);

            var saveBtn = new Button(SaveAsset) { text = "Save" };
            saveBtn.style.width = 200;
            layout.Add(saveBtn);

            var beginMigrationBtn = new Button(BeginMigration) { text = "Begin Migration" };
            beginMigrationBtn.style.width = 200;
            layout.Add(beginMigrationBtn);

            var endMigrationBtn = new Button(EndMigration) { text = "Complete Migration" };
            endMigrationBtn.style.width = 200;
            layout.Add(endMigrationBtn);
            
            Reload();
        }

        void OnDestroy()
        {
            SaveAsset();
            EditorApplication.quitting -= SaveAsset;
        }

        void OnRequestNodeCreation(NodeCreationContext context)
        {
            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), this);
        }

        void BeginMigration()
        {
            ActorSystemSetupAnalyzer.BeginInteractiveMigration(m_Asset);
            Reload();
        }

        void EndMigration()
        {
            if (m_Asset != null)
                ActorSystemSetupAnalyzer.CompleteInteractiveMigration(m_Asset);

            SaveAsset();
            Reload();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>();

            tree.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));

            var nodes = m_Asset.ActorConfigs
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.DisplayName)
                .ToList();

            string lastNodeGroupName = null;
            foreach (var node in nodes)
            {
                if (node.IsRemoved)
                    continue;

                if (node.GroupName != lastNodeGroupName)
                {
                    lastNodeGroupName = node.GroupName;
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(node.GroupName), 1));
                }
                
                tree.Add(new SearchTreeEntry(new GUIContent(node.DisplayName)) { level = 2, userData = node });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry is SearchTreeGroupEntry)
                return false;

            var actorConfig = (ActorConfig)entry.userData;
            var actorSetup = ActorSystemSetupAnalyzer.CreateActorSetup(actorConfig);
            m_Asset.ActorSetups.Add(actorSetup);

            foreach (var inputConfig in actorConfig.InputConfigs)
            {
                var isValid = MultiplicityValidator.IsValid(m_Asset.ComponentConfigs.First(x => x.Id == inputConfig.ComponentConfigId).InputMultiplicity, 0);
                actorSetup.Inputs.Add(new ActorPort(Guid.NewGuid().ToString(), inputConfig.Id, new List<ActorLink>(), isValid, false));
            }

            foreach (var outputConfig in actorConfig.OutputConfigs)
            {
                var isValid = MultiplicityValidator.IsValid(m_Asset.ComponentConfigs.First(x => x.Id == outputConfig.ComponentConfigId).OutputMultiplicity, 0);
                actorSetup.Outputs.Add(new ActorPort(Guid.NewGuid().ToString(), outputConfig.Id, new List<ActorLink>(), isValid, false));
            }

            var node = CreateNode(actorSetup);

            var pointInWindow = context.screenMousePosition - position.position;
            var pointInGraph = node.parent.WorldToLocal(pointInWindow);

            node.SetPosition(new Rect(pointInGraph, Vector2.zero));
            actorSetup.Position = pointInGraph;

            return true;
        }
		
        GraphViewChange GraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (element is Node)
                    {
                        var setup = (ActorSetup)element.userData;
                        m_Asset.ActorSetups.Remove(setup);
                    }
                    else if (element is CustomEdge edge)
                    {
                        if (edge.userData is ActorLink link)
                        {
                            var inputSetup = (ActorSetup)edge.input.node.userData;
                            var outputSetup = (ActorSetup)edge.output.node.userData;
                            
                            var input = inputSetup.Inputs.FirstOrDefault(x => x.Id == link.InputId);
                            input?.Links.Remove(link);
                            var output = outputSetup.Outputs.FirstOrDefault(x => x.Id == link.OutputId);
                            output?.Links.Remove(link);

                            if (input != null)
                            {
                                var inputActorConfig = m_Asset.ActorConfigs.First(x => x.Id == inputSetup.ConfigId);
                                var inputConfig = inputActorConfig.InputConfigs.First(x => x.Id == input.ConfigId);
                                var inputComponentConfig = m_Asset.ComponentConfigs.First(x => x.Id == inputConfig.ComponentConfigId);
                                var inputMultiplicity = inputComponentConfig.InputMultiplicity;
                                input.IsValid = MultiplicityValidator.IsValid(inputMultiplicity, input.Links.Count);
                                SetPortColor(edge.input, input, inputConfig);
                            }

                            if (output != null)
                            {
                                var outputActorConfig = m_Asset.ActorConfigs.First(x => x.Id == outputSetup.ConfigId);
                                var outputConfig = outputActorConfig.OutputConfigs.First(x => x.Id == output.ConfigId);
                                var outputComponentConfig = m_Asset.ComponentConfigs.First(x => x.Id == outputConfig.ComponentConfigId);
                                var outputMultiplicity = outputConfig.IsOptional ? Multiplicity.Any : outputComponentConfig.OutputMultiplicity;
                                output.IsValid = MultiplicityValidator.IsValid(outputMultiplicity, output.Links.Count);
                                SetPortColor(edge.output, output, outputConfig);
                            }
                        }
                    }
                    RemoveElement(element);
                }
            }

            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var e in graphViewChange.edgesToCreate)
                {
                    var edge = (CustomEdge)e;
                    var input = (ActorPort)edge.input.userData;
                    var output = (ActorPort)edge.output.userData;
                    
                    var link = new ActorLink(output.Id, input.Id, output.IsRemoved || input.IsRemoved);
                    input.Links.Add(link);
                    output.Links.Add(link);

                    edge.userData = link;
                    if (link.IsRemoved)
                        edge.EdgeState = EdgeState.Deleted;

                    if (!link.IsRemoved)
                    {
                        if (!input.IsRemoved)
                        {
                            var inputSetup = (ActorSetup)edge.input.node.userData;
                            var inputActorConfig = m_Asset.ActorConfigs.First(x => x.Id == inputSetup.ConfigId);
                            var inputConfig = inputActorConfig.InputConfigs.First(x => x.Id == input.ConfigId);
                            var inputComponentConfig = m_Asset.ComponentConfigs.First(x => x.Id == inputConfig.ComponentConfigId);
                            var inputMultiplicity = inputComponentConfig.InputMultiplicity;
                            input.IsValid = MultiplicityValidator.IsValid(inputMultiplicity, input.Links.Count);
                            SetPortColor(edge.input, input, inputConfig);
                        }

                        if (!output.IsRemoved)
                        {
                            var outputSetup = (ActorSetup)edge.output.node.userData;
                            var outputActorConfig = m_Asset.ActorConfigs.First(x => x.Id == outputSetup.ConfigId);
                            var outputConfig = outputActorConfig.OutputConfigs.First(x => x.Id == output.ConfigId);
                            var outputComponentConfig = m_Asset.ComponentConfigs.First(x => x.Id == outputConfig.ComponentConfigId);
                            var outputMultiplicity = outputConfig.IsOptional ? Multiplicity.Any : outputComponentConfig.OutputMultiplicity;
                            output.IsValid = MultiplicityValidator.IsValid(outputMultiplicity, output.Links.Count);
                            SetPortColor(edge.output, output, outputConfig);
                        }
                    }

                    AddElement(edge, true);
                }
            }

            if (graphViewChange.movedElements != null)
            {
                foreach (var element in graphViewChange.movedElements)
                {
                    if (element is Node)
                        ((ActorSetup)element.userData).Position = element.GetPosition().position;
                }
            }

            return graphViewChange;
        }

        Node CreateNode(ActorSetup actorSetup)
        {
            var node = new Node();
            node.title = actorSetup.DisplayName;
            node.capabilities |= Capabilities.Movable;
            node.name = actorSetup.Id;
            node.userData = actorSetup;
            node.SetPosition(new Rect(actorSetup.Position, Vector2.zero));

            var actorConfig = m_Asset.ActorConfigs.FirstOrDefault(x => x.Id == actorSetup.ConfigId);
            
            CreatePorts(node.inputContainer, actorSetup.Inputs, actorConfig?.InputConfigs);
            CreatePorts(node.outputContainer, actorSetup.Outputs, actorConfig?.OutputConfigs);

            if (actorSetup.IsRemoved)
                node.mainContainer.style.backgroundColor = Color.red;
            else if (actorSetup.HasSettingsTypeChanged)
                node.mainContainer.style.backgroundColor = Color.cyan;

            AddElement(node);

            return node;
        }

        void CreatePorts(VisualElement container, List<ActorPort> actorPorts, List<ActorPortConfig> actorPortConfigs)
        {
            if (actorPortConfigs == null)
                return;
            
            // Bind
            var actorPortToConfig = new List<Tuple<ActorPort, ActorPortConfig>>();

            foreach (var actorPort in actorPorts)
            {
                var portConfig = actorPortConfigs.FirstOrDefault(x => x.Id == actorPort.ConfigId);

                if (portConfig == null)
                    continue;
                
                // Prevent DelegateJob output from being drawn since their connected automatically by the ActorSystemSetupAnalyser
                if (portConfig.PortType == PortType.Output && portConfig.MessageTypeNormalizedFullName == typeof(DelegateJob).FullName)
                    continue;

                actorPortToConfig.Add(new Tuple<ActorPort, ActorPortConfig>(actorPort, portConfig));
            }
            
            // Sort
            var sortedPorts = actorPortToConfig.OrderByDescending(item => item.Item2.TypeNormalizedFullName)
                .ThenByDescending(item => item.Item2.DisplayName);

            foreach (var (actorPort, portConfig) in sortedPorts)
            {
                var direction = portConfig.PortType == PortType.Input ? Direction.Input : Direction.Output;

                var componentConfig = m_Asset.ComponentConfigs.FirstOrDefault(x => x.Id == portConfig.ComponentConfigId);

                if (componentConfig == null)
                    continue;

                var multiplicity = portConfig.PortType == PortType.Input ? componentConfig.InputMultiplicity : componentConfig.OutputMultiplicity;

                var port = Port.Create<CustomEdge>(Orientation.Horizontal, direction, ToCapacity(multiplicity), typeof(ActorSetup));
                port.portName = PortDecoration.GetPortName(portConfig.DisplayName, portConfig);
                port.name = actorPort.Id;
                port.userData = actorPort;
                port.SetEnabled(multiplicity != Multiplicity.Zero);
                SetPortColor(port, actorPort, portConfig);
                container.Add(port);
            }
        }

        static Port.Capacity ToCapacity(Multiplicity multiplicity)
        {
            if (multiplicity == Multiplicity.ExactlyOne || multiplicity == Multiplicity.ZeroOrOne)
                return Port.Capacity.Single;
            return Port.Capacity.Multi;
        }

        void AddElement(GraphElement element, bool isAlreadyInGraphView = false)
        {
            m_CurrentElements.Add(element);
            if (!isAlreadyInGraphView)
                m_GraphView.AddElement(element);
        }

        void RemoveElement(GraphElement element)
        {
            m_CurrentElements.Remove(element);
            m_GraphView.RemoveElement(element);
        }

        void CreateDataEdge(List<Node> nodes, ActorLink link)
        {
            var outputNode = nodes.FirstOrDefault(x => ((ActorSetup)x.userData).Outputs.Any(x => x.Id == link.OutputId));
            var inputNode = nodes.FirstOrDefault(x => ((ActorSetup)x.userData).Inputs.Any(x => x.Id == link.InputId));

            if (outputNode == null || inputNode == null)
                return;
            
            var outputPort = outputNode.Q<Port>(link.OutputId);
            var inputPort = inputNode.Q<Port>(link.InputId);

            if (outputPort == null || inputPort == null)
                return;

            var edge = outputPort.ConnectTo<CustomEdge>(inputPort);
            edge.userData = link;

            if (link.IsRemoved)
                edge.EdgeState = EdgeState.Deleted;

            AddElement(edge);
        }

        void Reload()
        {
            if (m_GraphView == null)
                return;

            if (m_Asset == null)
                return;
            
            titleContent = new GUIContent($"Actor: {m_Asset.name}");

            foreach(var element in m_CurrentElements)
                m_GraphView.RemoveElement(element);
            m_CurrentElements.Clear();

            var nodes = new List<Node>();
            foreach (var setup in m_Asset.ActorSetups)
                nodes.Add(CreateNode(setup));

            foreach (var actorSetup in m_Asset.ActorSetups)
                foreach (var output in actorSetup.Outputs)
                    foreach (var link in output.Links)
                        CreateDataEdge(nodes, link);

            UpdatePortStates(nodes);

            if (m_MiniMap == null)
            {
                m_MiniMap = new MiniMap();
                m_MiniMap.SetPosition(new Rect(0, 372, 200, 176));
                m_GraphView.Add(m_MiniMap);
            }
        }

        void UpdatePortStates(List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                var setup = (ActorSetup)node.userData;
                var actorConfig = m_Asset.ActorConfigs.First(x => x.Id == setup.ConfigId);

                foreach (var port in setup.Inputs)
                {
                    var portConfig = actorConfig.InputConfigs.First(x => x.Id == port.ConfigId); 
                    SetPortColor(node.inputContainer.Q<Port>(port.Id), port, portConfig);
                }

                foreach (var port in setup.Outputs)
                {
                    var portConfig = actorConfig.OutputConfigs.First(x => x.Id == port.ConfigId); 
                    SetPortColor(node.outputContainer.Q<Port>(port.Id), port, portConfig);
                }
            }
        }

        static void SetPortColor(Port port, ActorPort actorPort, ActorPortConfig actorPortConfig)
        {
            if (port == null || actorPort == null || actorPortConfig == null)
                return;
            
            var color = PortDecoration.GetPortColor(actorPortConfig);
            
            port.portColor = actorPort.IsRemoved ? Color.red : actorPort.IsValid ? color : new Color(1.0f, 0.65f, 0.0f);
            port.highlight = true;
        }

        void SaveAsset()
        {
            if (m_Asset != null)
            {
                EditorUtility.SetDirty(m_Asset);
                AssetDatabase.SaveAssets();

                Reload();
            }
        }

        void CreateSetupAsset(string fileName)
        {
            var asset = CreateInstance<ActorSystemSetup>();
            AssignAsset(asset);
            ActorSystemSetupAnalyzer.InitializeActorSystemSetup(asset);
            
            var getActiveFolderPath = typeof(ProjectWindowUtil)
                .GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            var obj = getActiveFolderPath.Invoke(null, new object[0]);
            var pathToCurrentFolder = obj.ToString();

            ProjectWindowUtil.CreateAsset(m_Asset, AssetDatabase.GenerateUniqueAssetPath(pathToCurrentFolder + $"/{fileName}.asset"));
        }

        void AssignAsset(ActorSystemSetup asset)
        {
            m_Asset = asset;
            if (m_GraphView != null)
                m_GraphView.Asset = asset;

            if (m_Asset != null)
                ActorSystemSetupAnalyzer.InitializeAnalyzer(m_Asset);
        }
    }

    public class CustomEdge : Edge
    {
        EdgeState m_EdgeState;
        public EdgeState EdgeState
        {
            get => m_EdgeState;
            set
            {
                m_EdgeState = value;
                UpdateEdgeControl();
            }
        }

        public override bool UpdateEdgeControl()
        {
            var res = base.UpdateEdgeControl();
            if (res)
                OverrideDefaultColor();

            return res;
        }

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);
            OverrideDefaultColor();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OverrideDefaultColor();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            OverrideDefaultColor();
        }

        void OverrideDefaultColor()
        {
            if (m_EdgeState == EdgeState.Deleted)
            {
                edgeControl.inputColor = Color.red;
                edgeControl.outputColor = Color.red;
            }
        }
    }

    public enum EdgeState
    {
        Normal,
        Deleted
    }
    
    static class PortDecoration
    {
        public static string GetPortName(string portName, ActorPortConfig portConfig)
        {
            if (IsRpcOutput(portConfig))
                return portName + " [RPC]";
            
            if (IsRpcInput(portConfig))
                return "[RPC] " + portName;
        
            if (IsPipeOutput(portConfig))
                return portName + " [PIPE]";
            
            if (IsPipeInput(portConfig))
                return "[PIPE] " + portName;

            return portName;
        }

        public static Color GetPortColor(ActorPortConfig portConfig)
        {
            if (IsRpcInput(portConfig) || IsRpcOutput(portConfig))
                return Color.HSVToRGB(0.0f, 0.5f, 1.0f);
            
            if (IsPipeInput(portConfig) || IsPipeOutput(portConfig))
                return Color.HSVToRGB(0.3f, 0.5f, 1.0f);
            
            return Color.HSVToRGB(0.5f, 0.5f, 1.0f);
        }
        
        static bool IsRpcInput(ActorPortConfig actorPortConfig)
        {
            return actorPortConfig.PortType == PortType.Input && actorPortConfig.TypeNormalizedFullName.Contains(".RpcContext");
        }

        static bool IsRpcOutput(ActorPortConfig actorPortConfig)
        {
            return actorPortConfig.PortType == PortType.Output && actorPortConfig.TypeNormalizedFullName.Contains(".RpcOutput");
        }

        static bool IsPipeInput(ActorPortConfig actorPortConfig)
        {
            return actorPortConfig.PortType == PortType.Input && actorPortConfig.TypeNormalizedFullName.Contains(".PipeContext");
        }

        static bool IsPipeOutput(ActorPortConfig actorPortConfig)
        {
            return actorPortConfig.PortType == PortType.Output && (actorPortConfig.TypeNormalizedFullName.Contains(".PipeOutput")
                || actorPortConfig.TypeNormalizedFullName.Contains(".PipeContext"));
        }
    }
}
