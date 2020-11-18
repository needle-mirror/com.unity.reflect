#if UNITY_STANDALONE_WIN
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEngine;
using System;

public partial class WindowsBuildPostProcess : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target == BuildTarget.StandaloneWindows64)
        {
            Debug.Log($"WindowsBuildPostProcess.OnPostprocessBuild");
            var lastFolderIndex = path.LastIndexOf("/");

            // UX: Reuse the .exe name of the reflect application being built
            var exeAppName = path.Substring(lastFolderIndex + 1);

            var interopDirectory = $"{path.Substring(0, lastFolderIndex)}/Unity_Reflect_Interop";
            var tokenResolverDestinationFilePath = $"{interopDirectory}/{exeAppName}";

            if (!Directory.Exists(interopDirectory)) 
            {
                Directory.CreateDirectory(interopDirectory);
            }

            // Write executable from hex string value
            byte[] hexArray = Convert.FromBase64String(hexFile);
            using (var fileStream = new FileStream(tokenResolverDestinationFilePath, FileMode.Create))
            using (var binaryWriter = new BinaryWriter(fileStream))
            {
                binaryWriter.Write(hexArray);
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
    }
}
#endif