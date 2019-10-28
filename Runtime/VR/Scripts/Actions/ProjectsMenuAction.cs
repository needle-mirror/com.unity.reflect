using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Projects", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.Projects)]
    public class ProjectsMenuAction : ReflectMenuAction
    {
        public override void ExecuteAction()
        {
            base.ExecuteAction();

            m_VRSetup.InvokeMenuAction((int)VRSetup.MenuActions.Projects);
        }
    }
}
