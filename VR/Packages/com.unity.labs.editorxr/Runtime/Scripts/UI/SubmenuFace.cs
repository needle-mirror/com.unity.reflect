using System;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Helpers;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Menus
{
    class SubmenuFace : MonoBehaviour, IUsesControlHaptics, IRayToNode
    {
#pragma warning disable 649
        [SerializeField]
        MainMenuButton m_BackButton;

        [SerializeField]
        HapticPulse m_ButtonClickPulse;

        [SerializeField]
        HapticPulse m_ButtonHoverPulse;
#pragma warning restore 649

        public GradientPair gradientPair { get; set; }

#if !FI_AUTOFILL
        IProvidesControlHaptics IFunctionalitySubscriber<IProvidesControlHaptics>.provider { get; set; }
#endif

        public void SetupBackButton(Action<Transform> backAction)
        {
            m_BackButton.hovered += OnButtonHovered;
            m_BackButton.clicked += OnButtonClicked;
            m_BackButton.clicked += backAction;
        }

        void OnButtonClicked(Transform rayOrigin)
        {
            this.Pulse(this.RequestNodeFromRayOrigin(rayOrigin), m_ButtonClickPulse);
        }

        void OnButtonHovered(Transform rayOrigin, Type buttonType, string buttonDescription)
        {
            this.Pulse(this.RequestNodeFromRayOrigin(rayOrigin), m_ButtonHoverPulse);
        }
    }
}
