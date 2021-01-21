using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Model;
using System.Reflection;
using Unity.Reflect;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IParam
    {
        string id { get; }
    }
    
    [Serializable]
    public abstract class Param<T> : IParam
    {
        [SerializeReference]
        public T value;
        
        public string id => m_Id;

        [SerializeField]
        string m_Id = Guid.NewGuid().ToString();

        internal void SetId(string newId)
        {
            m_Id = newId;
        }
    }

    interface IInput
    {
        string id { get; }
        void OnStreamBegin();
        void OnStreamEvent(object stream, StreamEvent eventType);
        void OnStreamEnd();
    }

    [Serializable]
    public abstract class Input<T> : IInput
    {
        public Action<T, StreamEvent> streamEvent;

        public Action streamBegin;
        
        public Action streamEnd;
        
        [SerializeField]
        string m_Id = Guid.NewGuid().ToString();

        public string id => m_Id;

        public void OnStreamBegin()
        {
            streamBegin?.Invoke();
        }

        public void OnStreamEvent(object stream, StreamEvent eventType)
        {
            streamEvent?.Invoke(stream is T s ? s : default, eventType);
        }

        public void OnStreamEnd()
        {
            streamEnd?.Invoke();
        }
    }

    public enum StreamEvent
    {
        Added,
        Changed,
        Removed
    }

    public interface IOutput
    {
        string id { get; }
    }

    public interface IOutput<T> : IOutput
    {
        void SendBegin();

        void SendStreamAdded(T stream);

        void SendStreamChanged(T stream);

        void SendStreamRemoved(T stream);

        void SendStreamEvent(T stream, StreamEvent eventType);

        void SendEnd();
    }
    
    [Serializable]
    public abstract class Output<T> : IOutput<T>
    {
        [NonSerialized]
        List<IInput> m_Inputs;

        [SerializeField]
        string m_Id = Guid.NewGuid().ToString();

        public string id => m_Id;
        
        internal List<IInput> inputs => m_Inputs;

        protected Output()
        {
            m_Inputs = new List<IInput>();
        }
        
        public void ConnectInput(Input<T> input)
        {
            m_Inputs.Add(input);
        }

        public void SendBegin()
        {
            m_Inputs.ForEach(i => i.OnStreamBegin());
        }

        public void SendStreamEvent(T stream, StreamEvent eventType)
        {
            m_Inputs.ForEach(i => i.OnStreamEvent(stream, eventType));
        }

        public void SendStreamAdded(T stream)
        {
            SendStreamEvent(stream, StreamEvent.Added);
        }

        public void SendStreamChanged(T stream)
        {
            SendStreamEvent(stream, StreamEvent.Changed);
        }

        public void SendStreamRemoved(T stream)
        {
            SendStreamEvent(stream, StreamEvent.Removed);
        }

        public void SendEnd()
        {
            m_Inputs.ForEach(i => i.OnStreamEnd());
        }
    }

    [Serializable]
    public abstract class DataOutput<T> : Output<SyncedData<T>>
    {
    }
    
    [Serializable]
    public abstract class DataInput<T> : Input<SyncedData<T>>
    {
    }

    [Serializable]
    public class SyncMaterialInput : DataInput<SyncMaterial> { }

    [Serializable]
    public class SyncMeshInput : DataInput<SyncMesh> { }

    [Serializable]
    public class MeshOutput : DataOutput<Mesh> { }
    
    
    [Serializable]
    public class MeshInput : DataInput<Mesh> { }
    
    [Serializable]
    public class MaterialOutput : DataOutput<Material> { }

    [Serializable]
    public class MaterialInput : DataInput<Material> { }
    
    [Serializable]
    public class SyncMeshOutput : DataOutput<SyncMesh> { }
    
    [Serializable]
    public class SyncMaterialOutput : DataOutput<SyncMaterial> { }
    
    [Serializable]
    public class SyncTextureOutput : DataOutput<SyncTexture> { }
    
    [Serializable]
    public class SyncTextureInput : DataInput<SyncTexture> { }
    
    [Serializable]
    public class Texture2DOutput : DataOutput<Texture2D> { }
    
    [Serializable]
    public class Texture2DInput : DataInput<Texture2D> { }

    [Serializable]
    public class StreamAssetInput : DataInput<StreamAsset> { }

    [Serializable]
    public class StreamAssetOutput : DataOutput<StreamAsset> { }
    
    [Serializable]
    public class StreamInstanceInput : DataInput<StreamInstance> { }
    
    [Serializable]
    public class StreamInstanceOutput : DataOutput<StreamInstance> { }
    
    [Serializable]
    public class StreamInstanceDataInput : DataInput<StreamInstanceData> { }
    
    [Serializable]
    public class StreamInstanceDataOutput : DataOutput<StreamInstanceData> { }
    
    [Serializable]
    public class GameObjectOutput : DataOutput<GameObject> { }
    
    [Serializable]
    public class GameObjectInput : DataInput<GameObject> { }
    
    [Serializable]
    public class HashCacheParam : Param<IHashProvider> { }
    
    public class PipelineAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeReference]
        List<IReflectNode> m_Nodes = new List<IReflectNode>();
        
        [Serializable]
        class Connection
        {
            public string output;
            public string input;
        }

        [SerializeField]
        List<Connection> m_Connections = new List<Connection>();

        [SerializeField]
        List<string> m_ParamKeys = new List<string>();

        [SerializeReference]
        List<IReflectNode> m_ParamValues = new List<IReflectNode>();

        public IList<IReflectNode> nodes => m_Nodes;

        public T CreateNode<T>() where T : class, IReflectNode
        {
            var node = Activator.CreateInstance<T>();
            m_Nodes.Add(node);

            return node;
        }

        public void CreateConnection<T>(Output<T> output, Input<T> input)
        {
            output.ConnectInput(input);
            
            var connection = new Connection
            {
                input = input.id,
                output = output.id,
            };
            
            m_Connections.Add(connection);
        }

        public void SetParam<T>(Param<T> param, T value)
        {
            param.value = value;
            
            if (!(value is IReflectNode node))
            {
                Debug.LogError("Node Params values must derive from ReflectNode.");
                return;
            }
            
            m_ParamKeys.Add(param.id);
            m_ParamValues.Add(node);
        }

        public void OnBeforeSerialize()
        {
            // Nothing
        }

        public void OnAfterDeserialize()
        {
            if (m_Connections == null || m_Connections.Count == 0)
                return;
            
            var inputsLookup = new Dictionary<string, IInput>();
            var outputsLookup = new Dictionary<string, IOutput>();
            var paramsLookup = new Dictionary<string, IParam>();

            foreach (var node in nodes)
            {
                GetPorts(node, out var inputs, out var outputs, out var parameters);

                foreach (var i in inputs)
                {
                    inputsLookup[i.id] = i;
                }
                
                foreach (var o in outputs)
                {
                    outputsLookup[o.id] = o;
                }

                foreach (var p in parameters)
                {
                    paramsLookup[p.id] = p;
                }
            }

            var error = false;

            foreach (var connection in m_Connections)
            {
                if (!inputsLookup.TryGetValue(connection.input, out var input))
                {
                    error = true;
                    break;
                }
                    
                if (!outputsLookup.TryGetValue(connection.output, out var output))
                {
                    error = true;
                    break;
                }

                var connectMethod = output.GetType().GetMethod("ConnectInput");

                if (connectMethod == null)
                {
                    error = true;
                    break;
                }
                
                connectMethod.Invoke(output, new object[] { input });
            }

            if (error)
            {
                Debug.LogError($"Unable de deserialize PipelineAsset connections in asset: {name}");
                return;
            }

            for (var i = 0; i < m_ParamKeys.Count; ++i)
            {
                var key = m_ParamKeys[i];
                
                if (!paramsLookup.TryGetValue(key, out var param))
                {
                    error = true;
                    break;
                }
                
                var valueField = param.GetType().GetField("value");

                if (valueField == null)
                {
                    error = true;
                    break;
                }
                
                valueField.SetValue(param, m_ParamValues[i]);
            }
            
            if (error)
            {
                Debug.LogError($"Unable de deserialize PipelineAsset parameters in asset: {name}");
            }
        }
        
        static void GetPorts(IReflectNode node, out IList<IInput> inputs, out IList<IOutput> outputs, out IList<IParam> parameters)
        {
            inputs = new List<IInput>();
            outputs = new List<IOutput>();
            parameters = new List<IParam>();

            var fields = node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (!field.FieldType.IsSerializable)
                    continue;

                if (typeof(IInput).IsAssignableFrom(field.FieldType))
                {
                    var i = (IInput)field.GetValue(node);
                    inputs.Add(i);
                }
                else if (typeof(IOutput).IsAssignableFrom(field.FieldType))
                {
                    var o = (IOutput)field.GetValue(node);
                    outputs.Add(o);
                }
                else if (typeof(IParam).IsAssignableFrom(field.FieldType))
                {
                    var p = (IParam)field.GetValue(node);
                    parameters.Add(p);
                }
            }
        }

        public bool TryGetNode<T>(out T node) where T : class, IReflectNode
        {
            node = null;
            var result = m_Nodes?.FirstOrDefault(x => x.GetType().IsAssignableFrom(typeof(T)));

            if (result == null)
                return false;

            node = result as T;

            return node != null;
        }
        
        public IEnumerable<T> GetNodes<T>() where T : class, IReflectNode
        {
            return m_Nodes.Where(n => n is T).Cast<T>();
        }
    }
}
