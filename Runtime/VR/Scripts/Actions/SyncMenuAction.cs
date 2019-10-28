using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Sync", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.Sync)]
    public class SyncMenuAction : ReflectMenuAction
    {
        public override void ExecuteAction()
        {
            base.ExecuteAction();

            m_VRSetup.InvokeMenuAction((int)VRSetup.MenuActions.Sync);
        }
    }
}
