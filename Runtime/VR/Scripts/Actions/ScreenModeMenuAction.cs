using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Screen Mode", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.ScreenMode)]
    public class ScreenModeMenuAction : ReflectMenuAction
    {
        public override void ExecuteAction()
        {
            base.ExecuteAction();

            m_VRSetup.InvokeMenuAction((int)VRSetup.MenuActions.ScreenMode);
        }
    }
}
