using System;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class ProjectTask : IDataInstance
    {
        public SyncId Id { get; set; }

        public SyncId ParentId { get; set; }

        public string DisplayName { get; set; }

        public string TaskType { get; set; }

        public DateTime? DateStart { get; set; }

        public DateTime? DateEnd { get; set; }

        public double Cost { get; set; }
        
        public DateTime? ActualDateStart { get; set;} 
        
        public DateTime? ActualDateEnd { get; set; }

        public int TaskIndex { get; set; }
        
        public static ProjectTask FromSyncModel(ISyncModel syncModel)
        {
            var syncProjectTask = syncModel as SyncProjectTask;

            var projectTask = new ProjectTask
            {
                Id = syncProjectTask.Id,
                DisplayName = syncProjectTask.DisplayName,
                ParentId = syncProjectTask.ParentId,
                TaskType = syncProjectTask.TaskType,
                DateStart = syncProjectTask.DateStart,
                DateEnd = syncProjectTask.DateEnd,
                Cost = syncProjectTask.Cost,
                ActualDateStart = syncProjectTask.ActualDateStart,
                ActualDateEnd = syncProjectTask.ActualDateEnd,
                TaskIndex = syncProjectTask.TaskIndex
            };

            return projectTask;
        }
    }
}
