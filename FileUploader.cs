using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Text;


public class FileUploader : EditorWindow
{
    //1
    private string folderPath = "";
    private List<string> allFiles = new List<string>();
    private List<string> selectedFiles = new List<string>();
    private HashSet<string> readonlyFiles = new HashSet<string>();
    private string commitMessage = "";
    private Vector2 scrollPosition = Vector2.zero;
    private bool isSelectAll = false;

    private string currentBranch = "";
    private List<string> availableBranches = new List<string>();
    private string selectedBranch = "";

    //缓存字典
    private Dictionary<string, bool> fileModificationCache = new Dictionary<string, bool>();

    private Dictionary<string, bool> fileTrackedCache = new Dictionary<string, bool>();


    [MenuItem("GIT/提交文件")]
    public static void ShowWindow()
    {
        FileUploader window = GetWindow<FileUploader>("Git提交文件");
        if (window != null)
        {
            window.LoadBranches();
        }
    }


    private void OnEnable()
    {

        folderPath = EditorPrefs.GetString("GitUploader_FolderPath", string.Empty);
    }


    private void SaveFolderPath(string path)
    {
        folderPath = path;

        EditorPrefs.SetString("GitUploader_FolderPath", folderPath);
    }

    private void OnGUI()
    {

        GUILayout.BeginHorizontal();
        GUILayout.Label("将文件夹中的文件上传到 Git 仓库", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("刷新", GUILayout.Width(70), GUILayout.Height(30)))
        {
            if (!string.IsNullOrEmpty(folderPath))
            {

                LoadFiles(folderPath);

                LoadBranches();

                Repaint();
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(1);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fixedHeight = 30,
        };

        if (GUILayout.Button("选择文件夹", buttonStyle))
        {
            string selectedFolder = EditorUtility.OpenFolderPanel("选择包含文件的文件夹", folderPath, "");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                SaveFolderPath(selectedFolder);
                LoadFiles(selectedFolder);
            }
        }

        GUILayout.Label("已选择的文件夹路径：");
        GUILayout.TextField(folderPath);

        GUILayout.Label("文件列表：");
        if (allFiles.Count > 0)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            for (int i = 0; i < allFiles.Count; i++)
            {
                string file = allFiles[i];
                bool isSelected = selectedFiles.Contains(file);
                bool isReadonly = readonlyFiles.Contains(file);

                bool isModified = fileModificationCache.ContainsKey(file) && fileModificationCache[file];

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(isReadonly);
                bool toggle = GUILayout.Toggle(isSelected, Path.GetFileName(file));
                EditorGUI.EndDisabledGroup();

                if (isReadonly)
                {
                    GUI.contentColor = Color.green;
                    GUILayout.Label("(已提交)", GUILayout.Width(300));
                    GUI.contentColor = Color.white;
                }

                if (isModified)
                {
                    GUI.contentColor = new Color(1f, 1f, 0f);
                    GUILayout.Label("(待推送)", GUILayout.Width(300));
                    GUI.contentColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(15);
                if (toggle != isSelected)
                {
                    if (toggle)
                    {
                        selectedFiles.Add(file);
                        string metaFile = file + ".meta";
                        if (File.Exists(metaFile) && !selectedFiles.Contains(metaFile))
                        {
                            selectedFiles.Add(metaFile);
                        }
                    }
                    else
                    {
                        selectedFiles.Remove(file);
                        string metaFile = file + ".meta";
                        if (File.Exists(metaFile))
                        {
                            selectedFiles.Remove(metaFile);
                        }
                    }
                    GUILayout.Space(10);
                }
            }

            GUILayout.EndScrollView();
        }

        GUILayout.Space(3);

        bool previousSelectAll = isSelectAll;
        isSelectAll = GUILayout.Toggle(isSelectAll, "全选");
        if (isSelectAll != previousSelectAll)
        {
            if (isSelectAll)
            {
                selectedFiles.Clear();
                foreach (var file in allFiles)
                {
                    if (!readonlyFiles.Contains(file))
                    {
                        selectedFiles.Add(file);
                        string metaFile = file + ".meta";
                        if (File.Exists(metaFile) && !selectedFiles.Contains(metaFile))
                        {
                            selectedFiles.Add(metaFile);
                        }
                    }
                }
            }
            else
            {
                selectedFiles.Clear();
            }
        }

        GUILayout.Space(5);

        GUILayout.Label("提交信息：");
        commitMessage = EditorGUILayout.TextArea(commitMessage, GUILayout.Height(50), GUILayout.Width(400));

        DisplayCurrentBranch(currentBranch);

        GUILayout.Label("切换分支：", EditorStyles.boldLabel);

        int branchIndex = availableBranches.IndexOf(selectedBranch);
        int newBranchIndex = EditorGUILayout.Popup(branchIndex, availableBranches.ToArray(), GUILayout.Width(200));
        if (newBranchIndex != branchIndex)
        {
            selectedBranch = availableBranches[newBranchIndex];
            SwitchBranch(selectedBranch);
        }

        GUILayout.Space(30);

        if (GUILayout.Button("提交", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(folderPath) || selectedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请选择一个有效的文件夹并选择要上传的文件。", "确定");
            }
            else
            {
                UploadSelectedFilesToGit(selectedFiles, commitMessage);
            }
        }
    }

    private void DisplayCurrentBranch(string branch)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            //加粗
            fontStyle = FontStyle.Bold,
            //字体
            fontSize = 14,
            //字体颜色
            normal = { textColor = Color.green }
        };

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        GUILayout.Label("---当前分支：", style);
        GUILayout.Label(branch, style);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
    }
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    private async void LoadFiles(string folderPath)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new CancellationTokenSource();
        CancellationToken token = cancellationTokenSource.Token;

        allFiles.Clear();
        selectedFiles.Clear();
        readonlyFiles.Clear();
        fileModificationCache.Clear();
        fileTrackedCache.Clear();

        try
        {
            string[] files = await Task.Run(() => Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories));

            List<string> allFilesCopy = new List<string>();

            foreach (string file in files)
            {
                if (!file.EndsWith(".meta"))
                {
                    allFilesCopy.Add(file);
                }
            }

            int batchSize = 50;
            int totalFiles = allFilesCopy.Count;
            UnityEngine.Debug.Log($"总文件数量: {totalFiles}");

            string projectPath = Application.dataPath.Replace("/Assets", "");

            for (int i = 0; i < totalFiles; i += batchSize)
            {
                if (token.IsCancellationRequested)
                {
                    UnityEngine.Debug.Log("文件加载任务已被取消。");
                    return;
                }

                var batchFiles = allFilesCopy.Skip(i).Take(batchSize);

                foreach (string file in batchFiles)
                {
                    if (token.IsCancellationRequested)
                    {
                        UnityEngine.Debug.Log("文件加载任务已被取消。");
                        return;
                    }

                    string relativePath = GetRelativeAssetPath(file);

                    bool isTracked = await Task.Run(() => IsFileTrackedByGit(relativePath, projectPath));
                    bool isModified = await Task.Run(() => IsFileModified(relativePath, projectPath));

                    fileTrackedCache[file] = isTracked;
                    fileModificationCache[file] = isModified;

                    if (isTracked && !isModified)
                    {
                        readonlyFiles.Add(file);
                    }
                    else if (isTracked && isModified)
                    {
                        selectedFiles.Add(file);
                    }

                    allFiles.Add(file);
                }

                UnityEngine.Debug.Log($"已加载文件数量: {allFiles.Count}");

                await Task.Yield();
            }

            CheckAndRemoveDeletedFiles(folderPath);
        }
        catch (OperationCanceledException)
        {
            UnityEngine.Debug.Log("文件加载任务被取消。");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log($"加载文件时出错: {ex.Message}");
        }
        finally
        {
            Repaint();
        }
    }

    private void OnDestroy()
    {
        
        cancellationTokenSource?.Cancel();
    }

    private void CheckAndRemoveDeletedFiles(string folderPath)
    {
        string projectPath = Application.dataPath.Replace("/Assets", ""); 
        try
        {
            string trackedFilesOutput = RunGitCommand("ls-files", projectPath);
            UnityEngine.Debug.Log($"Git 返回的受控文件列表: {trackedFilesOutput}"); 
            string[] trackedFiles = trackedFilesOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            List<string> deletedFiles = new List<string>();

            foreach (string relativePath in trackedFiles)
            {
                UnityEngine.Debug.Log($"Git 受控文件相对路径: {relativePath}");

                string cleanedRelativePath = CleanPath(relativePath);

                string absolutePath = Path.Combine(projectPath, cleanedRelativePath);

                UnityEngine.Debug.Log($"检查文件路径: {absolutePath}");

                if (!File.Exists(absolutePath))
                {
                    deletedFiles.Add(relativePath);
                    UnityEngine.Debug.Log($"本地已删除文件: {relativePath}");
                }
            }

            if (deletedFiles.Count > 0)
            {
                string formattedFileList = string.Join("\n", deletedFiles.Select(file => DecodeUnicode(file)));

                string message = "以下文件已在本地删除，但仍在 Git 中:\n" +
                                 formattedFileList +
                                 "\n是否从 Git 仓库中移除这些文件？";

                bool confirm = EditorUtility.DisplayDialog("检测到已删除文件", message, "移除", "取消");

                if (confirm)
                {
                    RemoveFilesFromGit(deletedFiles, projectPath);
                }
            }
            else
            {
                UnityEngine.Debug.Log("未检测到本地已删除的文件。");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"检测已删除文件失败: {ex.Message}");
            UnityEngine.Debug.LogError($"异常堆栈: {ex.StackTrace}");
        }
    }

    private string DecodeUnicode(string input)
    {
        UnityEngine.Debug.Log($"原始路径：{input}"); 

        string decodedString = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\\u([0-9A-Fa-f]{4})",
            match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString()
        );

        UnityEngine.Debug.Log($"解码后的路径：{decodedString}");  
        return decodedString;
    }
    private string CleanPath(string path)
    {

        path = path.Trim();

        char[] invalidChars = Path.GetInvalidPathChars();
        foreach (var ch in invalidChars)
        {
            path = path.Replace(ch.ToString(), string.Empty);
        }

        return path;
    }

    private void RemoveFilesFromGit(List<string> files, string workingDirectory)
    {
        UnityEngine.Debug.Log($"Attempting to remove {files.Count} files from Git repository at {workingDirectory}");

        try
        {
            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file))
                {
                    UnityEngine.Debug.LogWarning("Encountered an empty or null file path. Skipping.");
                    continue;
                }

                UnityEngine.Debug.Log($"Trying to remove file: {file}");

                RunGitCommand($"rm \"{file}\"", workingDirectory);

                string metaFile = file + ".meta";
                string metaFileFullPath = Path.Combine(workingDirectory, metaFile);
                UnityEngine.Debug.Log($"Checking for .meta file: {metaFileFullPath}");

                if (File.Exists(metaFileFullPath))
                {
                    UnityEngine.Debug.Log($"Found .meta file: {metaFileFullPath}. Removing...");
                    RunGitCommand($"rm \"{metaFile}\"", workingDirectory);
                }
                else
                {
                    UnityEngine.Debug.Log($"No .meta file found for {file}. Skipping...");
                }
            }

            UnityEngine.Debug.Log("Committing changes...");
            RunGitCommand("commit -m \"移除本地已删除的文件\"", workingDirectory);

            UnityEngine.Debug.Log("Pushing changes...");
            RunGitCommand("push", workingDirectory);

            UnityEngine.Debug.Log("已成功从 Git 仓库移除本地删除的文件。");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"移除文件失败: {ex.Message}");
        }
    }


    private bool IsFileTrackedByGit(string relativePath, string workingDirectory)
    {
        if (fileTrackedCache.ContainsKey(relativePath))
        {
            return fileTrackedCache[relativePath];
        }

        try
        {
            string output = RunGitCommand($"ls-files \"{relativePath}\"", workingDirectory);
            bool isTracked = !string.IsNullOrEmpty(output);
            fileTrackedCache[relativePath] = isTracked;
            return isTracked;
        }
        catch
        {
            return false;
        }
    }

    private bool IsFileModified(string filePath, string workingDirectory)
    {
        if (fileModificationCache.ContainsKey(filePath))
        {
            return fileModificationCache[filePath];
        }

        try
        {
            string output = RunGitCommand($"diff --name-only \"{filePath}\"", workingDirectory);
            bool isModified = !string.IsNullOrEmpty(output);
            fileModificationCache[filePath] = isModified;
            return isModified;
        }
        catch
        {
            return false;
        }
    }

    private void UploadSelectedFilesToGit(List<string> files, string commitMessage)
    {
        try
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            EditorUtility.DisplayProgressBar("上传到 Git", "正在检查文件状态...", 0.1f);

            foreach (string file in files)
            {
                string relativePath = GetRelativeAssetPath(file);

                bool isModified = IsFileModified(relativePath, projectPath);
                bool isTracked = IsFileTrackedByGit(relativePath, projectPath);

                if (isModified || !isTracked)
                {
                    RunGitCommand($"add \"{relativePath}\"", projectPath);
                }
            }

            EditorUtility.DisplayProgressBar("上传到 Git", "正在提交更改到 Git...", 0.6f);
            RunGitCommand($"commit -m \"{commitMessage}\"", projectPath);

            EditorUtility.DisplayProgressBar("上传到 Git", "正在推送到远程仓库...", 0.9f);
            RunGitCommand("push", projectPath);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("成功", $"已成功上传 {files.Count} 个文件到 Git 仓库。", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            UnityEngine.Debug.LogError($"Git 上传失败: {ex.Message}");
            EditorUtility.DisplayDialog("错误", $"Git 上传失败: {ex.Message}", "确定");

            foreach (string file in files)
            {
                try
                {
                    string relativePath = GetRelativeAssetPath(file);
                    string projectPath = Application.dataPath.Replace("/Assets", "");

                    RunGitCommand($"rm --cached \"{relativePath}\"", projectPath);

                    if (readonlyFiles.Contains(file))
                    {
                        readonlyFiles.Remove(file);
                    }
                }
                catch (System.Exception rmEx)
                {
                    UnityEngine.Debug.LogError($"无法移除文件: {file}, 错误: {rmEx.Message}");
                }
            }
        }
    }

    private string GetRelativeAssetPath(string filePath)
    {
        string assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);
        return assetPath.Replace("\\", "/");
    }

    private void LoadBranches()
    {
        availableBranches.Clear();
        try
        {
            string currentBranchOutput = RunGitCommand("rev-parse --abbrev-ref HEAD", Application.dataPath);
            currentBranch = currentBranchOutput.Trim();

            string branchesOutput = RunGitCommand("branch", Application.dataPath);
            availableBranches = new List<string>(branchesOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            availableBranches.RemoveAll(branch => string.IsNullOrWhiteSpace(branch));

            if (string.IsNullOrEmpty(currentBranch))
            {
                UnityEngine.Debug.LogError("获取当前分支失败，输出为空或无效");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"加载分支失败: {ex.Message}");
        }
    }

    private void SwitchBranch(string branchName)
    {
        try
        {
            RunGitCommand($"checkout {branchName}", Application.dataPath);
            currentBranch = branchName;

            LoadBranches();
            Repaint();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"切换分支失败: {ex.Message}");
        }
    }

    private string RunGitCommand(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c git {command}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,  
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception(error);
            }
            return output.Trim();
        }
    }

}
