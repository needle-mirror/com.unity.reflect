using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class BIMParameterUI : MonoBehaviour
    {
        [SerializeField] protected Text m_TitleText;
        [SerializeField] protected Text m_ValueText;

        protected RectTransform m_RectTransform;

        protected void Awake()
        {
            m_RectTransform = transform as RectTransform;
        }

        protected void Update()
        {
            m_TitleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_RectTransform.sizeDelta.x / 2f);
            m_ValueText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_RectTransform.sizeDelta.x / 2f);
        }

        public void Init(string title, string value)
        {
            m_TitleText.text = title;
            m_ValueText.text = value;
        }
    }
}
