﻿using System;
using UnityEngine;
using UnityEngine.InputNew;
using UnityEngine.XR;

#if UNITY_2018_3
using System.Text.RegularExpressions;
#endif

namespace UnityEditor.Experimental.EditorVR.Input
{
    abstract class BaseVRInputToEvents : BaseInputToEvents
    {
        protected virtual string DeviceName
        {
            get { return "Unknown VR Device"; }
        }

        const uint k_ControllerCount = 2;
        const int k_AxisCount = (int)VRInputDevice.VRControl.Analog9 + 1;
        const float k_DeadZone = 0.05f;

        float[,] m_LastAxisValues = new float[k_ControllerCount, k_AxisCount];
        Vector3[] m_LastPositionValues = new Vector3[k_ControllerCount];
        Quaternion[] m_LastRotationValues = new Quaternion[k_ControllerCount];
        static readonly VRInputDevice.VRControl[] k_Buttons =
        {
            VRInputDevice.VRControl.Action1,
            VRInputDevice.VRControl.Action2,
            VRInputDevice.VRControl.LeftStickButton
        };

        void Awake()
        {
#if UNITY_2018_3
            var match = Regex.Match(Application.unityVersion, @"([.](\d+))+");
            if (match.Groups.Count > 0)
            {
                int minor;
                var group = match.Groups[2];
                if (int.TryParse(group.Value, out minor) && minor < 12)
                    Debug.LogError("Unity 2018.3.12f1 or greater is required to have full input capabilities");
            }
#endif
        }


            public void Update()
        {
            var deviceActive = false;
            foreach (var device in UnityEngine.Input.GetJoystickNames())
            {
                if (device.IndexOf(DeviceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    deviceActive = true;
                    break;
                }
            }

            active = deviceActive;
            if (!active)
                return;

            for (VRInputDevice.Handedness hand = VRInputDevice.Handedness.Left;
                (int)hand <= (int)VRInputDevice.Handedness.Right;
                hand++)
            {
                int deviceIndex = hand == VRInputDevice.Handedness.Left ? 3 : 4;

                // TODO change 3 and 4 based on virtual devices defined in InputDeviceManager (using actual hardware available)
                SendButtonEvents(hand, deviceIndex);
                SendAxisEvents(hand, deviceIndex);
                SendTrackingEvents(hand, deviceIndex);
            }
        }

        bool GetAxis(VRInputDevice.Handedness hand, VRInputDevice.VRControl axis, out float value)
        {
            switch (axis)
            {
                case VRInputDevice.VRControl.Trigger1:
                    if (hand == VRInputDevice.Handedness.Left)
                        value = UnityEngine.Input.GetAxis("XRI_Left_Trigger");
                    else
                        value = UnityEngine.Input.GetAxis("XRI_Right_Trigger");
                    return true;
                case VRInputDevice.VRControl.Trigger2:
                    if (hand == VRInputDevice.Handedness.Left)
                        value = UnityEngine.Input.GetAxis("XRI_Left_Grip");
                    else
                        value = UnityEngine.Input.GetAxis("XRI_Right_Grip");
                    return true;
                case VRInputDevice.VRControl.LeftStickX:
                    if (hand == VRInputDevice.Handedness.Left)
                        value = UnityEngine.Input.GetAxis("XRI_Left_Primary2DAxis_Horizontal");
                    else
                        value = UnityEngine.Input.GetAxis("XRI_Right_Primary2DAxis_Horizontal");
                    return true;
                case VRInputDevice.VRControl.LeftStickY:
                    if (hand == VRInputDevice.Handedness.Left)
                        value = -1f * UnityEngine.Input.GetAxis("XRI_Left_Primary2DAxis_Vertical");
                    else
                        value = -1f * UnityEngine.Input.GetAxis("XRI_Right_Primary2DAxis_Vertical");
                    return true;
            }

            value = 0f;
            return false;
        }

        void SendAxisEvents(VRInputDevice.Handedness hand, int deviceIndex)
        {
            for (var axis = 0; axis < k_AxisCount; ++axis)
            {
                float value;
                if (GetAxis(hand, (VRInputDevice.VRControl)axis, out value))
                {
                    if (Mathf.Approximately(m_LastAxisValues[(int)hand, axis], value))
                        continue;

                    if (Mathf.Abs(value) < k_DeadZone)
                        value = 0;

                    var inputEvent = InputSystem.CreateEvent<GenericControlEvent>();
                    inputEvent.deviceType = typeof(VRInputDevice);
                    inputEvent.deviceIndex = deviceIndex;
                    inputEvent.controlIndex = axis;
                    inputEvent.value = value;

                    m_LastAxisValues[(int)hand, axis] = inputEvent.value;

                    InputSystem.QueueEvent(inputEvent);
                }
            }
        }

        protected virtual string GetButtonAxis(VRInputDevice.Handedness hand, VRInputDevice.VRControl button)
        {
            switch (button)
            {
                case VRInputDevice.VRControl.Action1:
                    if (hand == VRInputDevice.Handedness.Left)
                        return "XRI_Left_PrimaryButton";
                    else
                        return "XRI_Right_PrimaryButton";

                case VRInputDevice.VRControl.Action2:
                    if (hand == VRInputDevice.Handedness.Left)
                        return "XRI_Left_SecondaryButton";
                    else
                        return "XRI_Right_SecondaryButton";

                case VRInputDevice.VRControl.LeftStickButton:
                    if (hand == VRInputDevice.Handedness.Left)
                        return "XRI_Left_Primary2DAxisClick";
                    else
                        return "XRI_Right_Primary2DAxisClick";
            }

            // Not all buttons are currently mapped
            return null;
        }

        void SendButtonEvents(VRInputDevice.Handedness hand, int deviceIndex)
        {
            foreach (VRInputDevice.VRControl button in k_Buttons)
            {
                var axis = GetButtonAxis(hand, button);

                bool isDown = UnityEngine.Input.GetButtonDown(axis);
                bool isUp = UnityEngine.Input.GetButtonUp(axis);

                if (isDown || isUp)
                {
                    var inputEvent = InputSystem.CreateEvent<GenericControlEvent>();
                    inputEvent.deviceType = typeof(VRInputDevice);
                    inputEvent.deviceIndex = deviceIndex;
                    inputEvent.controlIndex = (int)button;
                    inputEvent.value = isDown ? 1.0f : 0.0f;

                    InputSystem.QueueEvent(inputEvent);
                }
            }
        }

        void SendTrackingEvents(VRInputDevice.Handedness hand, int deviceIndex)
        {
#pragma warning disable 618
            var node = hand == VRInputDevice.Handedness.Left ? XRNode.LeftHand : XRNode.RightHand;
            var localPosition = InputTracking.GetLocalPosition(node);
            var localRotation = InputTracking.GetLocalRotation(node);
#pragma warning restore 618

            if (localPosition == m_LastPositionValues[(int)hand] && localRotation == m_LastRotationValues[(int)hand])
                return;

            var inputEvent = InputSystem.CreateEvent<VREvent>();
            inputEvent.deviceType = typeof(VRInputDevice);
            inputEvent.deviceIndex = deviceIndex;
            inputEvent.localPosition = localPosition;
            inputEvent.localRotation = localRotation;

            m_LastPositionValues[(int)hand] = inputEvent.localPosition;
            m_LastRotationValues[(int)hand] = inputEvent.localRotation;

            InputSystem.QueueEvent(inputEvent);
        }
    }
}
