﻿namespace UnityEditor.Experimental.EditorVR.Input
{
    sealed class OVRTouchInputToEvents : BaseVRInputToEvents
    {
        protected override string DeviceName
        {
            get { return "Oculus Touch Controller"; }
        }
    }
}
