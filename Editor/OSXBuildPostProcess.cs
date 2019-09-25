#if UNITY_STANDALONE_OSX
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;
using System.Diagnostics;

class OSXBuildPostProcess : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 2; }
    }

    public void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("OSXBuildPostProcess.OnPostprocessBuild for target " + target + " at path " + path);

            var xcodeFrameworkOutput = Path.GetFullPath("Packages/com.unity.reflect/Plugins/osx~/ReflectCustomUri.framework");

            var viewerMachOBinary = $"\"{Path.Combine(path, "Contents", "MacOS", Application.productName)}\"";
            var xcodeFrameworkDestination = Path.Combine(path, "Contents", "Frameworks", "ReflectCustomUri.framework");

            var xcodeFrameworkInjectScript = Path.GetFullPath("Packages/com.unity.reflect/Plugins/osx~/InjectFramework.sh");
            var xcodeFrameworkInjectExecutable = Path.GetFullPath("Packages/com.unity.reflect/Plugins/osx~/InjectFramework.command");

            // Create destination directory
            if(!Directory.Exists(xcodeFrameworkDestination)) 
            {
                Directory.CreateDirectory(xcodeFrameworkDestination);
            }

            if(Directory.Exists(xcodeFrameworkOutput) && Directory.Exists(xcodeFrameworkDestination)) 
            {
                DirectoryCopy(xcodeFrameworkOutput, xcodeFrameworkDestination, true);
                
                if (File.Exists(xcodeFrameworkInjectExecutable))
                {
                    File.Delete(xcodeFrameworkInjectExecutable);
                }

                if (File.Exists(xcodeFrameworkInjectScript))
                {
                    UnityEngine.Debug.Log($"Injecting ReflectCustomUri.Framework in {viewerMachOBinary}");
                    
                    File.Copy(xcodeFrameworkInjectScript, xcodeFrameworkInjectExecutable);

                    var injectProcessInfo = new ProcessStartInfo();
                    injectProcessInfo.FileName = xcodeFrameworkInjectExecutable;
                    injectProcessInfo.UseShellExecute = false;
                    injectProcessInfo.RedirectStandardOutput = true;
                    injectProcessInfo.Arguments = viewerMachOBinary; 
                    var injectProcess = Process.Start(injectProcessInfo);
                    var injectOutput = injectProcess.StandardOutput.ReadToEnd();
                    injectProcess.WaitForExit();
                    UnityEngine.Debug.Log($"xcodeFrameworkInjectExecutable output: '{injectOutput}'");

                    File.Delete(xcodeFrameworkInjectExecutable);
                }
            }

        }
    }


    void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // Get the subdirectories for the specified directory.
        var dir = new DirectoryInfo(sourceDirName);

        DirectoryInfo[] dirs = dir.GetDirectories();
        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (var file in files)
        {
            //Do not copy symbolic kinks
            if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint)) 
            {
                var copyPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(copyPath, false);
            }
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (copySubDirs)
        {
            foreach (var subdir in dirs)
            {
                string subDirectoryPath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, subDirectoryPath, copySubDirs);
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
    }
}
#endif
