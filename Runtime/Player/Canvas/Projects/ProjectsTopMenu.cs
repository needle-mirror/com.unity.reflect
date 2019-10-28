using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class ProjectsTopMenu : TopMenu
    {
        protected override void Start()
        {
            base.Start();
            StartCoroutine(DelayedStart());
        }
        
        public void OnOpen()
        {
            Deactivate();
        }

        public void OnCancel()
        {
            Deactivate();
        }

        IEnumerator DelayedStart()
        {
            yield return null;
            Activate();
        }
    }
}
