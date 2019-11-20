using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class TMPChecker : MonoBehaviour
{
    void Start()
    {
#if UNITY_EDITOR
        if (!Directory.Exists("Assets/TextMesh Pro"))
        {
            TMP_PackageResourceImporterWindow.ShowPackageImporterWindow();
        }
#endif
    }
}
