using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Core;

namespace UnityEngine.Reflect
{ 
    public class ReflectRadialMenuTool : MonoBehaviour, 
        ITool, 
        INodeToRay, 
        IUsesConnectInterfaces, 
        IUsesInstantiateMenuUI, 
        IUsesHandedRayOrigin
    {
        public ReflectRadialMenu MenuPrefab;

        ReflectRadialMenu reflectRadialMenu;

        IProvidesConnectInterfaces IFunctionalitySubscriber<IProvidesConnectInterfaces>.provider { get; set; }

        public IProvidesInstantiateMenuUI provider { get; set; }

        public Transform rayOrigin { get; set; }
        public Node node { internal get; set; }

        protected void Start()
        {
            if (node == Node.LeftHand)
            {
                rayOrigin = this.RequestRayOriginFromNode(Node.LeftHand);
                Transform otherRayOrigin = this.RequestRayOriginFromNode(Node.RightHand);
                reflectRadialMenu = this.InstantiateMenuUI(otherRayOrigin, MenuPrefab).GetComponent<ReflectRadialMenu>();
                this.ConnectInterfaces(reflectRadialMenu, rayOrigin);
                reflectRadialMenu.Init(Node.LeftHand, rayOrigin);
            }
        }
    }
}

