using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Open BIM Viewer", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.BimViewer)]
    public class OpenBimViewerAction : BaseAction, IUsesCreateWorkspace
    {
        IProvidesCreateWorkspace IFunctionalitySubscriber<IProvidesCreateWorkspace>.provider { get; set; }

        public override void ExecuteAction()
        {
            this.CreateWorkspace(typeof(BIMWorkspace));
        }
    }
}
