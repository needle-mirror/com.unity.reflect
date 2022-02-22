#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;

namespace UnityEngine.Reflect
{
    // Windows OS interop layer
    internal class Interop : IInteropable
    {

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int exitCode);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);

        [DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern IntPtr SetFocus(HandleRef hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static IntPtr m_MainWindow;
        static IntPtr m_PreviousMainWindowWndProcPtr = IntPtr.Zero;
        static IntPtr m_WndProcDelegatePtr;
        static WndProcDelegate m_WndProcDelegate;

        static LoginManager m_LoginManager;
        
        const uint WM_SETTEXT = 0x000C;
        const uint WM_CLOSE = 0x0010;
        const int SW_RESTORE = 9;

        static readonly string k_MainWindowTitle = Application.productName;
        static string m_InstanceId;
        static bool m_HasDelegate = false;
        
        internal Interop(LoginManager loginManager)
        {
            m_LoginManager = loginManager;
        }

        public void Start()
        {
            if (m_HasDelegate)
                return;

            m_InstanceId = Guid.NewGuid().ToString();
            m_MainWindow = AuthBackend.GetWindowHandle();
            m_WndProcDelegate = new WndProcDelegate(WndProc);
            m_WndProcDelegatePtr = Marshal.GetFunctionPointerForDelegate(m_WndProcDelegate);

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

        [MonoPInvokeCallback(typeof(WndProcDelegate))]
        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!m_HasDelegate)
                return IntPtr.Zero;

            if (msg == WM_CLOSE)
            {
                SetWindowLongPtr(m_MainWindow, -4, m_PreviousMainWindowWndProcPtr);
                PostQuitMessage(0);
                return IntPtr.Zero;
            }

            if (msg == WM_SETTEXT)
            {
                var message = Marshal.PtrToStringAnsi(lParam);
                if (UrlHelper.TryParseDeepLink(message, out var token, out var route, out var args, out var queryArgs))
                {
                    m_LoginManager.ProcessDeepLink(token, route, args, queryArgs);
                    BringViewerProcessToFront();
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(m_PreviousMainWindowWndProcPtr, hWnd, msg, wParam, lParam);
        }

        public void OnDisable() 
        {
            // We don't want to disable the WndProc as it will do a memory leak the way it's connected (through SetWindowLongPtr instead of using the "new" sub-classing mechanism)
        }
        
        public static void BringViewerProcessToFront()
        {
            var processHandle = AuthBackend.GetWindowHandle();
            if (IsIconic(processHandle))
            {
                ShowWindow(processHandle, SW_RESTORE);
            }
            SetForegroundWindow(processHandle);
            SetFocus(new HandleRef(null, processHandle));
        }

    }
}
#endif
