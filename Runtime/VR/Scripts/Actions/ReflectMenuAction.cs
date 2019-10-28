using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Actions;

namespace UnityEngine.Reflect
{
    abstract public class ReflectMenuAction : BaseAction
    {
        protected VRSetup m_VRSetup;

        public override void ExecuteAction()
        {
            if (m_VRSetup == null)
            {
                m_VRSetup = FindObjectOfType<VRSetup>();
            }
        }
    }
}
