using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

public class GitPuller : EditorWindow
{
    private string folderPath = Directory.GetCurrentDirectory();
    private string currentBranch = "";
    private List<string> availableBranches = new List<string>();
    private string selectedBranch = "";
    private Vector2 scrollPosition = Vector2.zero;
    private List<string> repositoryFiles = new List<string>();
    private HashSet<string> selectedFiles = new HashSet<string>();

    [MenuItem("GIT/Git拉取文件")]
    public static void ShowWindow()
    {
        GitPuller window = GetWindow<GitPuller>("Git拉取文件");
        if (window != null)
        {
            window.LoadBranches();
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        // 当前分支
        DisplayCurrentBranch(currentBranch);
        // 刷新按钮
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("刷新", GUILayout.Width(70), GUILayout.Height(30)))
        {
            LoadBranches();
            LoadFilesToPull();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 下拉框选择分支
        GUILayout.Label("切换分支：", EditorStyles.boldLabel);
        int branchIndex = availableBranches.IndexOf(selectedBranch);
        int newBranchIndex = EditorGUILayout.Popup(branchIndex, availableBranches.ToArray(), GUILayout.Width(200));
        if (newBranchIndex != branchIndex)
        {
            selectedBranch = availableBranches[newBranchIndex];
            // 切换分支
            SwitchBranch(selectedBranch);
        }

        GUILayout.Space(10);

        // 显示将要拉取的文件
        if (repositoryFiles.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("远端与本地不一致的文件：", EditorStyles.boldLabel);

            //滚动视图高度
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            var filteredFiles = repositoryFiles.Where(file => !file.EndsWith(".meta")).ToList();
            foreach (var file in filteredFiles)
            {
                // 显示文件名
                GUILayout.Label(file);
            }
            EditorGUILayout.EndScrollView();
        }

        GUILayout.Space(30);

        // 拉取按钮
        if (GUILayout.Button("拉取所有文件"))
        {
            // 拉取所有文件
            PullAllFilesFromGit();
        }
    }

    private void LoadFilesToPull()
    {
        try
        {
            // 拉取最新的分支信息
            RunGitCommand("fetch", folderPath);

            // 获取本地与远程分支差异的文件
            string diff = RunGitCommand($"diff origin/{currentBranch} --name-only", folderPath);
            List<string> changedFiles = new List<string>(diff.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            changedFiles = changedFiles.Select(file => file.Trim()).ToList();

            repositoryFiles = changedFiles;

        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"加载将要拉取的文件列表失败: {ex.Message}");
        }
    }

    private void DisplayCurrentBranch(string branch)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14,
            normal = { textColor = Color.green }
        };

        GUILayout.BeginHorizontal();
        GUILayout.Label("--当前分支：", style);
        GUILayout.Label(branch, style);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
    }

    private void SwitchBranch(string branchName)
    {
        try
        {
            if (string.IsNullOrEmpty(branchName))
            {
                UnityEngine.Debug.LogError("分支名称为空，无法切换分支。");
                return;
            }

            RunGitCommand("fetch", folderPath);

            string branches = RunGitCommand("branch -a", folderPath);
            bool isRemoteBranch = branches.Contains($"remotes/origin/{branchName}");
            bool isLocalBranch = branches.Contains(branchName);

            if (!isLocalBranch && !isRemoteBranch)
            {
                UnityEngine.Debug.LogError($"分支 '{branchName}' 不存在。");
                return;
            }

            UnityEngine.Debug.Log($"正在切换到分支：{branchName}");

            if (isRemoteBranch && !isLocalBranch)
            {
                RunGitCommand($"checkout -b {branchName} origin/{branchName}", folderPath);
            }
            else if (isLocalBranch)
            {
                RunGitCommand($"checkout {branchName}", folderPath);
            }

            currentBranch = branchName;
            selectedBranch = branchName;

            UnityEngine.Debug.Log($"成功切换到分支：{branchName}");

            LoadBranches();
            EditorApplication.delayCall += Repaint;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"切换分支失败: {ex.Message}");
        }
    }

    private void LoadBranches()
    {
        try
        {
            currentBranch = RunGitCommand("rev-parse --abbrev-ref HEAD", folderPath);
            UnityEngine.Debug.Log("当前分支: " + currentBranch);

            string branches = RunGitCommand("branch -a", folderPath);
            availableBranches = new List<string>(branches.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            availableBranches.RemoveAll(branch => string.IsNullOrWhiteSpace(branch));

            if (availableBranches.Count > 0)
            {
                selectedBranch = availableBranches[0];
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"加载分支失败: {ex.Message}");
        }
    }
    private void PullAllFilesFromGit()
    {
        try
        {
            EditorUtility.DisplayProgressBar("拉取文件", "正在拉取远程仓库代码...", 0.5f);

            //检查本地修改并判断本地与远程分歧
            string status = RunGitCommand("status --porcelain", folderPath);
            UnityEngine.Debug.Log("Git Status Output:\n" + status);

            var conflictedFiles = GetConflictedFiles(status);
            UnityEngine.Debug.Log("Conflicted Files:\n" + string.Join("\n", conflictedFiles));

            //获取本地和远程分支差异
            string diff = RunGitCommand($"diff origin/{currentBranch} --name-only", folderPath);
            List<string> changedFiles = new List<string>(diff.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            changedFiles = changedFiles.Select(file => file.Trim()).ToList();

            //检测是否存在分支分歧
            bool hasDivergence = status.Contains("diverged") || changedFiles.Count > 0;

            bool hasLocalChanges = status.Contains("M");

            if (hasLocalChanges)
            {
                //UnityEngine.Debug.Log("执行 git stash");
                RunGitCommand("stash", folderPath);
            }


            string pullOutput = RunGitCommand($"pull origin {currentBranch}", folderPath);
            //UnityEngine.Debug.Log("拉取结果: " + pullOutput);

            //获取本地和远程文件差异
            List<string> remoteChangedFiles = GetRemoteChangedFiles(pullOutput);

            EditorApplication.delayCall += () =>
            {
                foreach (var file in conflictedFiles)
                {
                    bool shouldOverwrite = EditorUtility.DisplayDialog(
                        "文件冲突",
                        $"远端文件 '{file}' 和本地文件有冲突，是否覆盖本地文件?",
                        "覆盖",
                        "保留本地");

                    if (shouldOverwrite)
                    {
                        string checkoutOutput = RunGitCommand($"checkout -- {file}", folderPath);
                        UnityEngine.Debug.Log($"已覆盖本地文件: {file}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"保留本地文件: {file}");
                    }
                }
            };



            //如果有本地修改且执行了git stash恢复本地修改
            if (hasLocalChanges)
            {
                // 检查是否有stash
                string stashList = RunGitCommand("stash list", folderPath);

                if (!string.IsNullOrEmpty(stashList))
                {
                    try
                    {
                        RunGitCommand("stash pop", folderPath);
                        UnityEngine.Debug.Log("已恢复本地修改");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"恢复本地修改失败: {ex.Message}");
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("没有本地修改的stash，跳过恢复操作");
                }
            }

            //获取本地仓库与远程仓库的差异
            string localCommitsAhead = RunGitCommand($"rev-list --count origin/{currentBranch}..HEAD", folderPath);

            if (int.Parse(localCommitsAhead) > 0)
            {
                UnityEngine.Debug.Log("检测到本地提交未推送到远程仓库，正在推送...");

                //推送本地的所有已提交的文件到远程仓库
                string pushOutput = RunGitCommand($"push origin {currentBranch}", folderPath);
                UnityEngine.Debug.Log("推送到远程仓库结果: " + pushOutput);
            }

            //拉取最新的文件列表
            LoadRepositoryFiles();
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("成功", "已成功拉取并推送未推送的文件！", "确定");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            UnityEngine.Debug.LogError($"拉取并推送文件失败: {ex.Message}");
            EditorUtility.DisplayDialog("错误", $"拉取并推送文件失败: {ex.Message}", "确定");
        }
    }


    private List<string> GetRemoteChangedFiles(string pullOutput)
    {
        //获取被修改的文件列表
        var changedFiles = new List<string>();

        if (!string.IsNullOrEmpty(pullOutput))
        {
            var lines = pullOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("Updating"))
                {
                    changedFiles.Add(line.Trim());
                }
            }
        }

        return changedFiles;
    }

    private string RunGitCommand(string command, string workingDirectory)
    {
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
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Git命令执行失败: {error}");
            }
            return output.Trim();
        }
    }

    //获取冲突的文件
    private List<string> GetConflictedFiles(string status)
    {
        List<string> conflictedFiles = new List<string>();

        foreach (var line in status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("UU"))
            {
                string conflictedFile = line.Substring(3).Trim().Replace("\\", "/");
                conflictedFiles.Add(conflictedFile);
            }
        }

        return conflictedFiles;
    }

    private void LoadRepositoryFiles()
    {
        try
        {
            RunGitCommand("fetch", folderPath);

            string remoteFiles = RunGitCommand($"ls-tree -r origin/{currentBranch} --name-only Assets", folderPath);
            repositoryFiles = new List<string>(remoteFiles.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            selectedFiles.Clear();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"加载文件列表失败: {ex.Message}");
        }
    }

}