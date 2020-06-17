#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UnityEngine.Reflect
{
    internal class WindowsStandaloneInterop : IInteropable
    {

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(HandleRef hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        IntPtr m_MainWindow;
        IntPtr m_PreviousMainWindowWndProcPtr = IntPtr.Zero;
        IntPtr m_WndProcDelegatePtr;
        WndProcDelegate m_WndProcDelegate;
        
        const uint WM_SETTEXT = 0x000C;

        readonly string k_MainWindowTitle = Application.productName;
        string m_InstanceId;
        bool m_HasDelegate = false;

        public void Start()
        {
            if (m_HasDelegate) return;

            m_InstanceId = Guid.NewGuid().ToString();
            m_MainWindow = LoginResolver.GetWindowHandle();
            m_WndProcDelegate = new WndProcDelegate(wndProc);
            m_WndProcDelegatePtr = Marshal.GetFunctionPointerForDelegate(m_WndProcDelegate);
            // Depending on arch, we will replace the window ptr for wndProc
            try 
            {
                m_PreviousMainWindowWndProcPtr = SetWindowLong(m_MainWindow, -4, m_WndProcDelegatePtr);
                m_HasDelegate = true;
            }
            catch (Exception longex)
            {
                Debug.Log($"set wndProc long exception: {longex}");
            }
            if (!m_HasDelegate)
            {
                try
                {
                    m_PreviousMainWindowWndProcPtr = SetWindowLongPtr(m_MainWindow, -4, m_WndProcDelegatePtr);
                    m_HasDelegate = true;
                }
                catch (Exception longptrex)
                {
                    Debug.Log($"set wndProc longPtr exception: {longptrex}");
                }
            }
        }

        IntPtr wndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!m_HasDelegate) 
            {
                return IntPtr.Zero;
            }
            if (msg == WM_SETTEXT)
            {
                var message = Marshal.PtrToStringAnsi(lParam);
                if (message.StartsWith("OPEN_PROJECT_REQUEST"))
                {
                    var request = message.Split(' ');
                    // TODO handle the request
                    Debug.Log($"open projectid {request[2]} request using token: {request[1]}");
                    return IntPtr.Zero;
                }
            }
            return CallWindowProc(m_PreviousMainWindowWndProcPtr, hWnd, msg, wParam, lParam);
        }

        public void OnDisable() 
        {
            if (!m_HasDelegate) return;
        }
    }
}
#endif
