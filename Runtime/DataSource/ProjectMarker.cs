using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class ProjectMarker : IDataInstance
    {
        public SyncId Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastUpdatedTime { get; set; }
        public DateTime LastUsedTime { get; set; }
        
        
        public static ProjectMarker FromSyncModel(ISyncModel syncModel)
        {
            var syncMarker = syncModel as SyncMarker;

            var projectMarker = new ProjectMarker
            {
                Id = syncMarker.Id,
                Name = syncMarker.Name,
                Position = new Vector3(syncMarker.Transform.Position.X, syncMarker.Transform.Position.Y, syncMarker.Transform.Position.Z),
                Rotation = new Quaternion(syncMarker.Transform.Rotation.X, syncMarker.Transform.Rotation.Y, syncMarker.Transform.Rotation.Z, syncMarker.Transform.Rotation.W).eulerAngles,
                Scale = new Vector3(syncMarker.Transform.Scale.X, syncMarker.Transform.Scale.Y, syncMarker.Transform.Scale.Z),
                CreatedTime = syncMarker.CreatedTime,
                LastUpdatedTime = syncMarker.LastUpdatedTime,
                LastUsedTime = syncMarker.LastUsedTime
            };

            return projectMarker;
        }
    }
}