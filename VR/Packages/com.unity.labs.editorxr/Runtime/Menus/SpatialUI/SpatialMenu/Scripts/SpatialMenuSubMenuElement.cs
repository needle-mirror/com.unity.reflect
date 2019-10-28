﻿using System;
using System.Collections;
using TMPro;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.Experimental.EditorVR
{
    sealed class SpatialMenuSubMenuElement : SpatialMenuElement
    {
#pragma warning disable 649
        [SerializeField]
        Image m_BackgroundImage;

        [Header("Borders")]
        [SerializeField]
        CanvasGroup m_BordersCanvasGroup;

        [SerializeField]
        RectTransform m_TopBorder;

        [SerializeField]
        RectTransform m_BottomBorder;

        [Header("Tooltip Text")]
        [SerializeField]
        CanvasGroup m_TooltipVisualsCanvasGroup;

        [SerializeField]
        TextMeshProUGUI m_TooltipText;

        [SerializeField]
        float m_ExpandedTooltipHeight = 52f;

        [SerializeField]
        float m_TooltipTransitionDuration = 1f;

        [Header("Haptic Pulses")]
        [SerializeField]
        HapticPulse m_HighlightPulse;

        [SerializeField]
        HapticPulse m_TooltipDisplayPulse;
#pragma warning restore 649

        RectTransform m_RectTransform;
        Vector2 m_OriginalSize;
        Vector2 m_ExpandedTooltipDisplaySize;
        Coroutine m_VisibilityCoroutine;
        Coroutine m_TooltipVisualsVisibilityCoroutine;
        Vector3 m_TextOriginalLocalPosition;
        bool m_Highlighted;
        Vector3 m_OriginalBordersLocalScale;
        float m_BordersOriginalAlpha;
        Color m_OriginalBackgroundColor;
        bool m_Visible;

        public Action selectedAction { get; set; }

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
            }
        }

        void Awake()
        {
            Setup = SetupInternal;
            m_OriginalBackgroundColor = m_BackgroundImage.color;

            m_Button.onClick.AddListener(Select);
        }

        void OnEnable()
        {
            // Cacheing position here, as layout groups were altering the position when originally cacheing in Start()
            m_TextOriginalLocalPosition = m_Text.transform.localPosition;

            if (m_TopBorder != null && m_BottomBorder != null)
            {
                m_OriginalBordersLocalScale = m_TopBorder.localScale;
                m_BordersOriginalAlpha = m_BordersCanvasGroup.alpha;
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        void OnDestroy()
        {
            m_Button.onClick.RemoveAllListeners();
        }

        void Select()
        {
            var selectionNode = hoveringNode != Node.None ? hoveringNode : spatialMenuActiveControllerNode;
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

            this.selectedAction = selectedAction;
            m_RectTransform = (RectTransform)transform;
            m_OriginalSize = m_RectTransform.sizeDelta;
            m_ExpandedTooltipDisplaySize = new Vector2(m_RectTransform.sizeDelta.x, m_ExpandedTooltipHeight);

            m_Icon.gameObject.SetActive(false);
            m_Text.gameObject.SetActive(true);
            m_Text.text = displayedText;

            transform.SetParent(parentTransform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;

            if (Mathf.Approximately(m_TransitionDuration, 0f))
                m_TransitionDuration = 0.001f;

            // Tooltip text related
            m_TooltipVisualsCanvasGroup.alpha = 0;
            m_TooltipText.text = toolTipText;
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

            if (!fadeIn)
                UnityObjectUtils.Destroy(gameObject); // TODO: pool
        }

        IEnumerator AnimateHighlight(bool isHighlighted)
        {
            this.RestartCoroutine(ref m_TooltipVisualsVisibilityCoroutine, AnimateTooltipVisualsVisibility(isHighlighted));

            var currentAlpha = m_CanvasGroup.alpha;
            var targetAlpha = 1f;
            var alphaTransitionAmount = 0f;
            var textTransform = m_Text.transform;
            var textCurrentLocalPosition = textTransform.localPosition;
            var textTargetLocalPosition = isHighlighted ? new Vector3(m_TextOriginalLocalPosition.x, m_TextOriginalLocalPosition.y, m_HighlightedZOffset) : m_TextOriginalLocalPosition;
            var positionTransitionAmount = 0f;
            var currentTextLocalScale = textTransform.localScale;
            var targetTextLocalScale = isHighlighted ? Vector3.one * 1.15f : Vector3.one;
            var currentBackgroundColor = m_BackgroundImage.color;
            var targetBackgroundColor = isHighlighted ? Color.black : m_OriginalBackgroundColor;
            var currentBordersLocalScale = m_TopBorder.localScale;
            var targetBordersLocalScale = isHighlighted ? new Vector3 (m_OriginalBordersLocalScale.x * 0.65f, m_OriginalBordersLocalScale.y * 8, m_OriginalBordersLocalScale.z) : m_OriginalBordersLocalScale;
            var currentBordersCanvasGroupAlpha = m_BordersCanvasGroup.alpha;
            var targetBordersCanvasGroupAlpha = isHighlighted ? 1f : m_BordersOriginalAlpha;
            var speedMultiplier = isHighlighted ? 3f : 6f;
            while (alphaTransitionAmount < 1f)
            {
                var alphaSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(alphaTransitionAmount);
                var positionSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(positionTransitionAmount);
                m_CanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaSmoothTransition * 1.5f);
                textTransform.localPosition = Vector3.Lerp(textCurrentLocalPosition, textTargetLocalPosition, positionSmoothTransition);
                textTransform.localScale = Vector3.Lerp(currentTextLocalScale, targetTextLocalScale, alphaSmoothTransition);
                alphaTransitionAmount += Time.deltaTime * speedMultiplier;
                positionTransitionAmount += alphaTransitionAmount * 1.35f;
                m_BackgroundImage.color = Color.Lerp(currentBackgroundColor, targetBackgroundColor, alphaSmoothTransition * 4);
                m_TopBorder.localScale = Vector3.Lerp(currentBordersLocalScale, targetBordersLocalScale, alphaSmoothTransition);
                m_BottomBorder.localScale = Vector3.Lerp(currentBordersLocalScale, targetBordersLocalScale, alphaSmoothTransition);
                m_BordersCanvasGroup.alpha = Mathf.Lerp(currentBordersCanvasGroupAlpha, targetBordersCanvasGroupAlpha, alphaSmoothTransition);
                yield return null;
            }

            m_BordersCanvasGroup.alpha = targetBordersCanvasGroupAlpha;
            textTransform.localPosition = textTargetLocalPosition;
            textTransform.localScale = targetTextLocalScale;
            m_BackgroundImage.color = targetBackgroundColor;
            m_CanvasGroup.alpha = targetAlpha;
            m_TopBorder.localScale = targetBordersLocalScale;
            m_BottomBorder.localScale = targetBordersLocalScale;
            m_VisibilityCoroutine = null;
        }

        IEnumerator AnimateTooltipVisualsVisibility(bool fadeIn)
        {
            var initialWaitBeforeDisplayDuration = fadeIn ? 1f : 0f;
            var currentAlpha = fadeIn ? 0f : m_TooltipVisualsCanvasGroup.alpha;
            var targetAlpha = fadeIn ? 1f : 0f;
            var alphaTransitionAmount = 0f;
            var currentSize = m_RectTransform.sizeDelta;
            var targetSize = fadeIn ? m_ExpandedTooltipDisplaySize : m_OriginalSize;
            var sizeTransitionAmount = 0f;
            var transitionDuration = fadeIn ? m_TooltipTransitionDuration : m_TooltipTransitionDuration * 0.2f; // faster fade out
            var transitionMultiplier = 1f / transitionDuration;
            while (initialWaitBeforeDisplayDuration > 0f)
            {
                initialWaitBeforeDisplayDuration -= Time.unscaledDeltaTime;
                yield return null;
            }

            if(fadeIn)
                this.Pulse(Node.None, m_TooltipDisplayPulse);

            var currentBordersLocalScale = m_TopBorder.localScale;
            while (alphaTransitionAmount < 1f)
            {
                var alphaSmoothTransition = MathUtilsExt.SmoothInOutLerpFloat(alphaTransitionAmount);
                var newBorderLocalScale = Vector3.Lerp(currentBordersLocalScale, m_OriginalBordersLocalScale, alphaSmoothTransition);
                m_TopBorder.localScale = newBorderLocalScale;
                m_BottomBorder.localScale = newBorderLocalScale;
                m_TooltipVisualsCanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaSmoothTransition);
                m_RectTransform.sizeDelta = Vector2.Lerp(currentSize, targetSize, sizeTransitionAmount);
                alphaTransitionAmount += Time.deltaTime * transitionMultiplier;
                sizeTransitionAmount += alphaTransitionAmount * 1.35f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_TooltipVisualsCanvasGroup.transform.parent as RectTransform);
                yield return null;
            }

            m_TooltipVisualsCanvasGroup.alpha = targetAlpha;
            m_RectTransform.sizeDelta = targetSize;
            m_TopBorder.localScale = m_OriginalBordersLocalScale;
            m_BottomBorder.localScale = m_OriginalBordersLocalScale;

            m_TooltipVisualsVisibilityCoroutine = null;
        }
    }
}
