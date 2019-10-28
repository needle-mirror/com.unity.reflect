using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Splash : MonoBehaviour
{
    public GameObject m_Background;
    public Image m_UnityLogo;
    public GameObject m_ReflectLogo;
    public float m_PhaseInDeltaTime = 0.6f;
    public float m_ShowReflectTime = 1.9f;
    public float m_HideSplashTime = 3.5f;
    public float m_StopTime = 5f;
    
    private float m_Start;
    private AudioSource m_AudioSource;
    public UnityEvent OnSplashScreenComplete;

    void Start()
    {
        m_Start = Time.time;
        
        m_AudioSource = GetComponent<AudioSource>();
        
        m_Background?.SetActive(true);
        if (m_UnityLogo != null)
        {
            m_UnityLogo.color = new Color(1, 1, 1, 0);
            m_UnityLogo.gameObject.SetActive(true);
        }
        m_ReflectLogo?.SetActive(false);
    }

    void Update()
    {
        float delta = m_AudioSource.isPlaying ? m_AudioSource.time : (Time.time - m_Start);

        //    Unity fade in
        var color = m_UnityLogo.color;
        if (color.a < 1f)
        {
            color.a = Mathf.Lerp(0f, 1f, delta * m_PhaseInDeltaTime);
            m_UnityLogo.color = color;
        }

        if (delta > m_StopTime)
        {
            //    release resources
            m_AudioSource.clip = null;
            if (m_Background.activeSelf)
            {
                OnSplashScreenComplete?.Invoke();
            }
            gameObject.SetActive(false);
        }
        else if (delta > m_HideSplashTime || Input.touchCount > 0 || Input.anyKey)
        {
            //    hide splash screen
            if (m_Background.activeSelf)
            {
                m_Background.SetActive(false);
                m_UnityLogo.gameObject.SetActive(false);
                m_ReflectLogo.SetActive(false);
                OnSplashScreenComplete?.Invoke();
            }
        }
        else if (!m_ReflectLogo.activeSelf && delta > m_ShowReflectTime)
        {
            //    show Reflect
            if (m_Background.activeSelf && !m_ReflectLogo.activeSelf)
            {
                m_ReflectLogo.SetActive(true);
            }
        }
    }
}
