using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class ProjectsTopMenu : TopMenu
    {
        public void OnOpen()
        {
            Deactivate();
        }

        public void OnCancel()
        {
            Deactivate();
        }
    }
}
