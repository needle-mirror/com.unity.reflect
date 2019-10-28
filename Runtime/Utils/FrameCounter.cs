using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrameCounter : MonoBehaviour
{
    const string TEXT_FORMAT = "<color=#{0}>{1}</color>";

    public int FrameBufferCount = 30;
    public int TargetFrameRate = 60;

    public Text MaxFrameRateText;
    public Text CurrentFrameRateText;
    public Text MinFrameRateText;

    public Color LowColor;
    public Color MidColor;
    public Color HighColor;

    float[] m_FrameCounts;
    int m_CurrentIndex;
    int m_CurrentValidFrameCount;
    float m_CurrentFrameRate;
    float m_TotalFrameRate;
    float m_MinFrameRate;
    float m_MaxFrameRate;
    float m_FrameRateRatio;

    void Start()
    {
        m_FrameCounts = new float[FrameBufferCount];
        for (int i = 0; i < m_FrameCounts.Length; ++i)
        {
            m_FrameCounts[i] = -1;
        }
    }

    void Update()
    {
        m_FrameCounts[m_CurrentIndex] = 1f / Time.deltaTime;
        ++m_CurrentIndex;
        m_CurrentIndex %= m_FrameCounts.Length;

        Calculate();
        RefreshTexts();
    }

    void Calculate()
    {
        m_CurrentValidFrameCount = 0;
        m_TotalFrameRate = 0;
        m_MinFrameRate = float.MaxValue;
        m_MaxFrameRate = float.MinValue;
        for (int i = 0; i < m_FrameCounts.Length; ++i)
        {
            if (m_FrameCounts[i] >= 0)
            {
                ++m_CurrentValidFrameCount;
                m_TotalFrameRate += m_FrameCounts[i];

                m_MinFrameRate = Mathf.Min(m_MinFrameRate, m_FrameCounts[i]);
                m_MaxFrameRate = Mathf.Max(m_MaxFrameRate, m_FrameCounts[i]);
            }
        }
        m_CurrentFrameRate = m_TotalFrameRate / m_CurrentValidFrameCount;
    }

    Color GetLerpedColor(float frameRate)
    {
        m_FrameRateRatio = Mathf.Clamp01(frameRate / TargetFrameRate);
        if (m_FrameRateRatio <= 0.5f)
        {
            return Color.Lerp(LowColor, MidColor, m_FrameRateRatio * 2f);
        }
        else
        {
            return Color.Lerp(MidColor, HighColor, (m_FrameRateRatio - 0.5f) * 2f);
        }
    }

    string GetFormattedText(float frameRate)
    {
        return string.Format(TEXT_FORMAT, ColorUtility.ToHtmlStringRGB(GetLerpedColor(frameRate)), (int)frameRate);
    }

    void RefreshTexts()
    {
        CurrentFrameRateText.text = GetFormattedText(m_CurrentFrameRate);
        MaxFrameRateText.text = GetFormattedText(m_MaxFrameRate);
        MinFrameRateText.text = GetFormattedText(m_MinFrameRate);
    }
}
