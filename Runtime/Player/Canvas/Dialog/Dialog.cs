using System;
using UnityEngine;
using UnityEngine.UI;

public class Dialog : MonoBehaviour
{
    public Text m_title;
    public Text m_message;
    public GameObject m_ui;

    Action m_confirmed;
    Action m_cancelled;

    public void Show(string title, string message, Action confirmed = null, Action cancelled = null)
    {
        m_title.text = title;
        m_message.text = message;
        m_confirmed = confirmed;
        m_cancelled = cancelled;
        m_ui.SetActive(true);
    }

    public void Confirm()
    {
        m_confirmed?.Invoke();
        Close();
    }

    public void Cancel()
    {
        m_cancelled?.Invoke();
        Close();
    }

    void Close()
    {
        m_ui.SetActive(false);
        m_confirmed = null;
        m_cancelled = null;
    }
}
