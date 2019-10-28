using System.Collections;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Core;

namespace UnityEngine.Reflect
{ 
    public class ReflectRadialMenuActivator : MonoBehaviour, 
        INodeToRay, 
        IUsesConnectInterfaces, 
        IUsesInstantiateMenuUI
    {
        public ReflectRadialMenu MenuPrefab;

        ReflectRadialMenu reflectRadialMenu;

        IProvidesConnectInterfaces IFunctionalitySubscriber<IProvidesConnectInterfaces>.provider { get; set; }

        public IProvidesInstantiateMenuUI provider { get; set; }

        protected void OnEnable()
        {
            StartCoroutine(InitCR());
        }

        protected IEnumerator InitCR()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            FunctionalityInjectionModule.instance.activeIsland.InjectFunctionalitySingle(this);

            Transform leftRayOrigin = this.RequestRayOriginFromNode(Node.LeftHand);
            Transform rightRayOrigin = this.RequestRayOriginFromNode(Node.RightHand);
            reflectRadialMenu = this.InstantiateMenuUI(rightRayOrigin, MenuPrefab).GetComponent<ReflectRadialMenu>();
            this.ConnectInterfaces(reflectRadialMenu, leftRayOrigin);
            reflectRadialMenu.Init(Node.LeftHand, leftRayOrigin);
        }
    }
}

