using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public interface IDisplayMode
    {
        string Title { get; }
        string Description { get; }
        Sprite Image { get; }
        bool EnabledByDefault { get; }
        int MenuOrderPriority { get; }
        DisplayModeStatusParameters StatusParameters { get; }
        ListControlItemData ListControlItemData { get; }

        /// <summary>
        /// Used to determine if this display mode button is currently visible in the display mode menu
        /// </summary>
        bool IsAvailable { get; }

        void RefreshStatus();

        void OnModeEnabled(bool isEnabled, ListControlDataSource source);

        IEnumerator CheckAvailability();

        string GetStatusMessage();
    }
}
