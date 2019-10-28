using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Manipulators;
using UnityEditor.Experimental.EditorVR.Proxies;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.InputNew;

#if !UNITY_EDITOR
public enum PivotRotation
{
    Local,
    Global,
}
#endif

namespace UnityEditor.Experimental.EditorVR.Tools
{
    public sealed class TransformTool : MonoBehaviour, ITool, ITransformer, ISelectionChanged, IActions, IUsesDirectSelection,
        IGrabObjects, IUsesSelectObject, IManipulatorController, IUsesSnapping, IUsesSetHighlight, ILinkedObject, IRayToNode,
        IUsesControlHaptics, IUsesRayOrigin, IUsesNode, ICustomActionMap, ITwoHandedScaler, IUsesIsMainMenuVisible,
        IUsesGetRayVisibility, IUsesRayVisibilitySettings, IUsesRequestFeedback, IUsesFunctionalityInjection
    {
        enum TwoHandedManipulateMode
        {
            ScaleOnly,
            RotateAndScale,
            RotateOnly // Disabled until visual improvement is implemented
        }

        class GrabData
        {
            Vector3[] m_PositionOffsets;
            Quaternion[] m_RotationOffsets;
            Vector3[] m_InitialScales;

            Vector3[] m_OriginalPositions;
            Quaternion[] m_OriginalRotations;
            Vector3[] m_OriginalScales;
            Vector3 m_StartMidPoint;
            Quaternion m_UpRotationOffset;
            Quaternion m_ForwardRotationOffset;
            float m_StartDistance;
            bool m_UseUp;

            readonly Vector3 m_GrabOffset;

            public Transform[] grabbedTransforms { get; private set; }
            public TransformInput input { get; private set; }
            public Transform rayOrigin { get; private set; }
            public TwoHandedManipulateMode twoHandedManipulateMode { get; set; }

            public bool suspended { private get; set; }

            Vector3 pivotPoint { get { return rayOrigin.position + rayOrigin.rotation * m_GrabOffset; } }

            public GrabData(Transform rayOrigin, TransformInput input, Transform[] grabbedTransforms, Vector3 contactPoint)
            {
                this.rayOrigin = rayOrigin;
                this.input = input;
                this.grabbedTransforms = grabbedTransforms;
                var inverseRotation = Quaternion.Inverse(rayOrigin.rotation);
                m_GrabOffset = inverseRotation * (contactPoint - rayOrigin.position);
                CaptureInitialTransforms();
                Reset();
            }

            void CaptureInitialTransforms()
            {
                var length = grabbedTransforms.Length;
                m_OriginalPositions = new Vector3[length];
                m_OriginalRotations = new Quaternion[length];
                m_OriginalScales = new Vector3[length];
                for (var i = 0; i < length; i++)
                {
                    var grabbedTransform = grabbedTransforms[i];
                    m_OriginalPositions[i] = grabbedTransform.position;
                    m_OriginalRotations[i] = grabbedTransform.rotation;
                    m_OriginalScales[i] = grabbedTransform.localScale;
                }
            }

            public void Reset()
            {
                if (suspended)
                    return;

                var length = grabbedTransforms.Length;
                m_PositionOffsets = new Vector3[length];
                m_RotationOffsets = new Quaternion[length];
                m_InitialScales = new Vector3[length];
                var pivot = pivotPoint;
                for (var i = 0; i < length; i++)
                {
                    var grabbedTransform = grabbedTransforms[i];

                    var inverseRotation = Quaternion.Inverse(rayOrigin.rotation);
                    m_PositionOffsets[i] = inverseRotation * (grabbedTransform.position - pivot);
                    m_RotationOffsets[i] = inverseRotation * grabbedTransform.rotation;
                    m_InitialScales[i] = grabbedTransform.localScale;
                }
            }

            public void UpdatePositions(IUsesSnapping usesSnapping, bool interpolate = true)
            {
                if (suspended)
                    return;

#if UNITY_EDITOR
                Undo.RecordObjects(grabbedTransforms, "Move");
#endif

                var rayOriginRotation = rayOrigin.rotation;
                var pivot = pivotPoint;
                for (var i = 0; i < grabbedTransforms.Length; i++)
                {
                    var grabbedObject = grabbedTransforms[i];
                    var position = grabbedObject.position;
                    var rotation = grabbedObject.rotation;
                    var targetPosition = pivot + rayOriginRotation * m_PositionOffsets[i];
                    var targetRotation = rayOriginRotation * m_RotationOffsets[i];

                    if (usesSnapping.DirectSnap(rayOrigin, grabbedObject, ref position, ref rotation, targetPosition, targetRotation))
                    {
                        if (interpolate)
                        {
                            var deltaTime = Time.deltaTime;
                            grabbedObject.position = Vector3.Lerp(grabbedObject.position, position, k_DirectLazyFollowTranslate * deltaTime);
                            grabbedObject.rotation = Quaternion.Lerp(grabbedObject.rotation, rotation, k_DirectLazyFollowRotate * deltaTime);
                        }
                        else
                        {
                            grabbedObject.position = position;
                            grabbedObject.rotation = rotation;
                        }
                    }
                    else
                    {
                        grabbedObject.position = targetPosition;
                        grabbedObject.rotation = targetRotation;
                    }
                }
            }

            public void ScaleObjects(GrabData otherGrab)
            {
                if (suspended)
                    return;

#if UNITY_EDITOR
                Undo.RecordObjects(grabbedTransforms, "Move");
#endif

                var thisPosition = pivotPoint;
                var otherPosition = otherGrab.pivotPoint;
                var distance = Vector3.Distance(thisPosition, otherPosition);
                var scaleFactor = distance / m_StartDistance;
                if (scaleFactor > 0 && scaleFactor < Mathf.Infinity)
                {
                    var rayToRay = otherPosition - thisPosition;
                    var midPoint = thisPosition + rayToRay * 0.5f;
                    var forward = Vector3.Slerp(rayOrigin.forward, otherGrab.rayOrigin.forward, 0.5f);
                    var upVector = Vector3.Slerp(rayOrigin.up, otherGrab.rayOrigin.up, 0.5f);

                    var grabRotation = Quaternion.LookRotation(rayToRay, m_UseUp ? upVector : forward);
                    var rotationOffset = grabRotation * (m_UseUp ? m_UpRotationOffset : m_ForwardRotationOffset);

                    for (var i = 0; i < grabbedTransforms.Length; i++)
                    {
                        var grabbedObject = grabbedTransforms[i];

                        if (twoHandedManipulateMode == TwoHandedManipulateMode.RotateOnly)
                        {
                            var targetPosition = midPoint + rotationOffset * m_PositionOffsets[i];
                            var currentPosition = grabbedObject.position;
                            if (currentPosition != targetPosition)
                                grabbedObject.position = Vector3.Lerp(currentPosition, targetPosition, k_DirectLazyFollowTranslate);

                            var targetScale = m_InitialScales[i];
                            if (grabbedObject.localScale != targetScale)
                                grabbedObject.localScale = targetScale;
                        }
                        else
                        {
                            var offset = m_PositionOffsets[i] * scaleFactor;
                            if (twoHandedManipulateMode != TwoHandedManipulateMode.ScaleOnly)
                                offset = rotationOffset * offset;

                            var targetPosition = midPoint + offset;
                            var currentPosition = grabbedObject.position;
                            if (currentPosition != targetPosition)
                                grabbedObject.position = Vector3.Lerp(currentPosition, targetPosition, k_DirectLazyFollowTranslate);

                            var targetScale = m_InitialScales[i] * scaleFactor;
                            if (grabbedObject.localScale != targetScale)
                                grabbedObject.localScale = targetScale;
                        }

                        if (twoHandedManipulateMode == TwoHandedManipulateMode.ScaleOnly)
                            grabbedObject.rotation = Quaternion.Lerp(grabbedObject.rotation, m_RotationOffsets[i], k_DirectLazyFollowRotate);
                        else
                            grabbedObject.rotation = Quaternion.Lerp(grabbedObject.rotation, rotationOffset * m_RotationOffsets[i], k_DirectLazyFollowRotate);
                    }
                }
            }

            public void TransferTo(Transform destRayOrigin, Vector3 deltaOffset)
            {
                rayOrigin = destRayOrigin;
                for (var i = 0; i < m_PositionOffsets.Length; i++)
                {
                    m_PositionOffsets[i] += deltaOffset;
                }
            }

            public void StartScaling(GrabData otherGrab)
            {
                var thisPosition = pivotPoint;
                var otherPosition = otherGrab.pivotPoint;
                var rayToRay = otherPosition - thisPosition;
                m_StartMidPoint = thisPosition + rayToRay * 0.5f;
                m_StartDistance = Vector3.Distance(thisPosition, otherPosition);

                m_UseUp = Vector3.Dot(rayOrigin.forward, otherGrab.rayOrigin.forward) < -0.5f;
                var forward = Vector3.Slerp(rayOrigin.forward, otherGrab.rayOrigin.forward, 0.5f);
                var upVector = Vector3.Slerp(rayOrigin.up, otherGrab.rayOrigin.up, 0.5f);
                m_UpRotationOffset = Quaternion.Inverse(Quaternion.LookRotation(rayToRay, upVector));
                m_ForwardRotationOffset = Quaternion.Inverse(Quaternion.LookRotation(rayToRay, forward));

                for (var i = 0; i < grabbedTransforms.Length; i++)
                {
                    var grabbedObject = grabbedTransforms[i];
                    m_PositionOffsets[i] = grabbedObject.position - m_StartMidPoint;
                    m_RotationOffsets[i] = grabbedObject.rotation;
                }

                // Transfer the first grab's original transform data, for eventual cancel
                otherGrab.m_OriginalPositions = m_OriginalPositions;
                otherGrab.m_OriginalRotations = m_OriginalRotations;
                otherGrab.m_OriginalScales = m_OriginalScales;
            }

            public void Cancel()
            {
                var length = grabbedTransforms.Length;
                for (var i = 0; i < length; i++)
                {
                    var grabbedTransform = grabbedTransforms[i];
                    grabbedTransform.position = m_OriginalPositions[i];
                    grabbedTransform.rotation = m_OriginalRotations[i];
                    grabbedTransform.localScale = m_OriginalScales[i];
                }
            }
        }

        class TransformAction : IAction, ITooltip
        {
            internal Func<bool> execute;
            public string tooltipText { get; internal set; }
            public Sprite icon { get; internal set; }

            public void ExecuteAction()
            {
                if (execute != null)
                    execute();
            }
        }

        const float k_LazyFollowTranslate = 8f;
        const float k_LazyFollowRotate = 12f;
        const float k_DirectLazyFollowTranslate = 20f;
        const float k_DirectLazyFollowRotate = 30f;

#pragma warning disable 649
        [SerializeField]
        Sprite m_OriginCenterIcon;

        [SerializeField]
        Sprite m_OriginPivotIcon;

        [SerializeField]
        Sprite m_RotationGlobalIcon;

        [SerializeField]
        Sprite m_RotationLocalIcon;

        [SerializeField]
        Sprite m_StandardManipulatorIcon;

        [SerializeField]
        Sprite m_ScaleManipulatorIcon;

        [SerializeField]
        GameObject m_StandardManipulatorPrefab;

        [SerializeField]
        GameObject m_ScaleManipulatorPrefab;

        [SerializeField]
        ActionMap m_ActionMap;

        [SerializeField]
        HapticPulse m_DragPulse;

        [SerializeField]
        HapticPulse m_RotatePulse;
#pragma warning restore 649

        List<IAction> m_Actions;

        BaseManipulator m_CurrentManipulator;

        BaseManipulator m_StandardManipulator;
        BaseManipulator m_ScaleManipulator;

        Bounds m_SelectionBounds;
        Vector3 m_TargetPosition;
        Quaternion m_TargetRotation;
        Vector3 m_TargetScale;
        Quaternion m_PositionOffsetRotation;
        Quaternion m_StartRotation;

        readonly Dictionary<Transform, Vector3> m_PositionOffsets = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Quaternion> m_RotationOffsets = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> m_ScaleOffsets = new Dictionary<Transform, Vector3>();

        PivotRotation m_PivotRotation = PivotRotation.Local;
        PivotMode m_PivotMode = PivotMode.Pivot;

        GrabData m_LeftGrabData, m_RightGrabData;
        bool m_DirectSelected;
        Node m_ScaleFirstNode;
        bool m_Scaling;
        bool m_CurrentlySnapping;

        TransformInput m_Input;

        readonly BindingDictionary m_Controls = new BindingDictionary();
        readonly List<ProxyFeedbackRequest> m_GrabFeedback = new List<ProxyFeedbackRequest>();
        readonly List<ProxyFeedbackRequest> m_ScaleFeedback = new List<ProxyFeedbackRequest>();
        readonly List<ProxyFeedbackRequest> m_ScaleOptionFeedback = new List<ProxyFeedbackRequest>();

        readonly TransformAction m_PivotModeToggleAction = new TransformAction();
        readonly TransformAction m_PivotRotationToggleAction = new TransformAction();
        readonly TransformAction m_ManipulatorToggleAction = new TransformAction();

        public event Action<Transform, HashSet<Transform>> objectsGrabbed;
        public event Action<Transform, Transform[]> objectsDropped;
        public event Action<Transform, Transform> objectsTransferred;

        public List<IAction> actions
        {
            get
            {
                if (!this.IsSharedUpdater(this))
                    return null;

                if (m_Actions == null)
                {
                    m_Actions = new List<IAction>
                    {
                        m_PivotModeToggleAction,
                        m_PivotRotationToggleAction,
                        m_ManipulatorToggleAction
                    };
                }
                return m_Actions;
            }
        }

        public bool manipulatorVisible { private get; set; }

        public bool manipulatorDragging
        {
            get
            {
                return
                    m_StandardManipulator && m_StandardManipulator.dragging
                    || m_ScaleManipulator && m_ScaleManipulator.dragging;
            }
        }

        public List<ILinkedObject> linkedObjects { private get; set; }

        public Transform rayOrigin { private get; set; }
        public Node node { private get; set; }

        public ActionMap actionMap { get { return m_ActionMap; } }
        public bool ignoreActionMapInputLocking { get { return false; } }

#if !FI_AUTOFILL
        IProvidesSnapping IFunctionalitySubscriber<IProvidesSnapping>.provider { get; set; }
        IProvidesFunctionalityInjection IFunctionalitySubscriber<IProvidesFunctionalityInjection>.provider { get; set; }
        IProvidesDirectSelection IFunctionalitySubscriber<IProvidesDirectSelection>.provider { get; set; }
        IProvidesSetHighlight IFunctionalitySubscriber<IProvidesSetHighlight>.provider { get; set; }
        IProvidesSelectObject IFunctionalitySubscriber<IProvidesSelectObject>.provider { get; set; }
        IProvidesRequestFeedback IFunctionalitySubscriber<IProvidesRequestFeedback>.provider { get; set; }
        IProvidesRayVisibilitySettings IFunctionalitySubscriber<IProvidesRayVisibilitySettings>.provider { get; set; }
        IProvidesIsMainMenuVisible IFunctionalitySubscriber<IProvidesIsMainMenuVisible>.provider { get; set; }
        IProvidesGetRayVisibility IFunctionalitySubscriber<IProvidesGetRayVisibility>.provider { get; set; }
        IProvidesControlHaptics IFunctionalitySubscriber<IProvidesControlHaptics>.provider { get; set; }
        IProvidesCanGrabObject IFunctionalitySubscriber<IProvidesCanGrabObject>.provider { get; set; }
#endif

        void Start()
        {
            if (!this.IsSharedUpdater(this))
                return;

            m_PivotModeToggleAction.execute = TogglePivotMode;
            UpdatePivotModeAction();
            m_PivotRotationToggleAction.execute = TogglePivotRotation;
            UpdatePivotRotationAction();
            m_ManipulatorToggleAction.execute = ToggleManipulator;
            UpdateManipulatorAction();

            // Add standard and scale manipulator prefabs to a list (because you cannot add asset references directly to a serialized list)
            if (m_StandardManipulatorPrefab != null)
                m_StandardManipulator = CreateManipulator(m_StandardManipulatorPrefab);

            if (m_ScaleManipulatorPrefab != null)
                m_ScaleManipulator = CreateManipulator(m_ScaleManipulatorPrefab);

            m_CurrentManipulator = m_StandardManipulator;

            InputUtils.GetBindingDictionaryFromActionMap(m_ActionMap, m_Controls);

            this.SubscribeToResetDirectSelectionState(OnResetDirectSelectionState);
        }

        public void OnSelectionChanged()
        {
            if (!this.IsSharedUpdater(this) || !m_CurrentManipulator)
                return;

            if (Selection.gameObjects.Length == 0)
                m_CurrentManipulator.gameObject.SetActive(false);
            else
                UpdateCurrentManipulator();
        }

        public void ProcessInput(ActionMapInput input, ConsumeControlDelegate consumeControl)
        {
            m_Input = (TransformInput)input;

            if (!this.IsSharedUpdater(this))
                return;

            var hasObject = false;
            var manipulatorGameObject = m_CurrentManipulator.gameObject;
            var gameObjects = Selection.gameObjects;
            if (!m_CurrentManipulator.dragging)
            {
                var directSelection = this.GetDirectSelection();

                var hasLeft = m_LeftGrabData != null;
                var hasRight = m_RightGrabData != null;
                hasObject = directSelection.Count > 0 || hasLeft || hasRight;

                var hoveringSelection = false;
                foreach (var kvp in directSelection)
                {
                    if (gameObjects.Contains(kvp.Value.gameObject))
                    {
                        hoveringSelection = true;
                        break;
                    }
                }

                // Disable manipulator on direct hover or drag
                if (manipulatorGameObject.activeSelf && (hoveringSelection || hasLeft || hasRight))
                    manipulatorGameObject.SetActive(false);

                var scaleHover = false;
                foreach (var kvp in directSelection)
                {
                    var directRayOrigin = kvp.Key;
                    var directSelectionData = kvp.Value;

                    if (!(hasLeft || hasRight) && this.IsMainMenuVisible(directRayOrigin))
                        continue;

                    var directHoveredObject = directSelectionData.gameObject;

                    var selectionCandidate = this.GetSelectionCandidate(directHoveredObject, true);

                    // Can't select this object (it might be locked or static)
                    if (directHoveredObject && !selectionCandidate)
                        continue;

                    if (selectionCandidate)
                        directHoveredObject = selectionCandidate;

                    if (!this.CanGrabObject(directHoveredObject, directRayOrigin))
                        continue;

                    this.AddRayVisibilitySettings(directRayOrigin, this, false, true); // This will also disable ray selection

                    if (!this.IsConeVisible(directRayOrigin))
                        continue;

                    var grabbingNode = this.RequestNodeFromRayOrigin(directRayOrigin);
                    var transformTool = linkedObjects.Cast<TransformTool>().FirstOrDefault(linkedObject => linkedObject.node == grabbingNode);
                    if (transformTool == null)
                        continue;

                    // Check if the other hand is already grabbing for two-handed scale
                    var otherData = grabbingNode == Node.LeftHand ? m_RightGrabData : m_LeftGrabData;

                    if (otherData != null && !otherData.grabbedTransforms.Contains(directHoveredObject.transform))
                        otherData = null;

                    if (otherData != null)
                    {
                        scaleHover = true;
                        if (m_ScaleFeedback.Count == 0)
                            ShowScaleFeedback(grabbingNode);
                    }

                    var transformInput = transformTool.m_Input;

                    if (transformInput.select.wasJustPressed)
                    {
                        this.ClearSnappingState(directRayOrigin);

                        consumeControl(transformInput.select);

                        var grabbedObjects = new HashSet<Transform> { directHoveredObject.transform };
                        grabbedObjects.UnionWith(Selection.transforms);

                        if (objectsGrabbed != null && !m_Scaling)
                            objectsGrabbed(directRayOrigin, grabbedObjects);

                        var grabData = new GrabData(directRayOrigin, transformInput, grabbedObjects.ToArray(), directSelectionData.contactPoint);
                        if (grabbingNode == Node.LeftHand)
                            m_LeftGrabData = grabData;
                        else
                            m_RightGrabData = grabData;

                        ShowGrabFeedback(grabbingNode);
                        if (otherData != null)
                        {
                            m_ScaleFirstNode = grabbingNode == Node.LeftHand ? Node.RightHand : Node.LeftHand;
                            otherData.StartScaling(grabData);
                            ShowScaleOptionsFeedback(otherData.twoHandedManipulateMode);
                            m_Scaling = true;
                        }

                        // A direct selection has been made. Hide the manipulator until the selection changes
                        m_DirectSelected = true;

#if UNITY_EDITOR
                        Undo.IncrementCurrentGroup();
#endif
                    }
                }

                if (!scaleHover)
                    HideScaleFeedback();

                hasLeft = m_LeftGrabData != null;
                hasRight = m_RightGrabData != null;

                var leftInput = m_LeftGrabData != null ? m_LeftGrabData.input : null;
                var leftHeld = m_LeftGrabData != null && leftInput.select.isHeld;
                var rightInput = m_RightGrabData != null ? m_RightGrabData.input : null;
                var rightHeld = m_RightGrabData != null && rightInput.select.isHeld;

                if (hasLeft)
                {
                    consumeControl(leftInput.cancel);
                    consumeControl(leftInput.suppressVertical);
                    if (!m_Scaling && leftInput.cancel.wasJustPressed)
                    {
                        m_LeftGrabData.Cancel();
                        DropHeldObjects(Node.LeftHand);
                        hasLeft = false;
                    }

                    if (leftInput.select.wasJustReleased)
                    {
                        if (rightInput != null && rightInput.select.wasJustReleased)
                        {
                            HideScaleOptionFeedback();
                            m_Scaling = false;
                        }

                        DropHeldObjects(Node.LeftHand);
                        hasLeft = false;
                        consumeControl(leftInput.select);
                    }
                }

                if (hasRight)
                {
                    consumeControl(rightInput.cancel);
                    consumeControl(rightInput.suppressVertical);
                    if (!m_Scaling && rightInput.cancel.wasJustPressed)
                    {
                        m_RightGrabData.Cancel();
                        DropHeldObjects(Node.RightHand);
                        hasRight = false;
                    }

                    if (rightInput.select.wasJustReleased)
                    {
                        if (leftInput != null && leftInput.select.wasJustReleased)
                        {
                            HideScaleOptionFeedback();
                            m_Scaling = false;
                        }

                        DropHeldObjects(Node.RightHand);
                        hasRight = false;
                        consumeControl(rightInput.select);
                    }
                }

                if (hasLeft && hasRight && leftHeld && rightHeld && m_Scaling) // Two-handed scaling
                {
                    var rightRayOrigin = m_RightGrabData.rayOrigin;
                    var leftRayOrigin = m_LeftGrabData.rayOrigin;
                    var leftCancel = leftInput.cancel;
                    var rightCancel = rightInput.cancel;

                    var scaleGrabData = m_ScaleFirstNode == Node.LeftHand ? m_LeftGrabData : m_RightGrabData;
                    if (leftCancel.wasJustPressed)
                    {
                        if (scaleGrabData.twoHandedManipulateMode == TwoHandedManipulateMode.ScaleOnly)
                            scaleGrabData.twoHandedManipulateMode = TwoHandedManipulateMode.RotateAndScale;
                        else
                            scaleGrabData.twoHandedManipulateMode = TwoHandedManipulateMode.ScaleOnly;

                        ShowScaleOptionsFeedback(scaleGrabData.twoHandedManipulateMode);
                    }

                    if (rightCancel.wasJustPressed)
                    {
                        HideScaleOptionFeedback();
                        m_Scaling = false;
                        if (m_ScaleFirstNode == Node.LeftHand)
                            m_LeftGrabData.Cancel();
                        else
                            m_RightGrabData.Cancel();

                        DropHeldObjects(Node.RightHand);
                        DropHeldObjects(Node.LeftHand);
                    }
                    else if (m_ScaleFirstNode == Node.LeftHand)
                    {
                        m_LeftGrabData.ScaleObjects(m_RightGrabData);
                        this.ClearSnappingState(leftRayOrigin);
                    }
                    else
                    {
                        m_RightGrabData.ScaleObjects(m_LeftGrabData);
                        this.ClearSnappingState(rightRayOrigin);
                    }
                }
                else
                {
                    // If m_Scaling is true but both hands don't have a grab, we need to transfer back to one-handed manipulation
                    // Offsets will change while scaling. Whichever hand keeps holding the trigger after scaling is done will need to reset itself
                    if (m_Scaling)
                    {
                        if (hasLeft)
                        {
                            m_LeftGrabData.Reset();

                            if (objectsTransferred != null && m_ScaleFirstNode == Node.RightHand)
                                objectsTransferred(m_RightGrabData.rayOrigin, m_LeftGrabData.rayOrigin);
                        }

                        if (hasRight)
                        {
                            m_RightGrabData.Reset();

                            if (objectsTransferred != null && m_ScaleFirstNode == Node.LeftHand)
                                objectsTransferred(m_LeftGrabData.rayOrigin, m_RightGrabData.rayOrigin);
                        }

                        HideScaleOptionFeedback();
                        m_Scaling = false;
                    }

                    if (hasLeft && leftHeld)
                        m_LeftGrabData.UpdatePositions(this);

                    if (hasRight && rightHeld)
                        m_RightGrabData.UpdatePositions(this);
                }

                foreach (var linkedObject in linkedObjects)
                {
                    var transformTool = (TransformTool)linkedObject;
                    var otherRayOrigin = transformTool.rayOrigin;
                    if (!(m_Scaling || directSelection.ContainsKey(otherRayOrigin) || GrabDataForNode(transformTool.node) != null))
                    {
                        this.RemoveRayVisibilitySettings(otherRayOrigin, this);
                    }
                }
            }

            // Manipulator is disabled while direct manipulation is happening
            if (hasObject || m_DirectSelected)
                return;

            if (gameObjects.Length > 0)
            {
                if (!m_CurrentManipulator.dragging)
                    UpdateCurrentManipulator();

                var deltaTime = Time.deltaTime;
                var manipulatorTransform = manipulatorGameObject.transform;
                var lerp = m_CurrentlySnapping ? 1f : k_LazyFollowTranslate * deltaTime;
                manipulatorTransform.position = Vector3.Lerp(manipulatorTransform.position, m_TargetPosition, lerp);

                // Manipulator does not rotate when in global mode
                if (m_PivotRotation == PivotRotation.Local && m_CurrentManipulator == m_StandardManipulator)
                    manipulatorTransform.rotation = Quaternion.Slerp(manipulatorTransform.rotation, m_TargetRotation, k_LazyFollowRotate * deltaTime);

                var selectionTransforms = Selection.transforms;
#if UNITY_EDITOR
                Undo.RecordObjects(selectionTransforms, "Move");
#endif

                foreach (var t in selectionTransforms)
                {
                    var targetRotation = Quaternion.Slerp(t.rotation, m_TargetRotation * m_RotationOffsets[t], k_LazyFollowRotate * deltaTime);
                    if (t.rotation != targetRotation)
                        t.rotation = targetRotation;

                    Vector3 targetPosition;
                    if (m_PivotMode == PivotMode.Center) // Rotate the position offset from the manipulator when rotating around center
                    {
                        m_PositionOffsetRotation = Quaternion.Slerp(m_PositionOffsetRotation, m_TargetRotation * Quaternion.Inverse(m_StartRotation), k_LazyFollowRotate * deltaTime);
                        targetPosition = manipulatorTransform.position + m_PositionOffsetRotation * m_PositionOffsets[t];
                    }
                    else
                    {
                        targetPosition = manipulatorTransform.position + m_PositionOffsets[t];
                    }

                    if (t.position != targetPosition)
                        t.position = targetPosition;

                    var targetScale = Vector3.Lerp(t.localScale, Vector3.Scale(m_TargetScale, m_ScaleOffsets[t]), k_LazyFollowTranslate * deltaTime);
                    if (t.localScale != targetScale)
                        t.localScale = targetScale;
                }
            }
        }

        public void Suspend(Node node)
        {
            var grabData = GrabDataForNode(node);
            if (grabData != null)
                grabData.suspended = true;
        }

        public void Resume(Node node)
        {
            var grabData = GrabDataForNode(node);
            if (grabData != null)
            {
                grabData.suspended = false;
                grabData.UpdatePositions(this, false);
            }
        }

        public Transform[] GetHeldObjects(Node node)
        {
            var grabData = GrabDataForNode(node);
            return grabData == null ? null : grabData.grabbedTransforms;
        }

        public void TransferHeldObjects(Transform rayOrigin, Transform destRayOrigin, Vector3 deltaOffset = default(Vector3))
        {
            if (!this.IsSharedUpdater(this))
                return;

            var grabData = GrabDataForRayOrigin(rayOrigin);

            if (grabData == null)
                return;

            grabData.TransferTo(destRayOrigin, deltaOffset);
            this.ClearSnappingState(rayOrigin);
            grabData.UpdatePositions(this, false);

            // Prevent lock from getting stuck
            this.RemoveRayVisibilitySettings(rayOrigin, this);
            this.AddRayVisibilitySettings(destRayOrigin, this, false, true);

            if (objectsTransferred != null)
                objectsTransferred(rayOrigin, destRayOrigin);
        }

        public void DropHeldObjects(Node node)
        {
            if (!this.IsSharedUpdater(this))
                return;

            var grabData = GrabDataForNode(node);
            var grabbedObjects = grabData.grabbedTransforms;
            var rayOrigin = grabData.rayOrigin;

            if (objectsDropped != null)
                objectsDropped(rayOrigin, grabbedObjects);

            if (node == Node.LeftHand)
                m_LeftGrabData = null;
            else
                m_RightGrabData = null;

            m_ScaleFirstNode = Node.None;

            HideGrabFeedback();

            this.RemoveRayVisibilitySettings(grabData.rayOrigin, this);

            this.ClearSnappingState(rayOrigin);
        }

        void Translate(Vector3 delta, Transform rayOrigin, AxisFlags constraints)
        {
            switch (constraints)
            {
                case AxisFlags.X | AxisFlags.Y:
                case AxisFlags.Y | AxisFlags.Z:
                case AxisFlags.X | AxisFlags.Z:
                    m_TargetPosition += delta;
                    break;
                default:
                    m_CurrentlySnapping = this.ManipulatorSnap(rayOrigin, Selection.transforms, ref m_TargetPosition, ref m_TargetRotation, delta, constraints, m_PivotMode);

                    if (constraints == 0)
                        m_CurrentlySnapping = false;

                    break;
            }

            this.Pulse(this.RequestNodeFromRayOrigin(rayOrigin), m_DragPulse);
        }

        void Rotate(Quaternion delta, Transform rayOrigin)
        {
            m_TargetRotation = delta * m_TargetRotation;

            this.Pulse(this.RequestNodeFromRayOrigin(rayOrigin), m_RotatePulse);
        }

        void Scale(Vector3 delta)
        {
            m_TargetScale += delta;
        }

        static void OnDragStarted()
        {
#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
#endif
        }

        void OnDragEnded(Transform rayOrigin)
        {
            this.ClearSnappingState(rayOrigin);
        }

        BaseManipulator CreateManipulator(GameObject prefab)
        {
            var go = EditorXRUtils.Instantiate(prefab, transform, active: false);
            foreach (var behavior in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                this.InjectFunctionalitySingle(behavior);
            }
            go.SetActive(false);
            var manipulator = go.GetComponent<BaseManipulator>();
            manipulator.translate = Translate;
            manipulator.rotate = Rotate;
            manipulator.scale = Scale;
            manipulator.dragStarted += OnDragStarted;
            manipulator.dragEnded += OnDragEnded;
            return manipulator;
        }

        void UpdateCurrentManipulator()
        {
            var selectionTransforms = Selection.transforms;
            if (selectionTransforms.Length <= 0)
                return;

            var manipulatorGameObject = m_CurrentManipulator.gameObject;
            manipulatorGameObject.SetActive(manipulatorVisible);

            m_SelectionBounds = BoundsUtils.GetBounds(selectionTransforms);

            var manipulatorTransform = manipulatorGameObject.transform;
            var activeTransform = Selection.activeTransform;
            if (activeTransform == null)
                activeTransform = selectionTransforms[0];

            manipulatorTransform.position = m_PivotMode == PivotMode.Pivot ? activeTransform.position : m_SelectionBounds.center;
            manipulatorTransform.rotation = m_PivotRotation == PivotRotation.Global && m_CurrentManipulator == m_StandardManipulator
                ? Quaternion.identity : activeTransform.rotation;
            m_TargetPosition = manipulatorTransform.position;
            m_TargetRotation = manipulatorTransform.rotation;
            m_StartRotation = m_TargetRotation;
            m_PositionOffsetRotation = Quaternion.identity;
            m_TargetScale = Vector3.one;

            // Save the initial position, rotation, and scale relative to the manipulator
            m_PositionOffsets.Clear();
            m_RotationOffsets.Clear();
            m_ScaleOffsets.Clear();

            foreach (var t in selectionTransforms)
            {
                m_PositionOffsets.Add(t, t.position - manipulatorTransform.position);
                m_ScaleOffsets.Add(t, t.localScale);
                m_RotationOffsets.Add(t, Quaternion.Inverse(manipulatorTransform.rotation) * t.rotation);
            }
        }

        public void OnResetDirectSelectionState()
        {
            m_DirectSelected = false;
        }

        bool TogglePivotMode()
        {
            m_PivotMode = m_PivotMode == PivotMode.Pivot ? PivotMode.Center : PivotMode.Pivot;
            UpdatePivotModeAction();
            UpdateCurrentManipulator();
            return true;
        }

        void UpdatePivotModeAction()
        {
            var isCenter = m_PivotMode == PivotMode.Center;
            m_PivotModeToggleAction.tooltipText = isCenter ? "Manipulator at Center" : "Manipulator at Pivot";
            m_PivotModeToggleAction.icon = isCenter ? m_OriginCenterIcon : m_OriginPivotIcon;
        }

        bool TogglePivotRotation()
        {
            m_PivotRotation = m_PivotRotation == PivotRotation.Global ? PivotRotation.Local : PivotRotation.Global;
            UpdatePivotRotationAction();
            UpdateCurrentManipulator();
            return true;
        }

        void UpdatePivotRotationAction()
        {
            var isGlobal = m_PivotRotation == PivotRotation.Global;
            m_PivotRotationToggleAction.tooltipText = isGlobal ? "Local Rotation" : "Global Rotation";
            m_PivotRotationToggleAction.icon = isGlobal ? m_RotationGlobalIcon : m_RotationLocalIcon;
        }

        bool ToggleManipulator()
        {
            m_CurrentManipulator.gameObject.SetActive(false);

            m_CurrentManipulator = m_CurrentManipulator == m_StandardManipulator ? m_ScaleManipulator : m_StandardManipulator;
            UpdateManipulatorAction();
            UpdateCurrentManipulator();
            return true;
        }

        void UpdateManipulatorAction()
        {
            var isStandard = m_CurrentManipulator == m_StandardManipulator;
            m_ManipulatorToggleAction.tooltipText = isStandard ? "Switch to Scale Manipulator" : "Switch to Standard Manipulator";
            m_ManipulatorToggleAction.icon = isStandard ? m_ScaleManipulatorIcon : m_StandardManipulatorIcon;
        }

        public bool IsTwoHandedScaling(Transform rayOrigin)
        {
            return m_Scaling && GrabDataForRayOrigin(rayOrigin) != null;
        }

        GrabData GrabDataForNode(Node node)
        {
            return node == Node.LeftHand ? m_LeftGrabData : m_RightGrabData;
        }

        GrabData GrabDataForRayOrigin(Transform rayOrigin)
        {
            if (m_LeftGrabData != null && m_LeftGrabData.rayOrigin == rayOrigin)
                return m_LeftGrabData;

            if (m_RightGrabData != null && m_RightGrabData.rayOrigin == rayOrigin)
                return m_RightGrabData;

            return null;
        }

        void ShowFeedback(List<ProxyFeedbackRequest> requests, string controlName, string tooltipText, Node node, bool suppressExisting = false)
        {
            List<VRInputDevice.VRControl> ids;
            if (m_Controls.TryGetValue(controlName, out ids))
            {
                foreach (var id in ids)
                {
                    var request = this.GetFeedbackRequestObject<ProxyFeedbackRequest>(this);
                    request.node = node;
                    request.control = id;
                    request.tooltipText = tooltipText;
                    request.priority = 1;
                    request.suppressExisting = suppressExisting;
                    requests.Add(request);
                    this.AddFeedbackRequest(request);
                }
            }
        }

        void ShowGrabFeedback(Node node)
        {
            ShowFeedback(m_GrabFeedback, "Cancel", "Cancel", node);
            ShowFeedback(m_GrabFeedback, "Select", null, node, true);
        }

        void ShowScaleFeedback(Node node)
        {
            ShowFeedback(m_ScaleFeedback, "Select", "Scale", node);
        }

        void ShowScaleOptionsFeedback(TwoHandedManipulateMode mode)
        {
            HideScaleOptionFeedback();
            switch (mode)
            {
                case TwoHandedManipulateMode.ScaleOnly:
                    ShowFeedback(m_ScaleOptionFeedback, "Cancel", "Press to Rotate and Scale", Node.LeftHand);
                    ShowFeedback(m_ScaleOptionFeedback, "Cancel", "Press to Cancel", Node.RightHand);
                    return;
                case TwoHandedManipulateMode.RotateAndScale:
                    ShowFeedback(m_ScaleOptionFeedback, "Cancel", "Press to Scale Only", Node.LeftHand);
                    ShowFeedback(m_ScaleOptionFeedback, "Cancel", "Press to Cancel", Node.RightHand);
                    return;
            }
        }

        void HideFeedback(List<ProxyFeedbackRequest> requests)
        {
            foreach (var request in requests)
            {
                this.RemoveFeedbackRequest(request);
            }
            requests.Clear();
        }

        void HideGrabFeedback()
        {
            HideFeedback(m_GrabFeedback);
        }

        void HideScaleFeedback()
        {
            HideFeedback(m_ScaleFeedback);
        }

        void HideScaleOptionFeedback()
        {
            HideFeedback(m_ScaleOptionFeedback);
        }

        void OnDestroy()
        {
            this.UnsubscribeFromResetDirectSelectionState(OnResetDirectSelectionState);

            if (m_ScaleManipulator)
                UnityObjectUtils.Destroy(m_ScaleManipulator.gameObject);

            if (m_StandardManipulator)
                UnityObjectUtils.Destroy(m_StandardManipulator.gameObject);
        }
    }
}
