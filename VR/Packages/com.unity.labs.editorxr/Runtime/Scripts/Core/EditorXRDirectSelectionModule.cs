#if UNITY_2018_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEditor.Experimental.EditorVR.Proxies;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Core
{
    [ModuleBehaviorCallbackOrder(ModuleOrders.DirectSelectionModuleBehaviorOrder)]
    class EditorXRDirectSelectionModule : IDelayedInitializationModule,
        IInterfaceConnector, IModuleBehaviorCallbacks, IProvidesDirectSelection, IProvidesCanGrabObject, IUsesViewerBody
    {
        readonly Dictionary<Transform, DirectSelectionData> m_DirectSelections = new Dictionary<Transform, DirectSelectionData>();
        readonly Dictionary<Transform, HashSet<Transform>> m_GrabbedObjects = new Dictionary<Transform, HashSet<Transform>>();
        readonly List<IGrabObjects> m_ObjectGrabbers = new List<IGrabObjects>();
        readonly List<ITwoHandedScaler> m_TwoHandedScalers = new List<ITwoHandedScaler>();

        IntersectionModule m_IntersectionModule;
        EditorXRMiniWorldModule m_MiniWorldModule;
        EditorXRRayModule m_RayModule;
        SceneObjectModule m_SceneObjectModule;
        EditorXRViewerModule m_ViewerModule;
        EditorXRToolModule m_ToolModule;

        public event Action<Transform, HashSet<Transform>> objectsGrabbed;
        public event Action<Transform, Transform[]> objectsDropped;
        public event Action<Transform, Transform> objectsTransferred;

        public int initializationOrder { get { return 0; } }
        public int shutdownOrder { get { return 0; } }
        public int connectInterfaceOrder { get { return 0; } }

        event Action resetDirectSelectionState;

        IProvidesViewerBody IFunctionalitySubscriber<IProvidesViewerBody>.provider { get; set; }

        public void LoadModule()
        {
            IUsesPointerMethods.getPointerLength = GetPointerLength;

            var moduleLoaderCore = ModuleLoaderCore.instance;
            m_IntersectionModule = moduleLoaderCore.GetModule<IntersectionModule>();
            m_MiniWorldModule = moduleLoaderCore.GetModule<EditorXRMiniWorldModule>();
            m_RayModule = moduleLoaderCore.GetModule<EditorXRRayModule>();
            m_SceneObjectModule = moduleLoaderCore.GetModule<SceneObjectModule>();
            m_ViewerModule = moduleLoaderCore.GetModule<EditorXRViewerModule>();
            m_ToolModule = moduleLoaderCore.GetModule<EditorXRToolModule>();
        }

        public void UnloadModule() { }

        public void ConnectInterface(object target, object userData = null)
        {
            var grabObjects = target as IGrabObjects;
            if (grabObjects != null)
            {
                m_ObjectGrabbers.Add(grabObjects);
                grabObjects.objectsGrabbed += OnObjectsGrabbed;
                grabObjects.objectsDropped += OnObjectsDropped;
                grabObjects.objectsTransferred += OnObjectsTransferred;
            }

            var twoHandedScaler = target as ITwoHandedScaler;
            if (twoHandedScaler != null)
                m_TwoHandedScalers.Add(twoHandedScaler);
        }

        public void DisconnectInterface(object target, object userData = null)
        {
            var grabObjects = target as IGrabObjects;
            if (grabObjects != null)
            {
                m_ObjectGrabbers.Remove(grabObjects);
                grabObjects.objectsGrabbed -= OnObjectsGrabbed;
                grabObjects.objectsDropped -= OnObjectsDropped;
                grabObjects.objectsTransferred -= OnObjectsTransferred;
            }
        }

        // NOTE: This is for the length of the pointer object, not the length of the ray coming out of the pointer
        internal float GetPointerLength(Transform rayOrigin)
        {
            var length = 0f;

            EditorXRMiniWorldModule.MiniWorldRay ray = null;
            if (m_MiniWorldModule != null)
            {
                // Check if this is a MiniWorldRay
                if (m_MiniWorldModule.rays.TryGetValue(rayOrigin, out ray))
                    rayOrigin = ray.originalRayOrigin;
            }

            if (m_RayModule != null)
            {
                DefaultProxyRay dpr;
                if (m_RayModule.defaultRays.TryGetValue(rayOrigin, out dpr))
                {
                    length = dpr.pointerLength;

                    // If this is a MiniWorldRay, scale the pointer length to the correct size relative to MiniWorld objects
                    if (ray != null)
                    {
                        var miniWorld = ray.miniWorld;

                        // As the MiniWorld gets smaller, the ray length grows, hence lossyScale.Inverse().
                        // Assume that both transforms have uniform scale, so we just need .x
                        length *= miniWorld.referenceTransform.TransformVector(miniWorld.miniWorldTransform.lossyScale.Inverse()).x;
                    }
                }
            }

            return length;
        }

        internal bool IsHovering(Transform rayOrigin)
        {
            return m_DirectSelections.ContainsKey(rayOrigin);
        }

        internal bool IsScaling(Transform rayOrigin)
        {
            return m_TwoHandedScalers.Any(twoHandedScaler => twoHandedScaler.IsTwoHandedScaling(rayOrigin));
        }

        internal void UpdateDirectSelection()
        {
            m_DirectSelections.Clear();

            if (m_ToolModule != null)
            {
                foreach (var deviceData in m_ToolModule.deviceData)
                {
                    var proxy = deviceData.proxy;
                    if (!proxy.active)
                        continue;

                    var rayOrigin = deviceData.rayOrigin;
                    Vector3 contactPoint;
                    var obj = GetDirectSelectionForRayOrigin(rayOrigin, out contactPoint);
                    if (obj && !obj.CompareTag(EditorVR.VRPlayerTag))
                    {
                        m_DirectSelections[rayOrigin] = new DirectSelectionData
                        {
                            gameObject = obj,
                            contactPoint = contactPoint
                        };
                    }
                }
            }

            if (m_MiniWorldModule != null)
            {
                foreach (var ray in m_MiniWorldModule.rays)
                {
                    var rayOrigin = ray.Key;
                    Vector3 contactPoint;
                    var go = GetDirectSelectionForRayOrigin(rayOrigin, out contactPoint);
                    if (go != null)
                    {
                        m_DirectSelections[rayOrigin] = new DirectSelectionData
                        {
                            gameObject = go,
                            contactPoint = contactPoint
                        };
                    }
                }
            }
        }

        GameObject GetDirectSelectionForRayOrigin(Transform rayOrigin, out Vector3 contactPoint)
        {
            var tester = rayOrigin.GetComponentInChildren<IntersectionTester>();

            if (m_IntersectionModule == null)
            {
                contactPoint = default(Vector3);
                return null;
            }

            var renderer = m_IntersectionModule.GetIntersectedObjectForTester(tester, out contactPoint);
            if (renderer)
                return renderer.gameObject;

            return null;
        }

        public bool CanGrabObject(GameObject selection, Transform rayOrigin)
        {
            if (selection.CompareTag(EditorVR.VRPlayerTag) && (m_MiniWorldModule == null || !m_MiniWorldModule.rays.ContainsKey(rayOrigin)))
                return false;

            return true;
        }

        void OnObjectsGrabbed(Transform rayOrigin, HashSet<Transform> grabbedObjects)
        {
            HashSet<Transform> objects;
            if (!m_GrabbedObjects.TryGetValue(rayOrigin, out objects))
            {
                objects = new HashSet<Transform>();
                m_GrabbedObjects[rayOrigin] = objects;
            }

            objects.UnionWith(grabbedObjects);

            // Detach the player head model so that it is not affected by its parent transform
            foreach (var grabbedObject in grabbedObjects)
            {
                if (grabbedObject.CompareTag(EditorVR.VRPlayerTag))
                {
                    grabbedObject.hideFlags = HideFlags.None;
                    grabbedObject.transform.parent = null;
                }
            }

            if (objectsGrabbed != null)
                objectsGrabbed(rayOrigin, grabbedObjects);
        }

        void OnObjectsDropped(Transform rayOrigin, Transform[] grabbedObjects)
        {
            HashSet<Transform> objects;
            if (!m_GrabbedObjects.TryGetValue(rayOrigin, out objects))
                return;

            var eventObjects = new List<Transform>();
            foreach (var grabbedObject in grabbedObjects)
            {
                objects.Remove(grabbedObject);

                if (m_ViewerModule != null)
                {
                    // Dropping the player head updates the camera rig position
                    if (grabbedObject.CompareTag(EditorVR.VRPlayerTag))
                        m_ViewerModule.DropPlayerHead(grabbedObject);
                    else if (m_SceneObjectModule != null && this.IsOverShoulder(rayOrigin) && (m_MiniWorldModule == null || !m_MiniWorldModule.rays.ContainsKey(rayOrigin)))
                        m_SceneObjectModule.DeleteSceneObject(grabbedObject.gameObject);
                    else
                        eventObjects.Add(grabbedObject);
                }
            }

            if (objects.Count == 0)
                m_GrabbedObjects.Remove(rayOrigin);

            if (objectsDropped != null)
                objectsDropped(rayOrigin, eventObjects.ToArray());
        }

        void OnObjectsTransferred(Transform srcRayOrigin, Transform destRayOrigin)
        {
            m_GrabbedObjects[destRayOrigin] = m_GrabbedObjects[srcRayOrigin];
            m_GrabbedObjects.Remove(srcRayOrigin);

            if (objectsTransferred != null)
                objectsTransferred(srcRayOrigin, destRayOrigin);
        }

        public HashSet<Transform> GetHeldObjects(Transform rayOrigin)
        {
            HashSet<Transform> objects;
            return m_GrabbedObjects.TryGetValue(rayOrigin, out objects) ? objects : null;
        }

        public void SuspendGrabbers(Node node)
        {
            foreach (var grabber in m_ObjectGrabbers)
            {
                grabber.Suspend(node);
            }
        }

        public void ResumeGrabbers(Node node)
        {
            foreach (var grabber in m_ObjectGrabbers)
            {
                grabber.Resume(node);
            }
        }

        public void DropHeldObjects(Node node)
        {
            foreach (var grabber in m_ObjectGrabbers)
            {
                grabber.DropHeldObjects(node);
            }
        }

        public void TransferHeldObjects(Transform rayOrigin, Transform destRayOrigin, Vector3 deltaOffset = default(Vector3))
        {
            foreach (var grabber in m_ObjectGrabbers)
            {
                grabber.TransferHeldObjects(rayOrigin, destRayOrigin, deltaOffset);
            }
        }

        public Dictionary<Transform, DirectSelectionData> GetDirectSelection() { return m_DirectSelections; }

        public void ResetDirectSelectionState()
        {
            if (resetDirectSelectionState != null)
                resetDirectSelectionState();
        }

        public void SubscribeToResetDirectSelectionState(Action callback) { resetDirectSelectionState += callback; }

        public void UnsubscribeFromResetDirectSelectionState(Action callback) { resetDirectSelectionState -= callback; }

        public void Initialize()
        {
            resetDirectSelectionState = null;
            m_DirectSelections.Clear();
            m_GrabbedObjects.Clear();
            m_ObjectGrabbers.Clear();
            m_TwoHandedScalers.Clear();
        }

        public void Shutdown()
        {
            resetDirectSelectionState = null;
            m_DirectSelections.Clear();
            m_GrabbedObjects.Clear();
            m_ObjectGrabbers.Clear();
            m_TwoHandedScalers.Clear();
        }

        public void OnBehaviorAwake() { }

        public void OnBehaviorEnable() { }

        public void OnBehaviorStart() { }

        public void OnBehaviorUpdate()
        {
            UpdateDirectSelection();
        }

        public void OnBehaviorDisable() { }

        public void OnBehaviorDestroy() { }

        public void LoadProvider() { }

        public void ConnectSubscriber(object obj)
        {
#if !FI_AUTOFILL
            var directSelectionSubscriber = obj as IFunctionalitySubscriber<IProvidesDirectSelection>;
            if (directSelectionSubscriber != null)
                directSelectionSubscriber.provider = this;

            var canGrabObjectSubscriber = obj as IFunctionalitySubscriber<IProvidesCanGrabObject>;
            if (canGrabObjectSubscriber != null)
                canGrabObjectSubscriber.provider = this;
#endif
        }

        public void UnloadProvider() { }
    }
}
#endif
