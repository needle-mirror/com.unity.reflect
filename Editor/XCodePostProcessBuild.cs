#if UNITY_IOS

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

			var projectPath = path + "/Unity-iPhone.xcodeproj/project.pbxproj";
			PBXProject pbxProject = new PBXProject();
			pbxProject.ReadFromFile(projectPath);
			string targetGuid = pbxProject.TargetGuidByName("Unity-iPhone");

			//  disable bitcode because the gRPC library does not have bitcode
			//  remove when using a gRPC library containing bitcode
			pbxProject.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

			//  include libz to support gRPC compression
			pbxProject.AddFrameworkToProject(targetGuid, "libz.tbd", false);

			pbxProject.WriteToFile(projectPath);
		}
	}

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
    }
}
#endif