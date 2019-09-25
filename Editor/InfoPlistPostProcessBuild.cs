#if UNITY_STANDALONE_OSX || UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;
using UnityEditor.Build.Reporting;

class InfoPlistPostProcessBuild : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 1; }
    }

    public void OnPostprocessBuild(BuildTarget target, string path)
    {
        // iOS and OSX share same info.plist entries to support Custom URI Schemes
        if (target == BuildTarget.StandaloneOSX || target == BuildTarget.iOS)
        {
            Debug.Log("InfoPlistPostProcessBuild.OnPostprocessBuild for target " + target + " at path " + path);
            var plistPath = string.Empty;
            if (target == BuildTarget.StandaloneOSX)
            {
                plistPath = $"{path}/Contents/Info.plist";
            }
            if (target == BuildTarget.iOS)
            {
                plistPath = $"{path}/Info.plist";
            }
            
            if (File.Exists(plistPath)) 
            {
                var plistDocument = new PlistDocument();
                plistDocument.ReadFromFile(plistPath);
                var rootDict = plistDocument.root;
                if (!rootDict.values.ContainsKey("CFBundleURLTypes"))
                {
                    // Create Custom URI Scheme entry
                    var urlTypeArray = new PlistElementArray();
                    var urlDict = urlTypeArray.AddDict();
                    var urlBundleName = new PlistElementString("Unity Reflect");
                    urlDict.values.Add("CFBundleURLName", urlBundleName);
                    var urlBundleSchemes = new PlistElementArray();
                    urlBundleSchemes.AddString("reflect");
                    urlDict.values.Add("CFBundleURLSchemes", urlBundleSchemes);
                    rootDict.values.Add("CFBundleURLTypes", urlTypeArray);

                    // Write back our changes to Info.plist
                    File.WriteAllText(plistPath, plistDocument.WriteToString());
                }
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
    }
}
#endif