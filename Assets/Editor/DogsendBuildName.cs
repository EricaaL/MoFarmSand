using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Names the Windows player Dogsend.exe whatever the build dialog's file name was. The product name stays MoFarmSand on purpose:
/// Application.persistentDataPath (where the save slots live) is built from it, so changing it would orphan existing saves.
/// </summary>
public class DogsendBuildName : IPostprocessBuildWithReport
{
    private const string ExeName = "Dogsend";

    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64 && report.summary.platform != BuildTarget.StandaloneWindows) return;

        string exePath = report.summary.outputPath;
        string folder = Path.GetDirectoryName(exePath);
        string oldName = Path.GetFileNameWithoutExtension(exePath);
        if (string.IsNullOrEmpty(folder) || oldName == ExeName) return;

        // Unity looks for "<exe name>_Data" next to the exe, so the exe and its folders are renamed together.
        Move(Path.Combine(folder, oldName + ".exe"), Path.Combine(folder, ExeName + ".exe"), false);
        Move(Path.Combine(folder, oldName + "_Data"), Path.Combine(folder, ExeName + "_Data"), true);
        Move(Path.Combine(folder, oldName + "_BurstDebugInformation_DoNotShip"), Path.Combine(folder, ExeName + "_BurstDebugInformation_DoNotShip"), true);
        Debug.Log($"Dogsend build: renamed {oldName}.exe to {ExeName}.exe in {folder}");
    }

    private static void Move(string from, string to, bool directory)
    {
        if (directory ? !Directory.Exists(from) : !File.Exists(from)) return;
        if (directory) { if (Directory.Exists(to)) Directory.Delete(to, true); Directory.Move(from, to); }
        else { if (File.Exists(to)) File.Delete(to); File.Move(from, to); }
    }
}
