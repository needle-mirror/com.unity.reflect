using System;
using UnityEngine;
using UnityEngine.UI;

public class Dialog : MonoBehaviour
{
#pragma warning disable 0649
    
    [SerializeField]
    Text m_Title;
    
    [SerializeField]
    Text m_Message;
    
    [SerializeField]
    GameObject m_Ui;

    [Space]
    
    [SerializeField]
    GameObject m_DoubleButton;
    
    [SerializeField]
    GameObject m_SingleButton;

    [Space]

    [SerializeField]
    Image m_Background;
    
    [Space]

    [SerializeField]
    Color m_ErrorBackgroundColor;

#pragma warning restore 0649
    
    Action m_Confirmed;
    Action m_Cancelled;

    Color m_NormalColor;

    void OnEnable()
    {
        m_NormalColor = m_Background.color;
    }

    public void Show(string title, string message, Action confirmed = null, Action cancelled = null)
    {
        m_Title.text = title;
        m_Message.text = message;
        m_Confirmed = confirmed;
        m_Cancelled = cancelled;

        m_Background.color = m_NormalColor;
        m_DoubleButton.SetActive(true);
        m_SingleButton.SetActive(false);
        m_Ui.SetActive(true);
    }
    
    public void ShowError(string title, string message)
    {
        m_Title.text = title;
        m_Message.text = message;

        m_Background.color = m_ErrorBackgroundColor;
        m_DoubleButton.SetActive(false);
        m_SingleButton.SetActive(true);
        m_Ui.SetActive(true);
    }

    public void Confirm()
    {
        m_Confirmed?.Invoke();
        Close();
    }

    public void Cancel()
    {
        m_Cancelled?.Invoke();
        Close();
    }

    void Close()
    {
        m_Ui.SetActive(false);
        m_Confirmed = null;
        m_Cancelled = null;
    }
}
