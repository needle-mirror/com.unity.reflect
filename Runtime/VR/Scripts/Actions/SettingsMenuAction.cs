using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    [ActionMenuItem("Settings", "ReflectRadialMainMenu", (int)VRSetup.MenuActions.Settings)]
    public class SettingsMenuAction : ReflectMenuAction
    {
        public override void ExecuteAction()
        {
            base.ExecuteAction();

            m_VRSetup.InvokeMenuAction((int)VRSetup.MenuActions.Settings);
        }
    }
}
