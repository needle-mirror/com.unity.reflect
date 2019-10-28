using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;
using UnityEditor.Experimental.EditorVR.Workspaces;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Open Mini World", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.MiniWorld)]
    public class OpenMiniWorldAction : BaseAction, IUsesCreateWorkspace
    {
        IProvidesCreateWorkspace IFunctionalitySubscriber<IProvidesCreateWorkspace>.provider { get; set; }

        public override void ExecuteAction()
        {
            this.CreateWorkspace(typeof(ReflectMiniWorldWorkspace));
        }
    }
}
