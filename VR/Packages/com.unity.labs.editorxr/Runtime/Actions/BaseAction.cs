﻿using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Actions
{
    /// <summary>
    /// A convenience class for simple action implementations
    /// </summary>
    public abstract class BaseAction : MonoBehaviour, IAction
    {
#pragma warning disable 649
        [SerializeField]
        Sprite m_Icon;
#pragma warning restore 649

        public Sprite icon
        {
            get { return m_Icon; }
        }

        public abstract void ExecuteAction();
    }
}
