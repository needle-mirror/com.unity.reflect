using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Filter", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.Filter)]
    public class FilterMenuAction : ReflectMenuAction
    {
        public override void ExecuteAction()
        {
            base.ExecuteAction();

            m_VRSetup.InvokeMenuAction((int)VRSetup.MenuActions.Filter);
        }
    }
}
