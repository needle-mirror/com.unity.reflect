using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class ProjectsTopMenu : TopMenu
    {
        protected override void Start()
        {
            base.Start();
            Activate();
        }
        
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
