using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Linq;


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

    //缓存
    private Dictionary<string, bool> fileModificationCache = new Dictionary<string, bool>();

    //记录文件是否被跟踪
    private Dictionary<string, bool> fileTrackedCache = new Dictionary<string, bool>();


    [MenuItem("GIT/Git提交文件")]
    public static void ShowWindow()
    {
        FileUploader window = GetWindow<FileUploader>("Git提交文件");
        if (window != null)
        {
            window.LoadBranches();
        }
    }

    // 加载面板时读取保存的文件夹路径
    private void OnEnable()
    {
        //从 EditorPrefs 加载已保存的文件夹路径
        folderPath = EditorPrefs.GetString("GitUploader_FolderPath", string.Empty);
    }

    //保存选择的文件夹路径
    private void SaveFolderPath(string path)
    {
        folderPath = path;
        //将文件夹路径保存在EditorPrefs
        EditorPrefs.SetString("GitUploader_FolderPath", folderPath);
    }

    private void OnGUI()
    {
        // 刷新按钮
        GUILayout.BeginHorizontal();
        GUILayout.Label("将文件夹中的文件上传到 Git 仓库", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("刷新", GUILayout.Width(70), GUILayout.Height(30)))
        {
            if (!string.IsNullOrEmpty(folderPath))
            {
                // 刷新文件列表
                LoadFiles(folderPath);
                // 刷新分支信息
                LoadBranches();
                // 强制刷新列表
                Repaint();
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(1);

        // 按钮样式
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fixedHeight = 30,
        };

        // 选择文件夹
        if (GUILayout.Button("选择文件夹", buttonStyle))
        {
            // 打开文件夹选择对话框，使用上次选择的路径作为默认路径
            string selectedFolder = EditorUtility.OpenFolderPanel("选择包含文件的文件夹", folderPath, "");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                SaveFolderPath(selectedFolder);
                LoadFiles(selectedFolder);
            }
        }

        // 显示选择的文件夹路径，始终显示在窗口上方，除非重新选择
        GUILayout.Label("已选择的文件夹路径：");
        GUILayout.TextField(folderPath);

        // 文件夹中的文件列表
        GUILayout.Label("文件夹中的文件：");
        if (allFiles.Count > 0)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            for (int i = 0; i < allFiles.Count; i++)
            {
                string file = allFiles[i];
                bool isSelected = selectedFiles.Contains(file);
                bool isReadonly = readonlyFiles.Contains(file);

                // 从缓存中读取文件修改状态
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

                // 显示待推送状态
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

        // 上边距
        GUILayout.Space(3);

        // 全选功能
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

        // 上边距
        GUILayout.Space(5);

        // 提交信息输入框
        GUILayout.Label("提交信息：");
        commitMessage = GUILayout.TextField(commitMessage);

        // 当前分支显示
        DisplayCurrentBranch(currentBranch);

        // 下拉框
        GUILayout.Label("切换分支：", EditorStyles.boldLabel);

        int branchIndex = availableBranches.IndexOf(selectedBranch);
        int newBranchIndex = EditorGUILayout.Popup(branchIndex, availableBranches.ToArray(), GUILayout.Width(200));
        if (newBranchIndex != branchIndex)
        {
            selectedBranch = availableBranches[newBranchIndex];
            SwitchBranch(selectedBranch);
        }

        // 上边距
        GUILayout.Space(30);

        // 提交按钮
        if (GUILayout.Button("提交"))
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

    //当前分支
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
        //上边距
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        GUILayout.Label("---当前分支：", style);
        GUILayout.Label(branch, style);
        GUILayout.EndHorizontal();

        //下边距
        GUILayout.Space(10);
    }
    private async void LoadFiles(string folderPath)
    {
        // 清空现有数据
        allFiles.Clear();
        selectedFiles.Clear();
        readonlyFiles.Clear();
        fileModificationCache.Clear();
        fileTrackedCache.Clear();

        try
        {
            // 异步加载文件
            string[] files = await Task.Run(() => Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories));

            // 使用副本来避免修改 allFiles 时出错
            List<string> allFilesCopy = new List<string>();

            foreach (string file in files)
            {
                if (!file.EndsWith(".meta"))
                {
                    allFilesCopy.Add(file);
                }
            }

            // 将 allFilesCopy 的内容添加回 allFiles
            allFiles.AddRange(allFilesCopy);

            UnityEngine.Debug.Log($"文件列表中的文件数量: {allFiles.Count}");

            // 检测已删除的文件
            CheckAndRemoveDeletedFiles(folderPath);

            string projectPath = Application.dataPath.Replace("/Assets", "");

            // 处理文件跟踪和修改状态
            foreach (string file in allFilesCopy) // 使用副本进行迭代
            {
                string relativePath = GetRelativeAssetPath(file);

                // 文件是否被跟踪
                bool isTracked = await Task.Run(() => IsFileTrackedByGit(relativePath, projectPath));
                bool isModified = await Task.Run(() => IsFileModified(relativePath, projectPath));

                // 缓存状态
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
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log($"加载文件时出错: {ex.Message}");
        }
        finally
        {
            // 刷新界面
            Repaint();
        }
    }

    //检测本地已删除文件并提示移除
    private void CheckAndRemoveDeletedFiles(string folderPath)
    {
        string projectPath = Application.dataPath.Replace("/Assets", ""); // 获取项目的根目录路径
        try
        {
            // 获取 Git 仓库中受版本控制的文件列表
            string trackedFilesOutput = RunGitCommand("ls-files", projectPath);
            UnityEngine.Debug.Log($"Git 返回的受控文件列表: {trackedFilesOutput}"); // 输出调试信息
            string[] trackedFiles = trackedFilesOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            List<string> deletedFiles = new List<string>();

            foreach (string relativePath in trackedFiles)
            {
                // 输出调试信息
                UnityEngine.Debug.Log($"Git 受控文件相对路径: {relativePath}");

                // 清理路径
                string cleanedRelativePath = CleanPath(relativePath);

                // 组合绝对路径
                string absolutePath = Path.Combine(projectPath, cleanedRelativePath);

                // 输出组合后的路径用于调试
                UnityEngine.Debug.Log($"检查文件路径: {absolutePath}");

                // 检查文件是否存在
                if (!File.Exists(absolutePath))
                {
                    deletedFiles.Add(relativePath);
                    UnityEngine.Debug.Log($"本地已删除文件: {relativePath}");
                }
            }

            if (deletedFiles.Count > 0)
            {
                // 使用 string.Join 格式化文件列表并确保正确显示中文
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

    /// <summary>
    /// 解码 Unicode 字符串为可读文本
    /// </summary>
    private string DecodeUnicode(string input)
    {
        UnityEngine.Debug.Log($"原始路径：{input}");  // 输出原始路径调试

        // 解码 Unicode 字符串为可读文本
        string decodedString = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\\u([0-9A-Fa-f]{4})",
            match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString()
        );

        UnityEngine.Debug.Log($"解码后的路径：{decodedString}");  // 输出解码后的路径调试
        return decodedString;
    }
    private string CleanPath(string path)
    {
        // 移除多余的空白字符
        path = path.Trim();

        //如果路径包含非法字符，进行替换
        char[] invalidChars = Path.GetInvalidPathChars();
        foreach (var ch in invalidChars)
        {
            path = path.Replace(ch.ToString(), string.Empty);
        }

        return path;
    }

    //移除git仓库中文件
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

                // 移除文件
                RunGitCommand($"rm \"{file}\"", workingDirectory);

                // 对应的 .meta 文件一起移除
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

            // 提交更改
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


    //判断文件是否被跟踪
    private bool IsFileTrackedByGit(string relativePath, string workingDirectory)
    {
        if (fileTrackedCache.ContainsKey(relativePath))
        {
            //从缓存中获取跟踪状态
            return fileTrackedCache[relativePath];
        }

        try
        {
            string output = RunGitCommand($"ls-files \"{relativePath}\"", workingDirectory);
            bool isTracked = !string.IsNullOrEmpty(output);
            //缓存结果
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
            //从缓存中读取修改状态
            return fileModificationCache[filePath];
        }

        try
        {
            string output = RunGitCommand($"diff --name-only \"{filePath}\"", workingDirectory);
            bool isModified = !string.IsNullOrEmpty(output);
            //缓存结果
            fileModificationCache[filePath] = isModified;
            return isModified;
        }
        catch
        {
            return false;
        }
    }

    //上传文件
    private void UploadSelectedFilesToGit(List<string> files, string commitMessage)
    {
        try
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            //进度条
            EditorUtility.DisplayProgressBar("上传到 Git", "正在检查文件状态...", 0.1f);

            foreach (string file in files)
            {
                string relativePath = GetRelativeAssetPath(file);

                bool isModified = IsFileModified(relativePath, projectPath);
                bool isTracked = IsFileTrackedByGit(relativePath, projectPath);

                if (isModified || !isTracked)
                {
                    //添加到暂存区
                    RunGitCommand($"add \"{relativePath}\"", projectPath);
                }
            }

            //提交更改
            EditorUtility.DisplayProgressBar("上传到 Git", "正在提交更改到 Git...", 0.6f);
            RunGitCommand($"commit -m \"{commitMessage}\"", projectPath);

            //推送更改到远程仓库
            EditorUtility.DisplayProgressBar("上传到 Git", "正在推送到远程仓库...", 0.9f);
            RunGitCommand("push", projectPath);

            //清除进度条并提示成功
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("成功", $"已成功上传 {files.Count} 个文件到 Git 仓库。", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            UnityEngine.Debug.LogError($"Git 上传失败: {ex.Message}");
            EditorUtility.DisplayDialog("错误", $"Git 上传失败: {ex.Message}", "确定");

            //处理失败的文件
            foreach (string file in files)
            {
                try
                {
                    string relativePath = GetRelativeAssetPath(file);
                    string projectPath = Application.dataPath.Replace("/Assets", "");

                    //删除文件的暂存状态
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

    //获取相对路径
    private string GetRelativeAssetPath(string filePath)
    {
        string assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);
        return assetPath.Replace("\\", "/");
    }

    private void LoadBranches()
    {
        availableBranches.Clear();  //清空缓存
        try
        {
            // 获取当前分支
            string currentBranchOutput = RunGitCommand("rev-parse --abbrev-ref HEAD", Application.dataPath);
            currentBranch = currentBranchOutput.Trim();

            //获取所有分支列表
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
            // 更新分支列表并刷新 UI
            LoadBranches();
            Repaint();  // 强制刷新界面
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"切换分支失败: {ex.Message}");
        }
    }

    //git命令
    private string RunGitCommand(string command, string workingDirectory)
    {
        //设置编码为 UTF-8
        //System.Diagnostics.Process.Start("cmd.exe", "/C chcp 65001 && " + command);

        ProcessStartInfo startInfo = new ProcessStartInfo("git")
        {
            Arguments = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception(process.StandardError.ReadToEnd());
            }
            return output.Trim();
        }
    }
}