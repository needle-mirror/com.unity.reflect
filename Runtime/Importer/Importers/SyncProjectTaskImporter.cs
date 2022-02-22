using System;
using Unity.Collections;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{    
    public class SyncProjectTaskImporter : RuntimeImporter<SyncProjectTask, ProjectTask>
    {
        
        public override ProjectTask CreateNew(SyncProjectTask syncProjectTask, object settings)
        {
            return ProjectTask.FromSyncModel(syncProjectTask);
        }

        protected override void Clear(ProjectTask projectTask)
        {
            // Nothing.
        }

        protected override void ImportInternal(SyncedData<SyncProjectTask> syncProjectTask, ProjectTask projectTask, object settings)
        {
            // 
        }
        
    }
}
