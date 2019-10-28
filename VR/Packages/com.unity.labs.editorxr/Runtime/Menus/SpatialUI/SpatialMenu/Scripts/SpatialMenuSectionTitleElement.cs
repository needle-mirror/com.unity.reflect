﻿using System;
using System.Collections;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR
{
    sealed class SpatialMenuSectionTitleElement : SpatialMenuElement
    {
#pragma warning disable 649
        [Header("Haptic Pulses")]
        [SerializeField]
        HapticPulse m_HighlightPulse;
#pragma warning restore 649

        Coroutine m_VisibilityCoroutine;
        Vector3 m_TextOriginalLocalPosition;
        bool m_Highlighted;
        bool m_Visible;

        public override bool visible
        {
            get { return m_Visible; }
            set
            {
                if (m_Visible == value)
                    return;

                m_Visible = value;

                if (m_CanvasGroup != null)
                    this.RestartCoroutine(ref m_VisibilityCoroutine, AnimateVisibility(m_Visible));
            }
        }

        public override bool highlighted
        {
            get { return m_Highlighted; }
            set
            {
                if (m_Highlighted == value)
                    return;

                m_Highlighted = value;
                parentMenuData.highlighted = value;

                this.RestartCoroutine(ref m_VisibilityCoroutine, AnimateHighlight(m_Highlighted));

                if (m_Highlighted)
                    this.Pulse(Node.None, m_HighlightPulse);

                var action = highlightedAction;
                if (action != null)
                    action(parentMenuData);
            }
        }

        void Awake()
        {
            Setup = SetupInternal;
            m_Button.onClick.AddListener(Select);
        }

        void OnDestroy()
        {
            m_Button.onClick.RemoveAllListeners();
        }

        void Select()
        {
            var beingHoveredByRay = hoveringNode != Node.None;
            var selectionNode = beingHoveredByRay ? hoveringNode : spatialMenuActiveControllerNode;

            // Force this element to be selected again when switching menu levels
            // due to the ray taking precedence over a non-ray-based highlighted item
            // The button will be cleaned up regardless on menu level change or close
            if (beingHoveredByRay)
                highlighted = true;

            if (selected != null)
                selected(selectionNode);
        }

        public void SetupInternal(Transform parentTransform, Action selectedAction, String displayedText = null, string toolTipText = null)
        {
            if (selectedAction == null)
            {
                UnityObjectUtils.Destroy(gameObject);
                return;
            }

            transform.SetParent(parentTransform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            m_Text.text = displayedText;

            if (Mathf.Approximately(m_TransitionDuration, 0f))
                m_TransitionDuration = 0.001f;
        }

        void OnEnable()
        {
            // Caching position here, as layout groups were altering the position when originally caching in Start()
            m_TextOriginalLocalPosition = m_Text.transform.localPosition;
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        IEnumerator AnimateVisibility(bool fadeIn)
        {
            var currentAlpha = fadeIn ? 0f : m_CanvasGroup.alpha;
            var targetAlpha = fadeIn ? 1f : 0f;
            var alphaTransitionAmount = 0f;
            var textTransform = m_Text.transform;
            var textCurrentLocalPosition = textTransform.localPosition;
            textCurrentLocalPosition = fadeIn ? new Vector3(m_TextOriginalLocalPosition.x, m_TextOriginalLocalPosition.y, m_FadeInZOffset) : textCurrentLocalPosition;
            var textTargetLocalPosition = m_TextOriginalLocalPosition;
            var positionTransitionAmount = 0f;
            var transitionSubtractMultiplier = 1f / m_TransitionDuration;
            while (alphaTransitionAmount < 1f)
            {
                var alphaSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(alphaTransitionAmount);
                var positionSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(positionTransitionAmount);
                m_CanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaSmoothTransition);
                textTransform.localPosition = Vector3.Lerp(textCurrentLocalPosition, textTargetLocalPosition, positionSmoothTransition);
                alphaTransitionAmount += Time.deltaTime * transitionSubtractMultiplier;
                positionTransitionAmount += alphaTransitionAmount * 1.35f;
                yield return null;
            }

            textTransform.localPosition = textTargetLocalPosition;
            m_CanvasGroup.alpha = targetAlpha;
            m_VisibilityCoroutine = null;
        }

        IEnumerator AnimateHighlight(bool isHighlighted)
        {
            const float targetAlpha = 1f;
            var currentAlpha = m_CanvasGroup.alpha;
            var alphaTransitionAmount = 0f;
            var textTransform = m_Text.transform;
            var textCurrentLocalPosition = textTransform.localPosition;
            var textTargetLocalPosition = isHighlighted ? new Vector3(m_TextOriginalLocalPosition.x, m_TextOriginalLocalPosition.y, m_HighlightedZOffset) : m_TextOriginalLocalPosition;
            var positionTransitionAmount = 0f;
            var currentTextLocalScale = textTransform.localScale;
            var targetTextLocalScale = isHighlighted ? Vector3.one * 1.15f : Vector3.one;
            var speedMultiplier = isHighlighted ? 8f : 4f;
            while (alphaTransitionAmount < 1f)
            {
                var alphaSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(alphaTransitionAmount);
                var positionSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(positionTransitionAmount);
                m_CanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaSmoothTransition);
                textTransform.localPosition = Vector3.Lerp(textCurrentLocalPosition, textTargetLocalPosition, positionSmoothTransition);
                textTransform.localScale = Vector3.Lerp(currentTextLocalScale, targetTextLocalScale, alphaSmoothTransition);
                alphaTransitionAmount += Time.deltaTime * speedMultiplier;
                positionTransitionAmount += alphaTransitionAmount * 1.35f; // slightly faster position transition
                yield return null;
            }

            textTransform.localPosition = textTargetLocalPosition;
            textTransform.localScale = targetTextLocalScale;
            m_CanvasGroup.alpha = targetAlpha;
            m_VisibilityCoroutine = null;
        }
    }
}
