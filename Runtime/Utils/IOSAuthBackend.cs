#if UNITY_IOS && !UNITY_EDITOR && PIPELINE_API
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[DllImport("__Internal")]
private static extern void UnityDeeplinks_init(string gameObject = null, string deeplinkMethod = null);
[DllImport("__Internal")]
extern static void LaunchSafariWebViewUrl(string url);
[DllImport("__Internal")]
extern static void DismissSafariWebView();


public class IOSAuthBackend : IAuthenticatable
{
}

#endif
