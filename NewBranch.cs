using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;


public class NewBranch : EditorWindow
{
    private string currentBranch = "未能获取当前分支";
    private string newBranchName = "";
    private string createBranchFeedback = "";

    private List<string> localBranches = new List<string>();
    private List<string> remoteBranches = new List<string>();

    private Vector2 scrollPosition;
    private List<bool> localBranchSelection = new List<bool>();
    private List<bool> remoteBranchSelection = new List<bool>();

    private bool forceDeleteBranch = false;


    [MenuItem("GIT/创建和删除分支")]
    public static void ShowWindow()
    {
        NewBranch window = GetWindow<NewBranch>("分支");
        if (window != null)
        {
            window.LoadBranches();
        }
    }

    public void LoadBranches()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                if (process != null)
                {
                    currentBranch = process.StandardOutput.ReadLine()?.Trim() ?? "未知分支";
                    UnityEngine.Debug.Log($"当前分支: {currentBranch}");
                }
            }


            ProcessStartInfo localBranchesPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "branch",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(localBranchesPsi))
            {
                if (process != null)
                {
                    string localBranchesOutput = process.StandardOutput.ReadToEnd();
                    localBranches = new List<string>(localBranchesOutput.Split('\n'));
                    localBranches.RemoveAll(branch => string.IsNullOrWhiteSpace(branch));
                    // UnityEngine.Debug.Log($"本地分支: {string.Join(", ", localBranches)}");
                }
            }

            ProcessStartInfo remoteBranchesPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "branch -r",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(remoteBranchesPsi))
            {
                if (process != null)
                {
                    string remoteBranchesOutput = process.StandardOutput.ReadToEnd();
                    remoteBranches = new List<string>(remoteBranchesOutput.Split('\n'));
                    remoteBranches.RemoveAll(branch => string.IsNullOrWhiteSpace(branch));

                    // UnityEngine.Debug.Log($"远程分支: {string.Join(", ", remoteBranches)}");
                }
            }


            localBranchSelection.Clear();
            remoteBranchSelection.Clear();
            for (int i = 0; i < localBranches.Count; i++)
            {
                localBranchSelection.Add(false);
            }

            for (int i = 0; i < remoteBranches.Count; i++)
            {
                remoteBranchSelection.Add(false);
            }


            newBranchName = "";
        }
        catch (System.Exception ex)
        {
            currentBranch = $"错误: {ex.Message}";
            UnityEngine.Debug.LogError($"加载分支时发生错误: {ex.Message}");
        }
    }


    private void OnGUI()
    {
        GUILayout.Space(5);
        GUILayout.Label("Git 分支管理", EditorStyles.boldLabel);

        GUILayout.Space(5);

        DisplayCurrentBranch(currentBranch);

        GUILayout.Space(10);
        GUILayout.Label("创建新分支", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("分支名称 :", GUILayout.Width(70));
        newBranchName = GUILayout.TextField(newBranchName, GUILayout.Height(30));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        Rect Buttons = new Rect(position.width - 110, 130, 100, 30);
        if (GUI.Button(Buttons, "创建分支"))
        {
            CreateNewBranch(newBranchName);
        }

        if (!string.IsNullOrEmpty(createBranchFeedback))
        {
            GUILayout.Space(10);
            GUILayout.Label(createBranchFeedback, EditorStyles.wordWrappedLabel);
        }

        GUILayout.Space(10);

        Rect buttonRect = new Rect(position.width - 110, 10, 100, 30);
        if (GUI.Button(buttonRect, "刷新"))
        {
            LoadBranches();
        }

        GUILayout.Space(10);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        GUILayout.Label("本地分支", EditorStyles.boldLabel);

        for (int i = 0; i < localBranches.Count; i++)
        {
            GUILayout.BeginHorizontal();
            bool isSelected = GUILayout.Toggle(localBranchSelection.Count > i ? localBranchSelection[i] : false, "", GUILayout.Width(20)); // 使用记录的状态
            if (localBranchSelection.Count <= i) localBranchSelection.Add(isSelected);
            else localBranchSelection[i] = isSelected;
            GUILayout.Label(localBranches[i].Trim(), EditorStyles.label);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        GUILayout.Label("远程分支", EditorStyles.boldLabel);

        for (int i = 0; i < remoteBranches.Count; i++)
        {
            GUILayout.BeginHorizontal();
            bool isSelected = GUILayout.Toggle(remoteBranchSelection.Count > i ? remoteBranchSelection[i] : false, "", GUILayout.Width(20)); // 使用记录的状态
            if (remoteBranchSelection.Count <= i) remoteBranchSelection.Add(isSelected);
            else remoteBranchSelection[i] = isSelected;
            GUILayout.Label(remoteBranches[i].Trim(), EditorStyles.label);
            GUILayout.EndHorizontal();
        }


        GUILayout.EndScrollView();

        GUILayout.Space(15);

        GUILayout.BeginHorizontal();
        forceDeleteBranch = GUILayout.Toggle(forceDeleteBranch, "忽略合并状态强制删除", GUILayout.Width(250));
        GUILayout.EndHorizontal();


        Rect ButtonsDel = new Rect(position.width - 110, 400, 100, 30);
        if (GUI.Button(ButtonsDel, "删除选中的分支"))
        {
            DeleteSelectedBranches();
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

    private void CreateNewBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
        {
            createBranchFeedback = "分支名称不能为空！";
            Repaint();
            return;
        }

        try
        {

            ProcessStartInfo createBranchPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"checkout -b {branchName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process createProcess = Process.Start(createBranchPsi))
            {
                if (createProcess != null)
                {
                    string createOutput = createProcess.StandardOutput.ReadToEnd();
                    string createError = createProcess.StandardError.ReadToEnd();
                    createProcess.WaitForExit();

                    if (createOutput.Contains("Switched to a new branch"))
                    {
                        UnityEngine.Debug.Log($"成功创建本地分支：{branchName}");
                        createBranchFeedback = $"成功创建本地分支：{branchName}";
                    }
                    else
                    {
                        Repaint();
                    }
                }
            }

            ProcessStartInfo pushBranchPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"push --set-upstream origin {branchName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process pushProcess = Process.Start(pushBranchPsi))
            {
                if (pushProcess != null)
                {
                    string pushOutput = pushProcess.StandardOutput.ReadToEnd();
                    string pushError = pushProcess.StandardError.ReadToEnd();
                    pushProcess.WaitForExit();

                    if (pushOutput.Contains("Branch") || pushOutput.Contains("new branch"))
                    {
                        UnityEngine.Debug.Log($"成功推送并设置远程分支：{branchName}");
                        createBranchFeedback = $"成功推送并设置远程分支：{branchName}";
                    }
                    else
                    {
                        Repaint();
                    }
                }
            }

            LoadBranches();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"创建分支时发生错误：{ex.Message}");
            createBranchFeedback = $"创建分支时发生错误：{ex.Message}";
            Repaint();
        }
    }


    private void DeleteSelectedBranches()
    {
        List<int> deletedIndexes = new List<int>();

        //删除本地分支
        for (int i = 0; i < localBranchSelection.Count; i++)
        {
            if (localBranchSelection[i])
            {
                string branchToDelete = localBranches[i].Trim();

                if (branchToDelete != currentBranch)
                {
                    try
                    {
                        if (branchToDelete.StartsWith("origin/"))
                        {
                            string deleteRemoteBranchArgs = $"push origin --delete {branchToDelete.Substring(7)}";
                            ExecuteGitCommand(deleteRemoteBranchArgs, $"成功删除远程分支：{branchToDelete}");
                            ExecuteGitCommand("fetch --prune", "成功更新远程分支信息");
                        }
                        else
                        {
                            string deleteBranchArgs = forceDeleteBranch ? $"branch -D {branchToDelete}" : $"branch -d {branchToDelete}";
                            ExecuteGitCommand(deleteBranchArgs, $"成功删除本地分支：{branchToDelete}");
                        }

                        deletedIndexes.Add(i);
                    }
                    catch (System.Exception ex)
                    {
                        createBranchFeedback = $"删除分支时发生错误：{ex.Message}";
                    }
                }
                else
                {
                    createBranchFeedback = "不能删除当前分支！";
                }
            }
        }

        for (int i = 0; i < remoteBranchSelection.Count; i++)
        {
            if (remoteBranchSelection[i])
            {
                string remoteBranchToDelete = remoteBranches[i].Trim();
                string remoteBranchName = remoteBranchToDelete.StartsWith("origin/") ? remoteBranchToDelete.Substring(7) : remoteBranchToDelete; // 去掉 "origin/" 前缀

                try
                {
                    string deleteRemoteBranchArgs = $"push origin --delete {remoteBranchName}";
                    ExecuteGitCommand(deleteRemoteBranchArgs, $"成功删除远程分支：{remoteBranchName}");
                    ExecuteGitCommand("fetch --prune", "成功更新远程分支信息");

                    deletedIndexes.Add(i);
                }
                catch (System.Exception ex)
                {
                    createBranchFeedback = $"删除远程分支时发生错误：{ex.Message}";
                }
            }
        }

        foreach (var index in deletedIndexes.OrderByDescending(x => x).ToList())
        {
            if (index < localBranches.Count)
            {
                localBranches.RemoveAt(index);
                localBranchSelection.RemoveAt(index);
            }

            if (index < remoteBranchSelection.Count)
            {
                remoteBranches.RemoveAt(index);
                remoteBranchSelection.RemoveAt(index);
            }
        }

        Repaint();
    }

    private void ExecuteGitCommand(string arguments, string successMessage)
    {
        
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe", 
            Arguments = $"/c git {arguments}", 
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, 
            CreateNoWindow = true,   
            StandardOutputEncoding = Encoding.UTF8, 
            StandardErrorEncoding = Encoding.UTF8    
        };

        try
        {
            using (Process process = Process.Start(psi))
            {
                if (process == null)
                {
                    UnityEngine.Debug.LogError("Git 进程启动失败");
                    return;
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log(successMessage);
                    createBranchFeedback = successMessage;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Git 命令执行失败: {error}");
                    createBranchFeedback = $"Git 命令执行失败：{error}";
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"执行 Git 命令失败: {ex.Message}");
            createBranchFeedback = $"Git 命令执行失败：{ex.Message}";
        }
    }
}
