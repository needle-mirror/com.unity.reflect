using System.Collections;

namespace UnityEngine.Reflect
{
    public class ScreenModeBehaviour : BaseDisplayModeBehaviour
    {
        public override bool IsAvailable => true;

        protected override void Start()
        {
            base.Start();

            SyncManager syncManager = FindObjectOfType<SyncManager>();
            if (m_UIManager != null && syncManager != null)
            {
                m_UIManager.FreeCamController.Target = syncManager.syncRoot.position;
            }
        }

        public override string GetStatusMessage()
        {
            // no status needed, always available
            return string.Empty;
        }

        public override void OnModeEnabled(bool isEnabled, ListControlDataSource source)
        {
            base.OnModeEnabled(isEnabled, source);

            if (isEnabled)
            {
                TopMenu.ShowButtons();
            }
            else
            {
                TopMenu.HideButtons();
            }
        }
    }
}
