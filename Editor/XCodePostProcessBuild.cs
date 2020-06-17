#if UNITY_IOS

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

class XCodePostProcessBuild : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnPostprocessBuild(BuildTarget target, string path)
    {
		if (target == BuildTarget.iOS)
		{
			Debug.Log("XCodePostProcessBuild.OnPostprocessBuild for target " + target + " at path " + path);

			//	edit project file
			var projectPath = path + "/Unity-iPhone.xcodeproj/project.pbxproj";
			PBXProject pbxProject = new PBXProject();
			pbxProject.ReadFromFile(projectPath);
			string targetGuid = pbxProject.GetUnityFrameworkTargetGuid(); 
			string frameworkGuid = pbxProject.GetUnityFrameworkTargetGuid(); 

			//  disable bitcode because the gRPC library does not have bitcode
			//  remove when using a gRPC library containing bitcode
			pbxProject.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
			pbxProject.SetBuildProperty(frameworkGuid, "ENABLE_BITCODE", "NO");

			//  include libz to support gRPC compression
			pbxProject.AddFrameworkToProject(frameworkGuid, "libz.tbd", false);

			//  include Safari Framework to support embedded Safari Login/Logout
			pbxProject.AddFrameworkToProject(frameworkGuid, "SafariServices.framework", false);

			pbxProject.WriteToFile(projectPath);
			
			
			//	edit plist file
			string plistPath = path + "/Info.plist";
			PlistDocument plist = new PlistDocument();
			plist.ReadFromString(File.ReadAllText(plistPath));
			PlistElementDict rootDict = plist.root;

			// 	remove exit on suspend if it exists
			string exitsOnSuspendKey = "UIApplicationExitsOnSuspend";
			if (rootDict.values.ContainsKey(exitsOnSuspendKey))
			{
				rootDict.values.Remove(exitsOnSuspendKey);
			}

			File.WriteAllText(plistPath, plist.WriteToString());
		}
	}

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
    }
}
#endif