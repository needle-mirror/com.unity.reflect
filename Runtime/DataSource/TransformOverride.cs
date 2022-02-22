using System;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class TransformOverride : IDataInstance
    {
        public SyncId Id { get; set; }
        public string Name { get; set; }
        public string SourceId { get; set; }
        public SyncId InstanceId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Scale { get; set; }
        
        
        public static TransformOverride FromSyncModel(ISyncModel syncModel)
        {
            var syncTransformOverride = syncModel as SyncTransformOverride;

            var transformOverride = new TransformOverride
            {
                Id = syncTransformOverride.Id,
                Name = syncTransformOverride.Name,
                SourceId = syncTransformOverride.SourceId,
                InstanceId = syncTransformOverride.InstanceId,
                Position = new Vector3(syncTransformOverride.Transform.Position.X, syncTransformOverride.Transform.Position.Y, syncTransformOverride.Transform.Position.Z),
                Rotation = new Quaternion(syncTransformOverride.Transform.Rotation.X, syncTransformOverride.Transform.Rotation.Y, syncTransformOverride.Transform.Rotation.Z, syncTransformOverride.Transform.Rotation.W).eulerAngles,
                Scale = new Vector3(syncTransformOverride.Transform.Scale.X, syncTransformOverride.Transform.Scale.Y, syncTransformOverride.Transform.Scale.Z)
            };

            return transformOverride;
        }
    }
}