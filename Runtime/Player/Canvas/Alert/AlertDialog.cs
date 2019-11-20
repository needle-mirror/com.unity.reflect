using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class AlertDialog : MonoBehaviour
    {
        public class ButtonOptions
        {
            public Action onClick;
            public string text;
        }

#pragma warning disable 0649
        [SerializeField]
        GameObject m_ui;

        [SerializeField]
        Text m_ErrorMessage;

        [SerializeField]
        Text m_ButtonText;
#pragma warning restore 0649

        ButtonOptions m_ButtonOptions;

        public void Show(string errorMessage, ButtonOptions buttonOptions)
        {
            m_ButtonOptions = buttonOptions;
            m_ErrorMessage.text = errorMessage;
            m_ButtonText.text = m_ButtonOptions.text;
            m_ui.SetActive(true);
        }

        public void ButtonClick()
        {
            m_ButtonOptions.onClick();
        }

        public void Close()
        {
            m_ui.SetActive(false);
        }
    }
}
