
using System;

namespace UnityEngine.Reflect
{
    public interface ISyncTask
    {
        event Action onSyncEnabled;
        event Action onSyncDisabled;
        event Action onSyncStopped;
        event Action onSyncStarted;

        event Action onSyncUpdateBegin;
        event Action<bool> onSyncUpdateEnd;

        void StartSync();        
        void StopSync();
    }
    
    public class SyncTopMenu : TopMenu
    {       
        [SerializeField]
        Color m_Color = Color.white;
        
        [SerializeField]
        Color m_DisableColor = Color.gray;
        
        [SerializeField]
        Color m_SyncingColor = new Color(0.09f, 0.94f, 0.45f);

        [SerializeField]
        float m_RotateSpeed = 300.0f;

        bool m_Animated;        
        bool m_Started;
        float m_TargetAngle = 0f;

        event Action onSyncStart;
        event Action onSyncStop;

        public void Register(ISyncTask syncTask)
        {
            syncTask.onSyncEnabled += OnSyncEnabled;
            syncTask.onSyncDisabled += OnSyncDisabled;

            syncTask.onSyncStopped += OnSyncStopped;
            syncTask.onSyncStarted += OnSyncStarted;

            syncTask.onSyncUpdateBegin += OnSyncUpdateBegin;
            syncTask.onSyncUpdateEnd += OnSyncUpdateEnd;

            onSyncStart += syncTask.StartSync;
            onSyncStop += syncTask.StopSync;
        }
        
        public void UnRegister(ISyncTask syncTask)
        {
            syncTask.onSyncEnabled -= OnSyncEnabled;
            syncTask.onSyncDisabled -= OnSyncDisabled;

            syncTask.onSyncUpdateBegin -= OnSyncUpdateBegin;
            syncTask.onSyncUpdateEnd -= OnSyncUpdateEnd;
            
            syncTask.onSyncStopped -= OnSyncStopped;
            syncTask.onSyncStarted -= OnSyncStarted;            

            onSyncStart -= syncTask.StartSync;
            onSyncStop -= syncTask.StopSync;
        }

        void OnSyncDisabled()
        {
            StopAnimation();

            button.interactable = false;
            button.image.color = m_DisableColor;
        }
        
        void OnSyncEnabled()
        {
            StopAnimation();

            button.interactable = true;
            button.image.color = m_Started ? m_SyncingColor : m_Color;
        }
        
        void OnSyncStarted()
        {
            m_Started = true;
            button.image.color = m_SyncingColor;            
        }        

        void OnSyncStopped()
        {
            StopAnimation();
            m_Started = false;
            button.image.color = m_Color;            
        }
        
        void OnSyncUpdateBegin()
        {
            StartAnimation();
        }
        
        void OnSyncUpdateEnd(bool hasChanged)
        {
            StopAnimation();
        }

        void StartAnimation()
        {
            m_Animated = true;
            m_TargetAngle = 0f;
        }

        void StopAnimation()
        {
            if (m_Animated)
            {
                m_Animated = false;
                m_TargetAngle = Mathf.Ceil(button.transform.rotation.eulerAngles.z / 180) * 180f;
            }
        }

        public override void OnClick()
        {
            if (m_Started)
            {
                onSyncStop?.Invoke();
            }
            else
            {
                onSyncStart?.Invoke();
            }
        }

        void OnEnable()
        {
            OnSyncDisabled();
        }

        void Update()
        {
            if (m_Animated)
            {
                button.transform.Rotate(Vector3.forward, Time.deltaTime * m_RotateSpeed);
            }
            else if (m_TargetAngle > 0f)
            {
                float diff = m_TargetAngle - button.transform.rotation.eulerAngles.z;
                if ((diff > 0f) && (diff < 180f))
                {
                    button.transform.Rotate(Vector3.forward, Time.deltaTime * m_RotateSpeed);
                }
                else
                {
                    m_TargetAngle = 0f;
                }
            }
        }
    }
}