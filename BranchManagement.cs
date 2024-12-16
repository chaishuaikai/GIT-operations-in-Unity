using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BranchManagement : EditorWindow
{
    private List<string> remoteBranches = new List<string>();
    private List<string> localBranches = new List<string>();
    private List<string> missingBranches = new List<string>();
    private List<string> remoteTags = new List<string>();
    private List<string> localTags = new List<string>();

    private Vector2 scrollPosition;

    private bool fetchAllBranches = false;
    private bool fetchMissingBranchesOnly = false;
    private bool fetchAllTags = false;

    [MenuItem("GIT/拉取分支")]
    public static void ShowWindow()
    {
        BranchManagement window = GetWindow<BranchManagement>("分支管理");
        if (window != null)
        {
            window.UpdateBranchLists();
        }
    }
    private void UpdateBranchLists()
    {
        remoteBranches = LoadBranches("branch -r");
        localBranches = LoadBranches("branch");    
        remoteTags = LoadBranches("tag -l"); 
        localTags = LoadBranches("tag");     


        string currentBranch = GetCurrentBranch();
        if (!string.IsNullOrEmpty(currentBranch) && localBranches.Contains(currentBranch))
        {
            localBranches.Remove(currentBranch);
        }


        missingBranches = remoteBranches
            .Select(branch => branch.Replace("origin/", "").Trim()) 
            .Where(branch => !localBranches.Contains(branch) && branch != currentBranch) 
            .ToList();

    }
 
    private string GetCurrentBranch()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "symbolic-ref --short HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string currentBranch = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return currentBranch;
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"获取当前分支时发生错误: {ex.Message}");
            return string.Empty;
        }
    }

    private List<string> LoadBranches(string gitArgs)
    {
        List<string> branches = new List<string>();
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = gitArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                StreamReader outputReader = process.StandardOutput;
                while (!outputReader.EndOfStream)
                {
                    string branch = outputReader.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(branch))
                    {
                        branches.Add(branch);
                    }
                }

                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"Git 命令执行失败: {process.StandardError.ReadToEnd()}");
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"获取分支或标签时发生错误: {ex.Message}");
        }

        return branches;
    }

    
    private void FetchMissingBranches()
    {
        foreach (string branch in missingBranches)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"checkout -b {branch} origin/{branch}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"拉取分支 {branch} 时出错: {process.StandardError.ReadToEnd()}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"拉取分支 {branch} 时发生错误: {ex.Message}");
            }
        }

        
        UpdateBranchLists();
    }
    private void FetchAllBranches()
    {
        foreach (string branch in remoteBranches)
        {
            try
            {
                if (branch.Equals("origin/HEAD"))
                {
                    continue;
                }

                string localBranch = branch.Replace("origin/", "").Trim();

                if (localBranches.Contains(localBranch))
                {
                    UnityEngine.Debug.Log($"分支 {localBranch} 已存在，跳过拉取。");
                    continue;
                }

                // 这里输出每个远程分支的信息
                UnityEngine.Debug.Log($"开始拉取远程分支: {branch}");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"checkout -b {localBranch} {branch}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"拉取分支 {branch} 时出错: {process.StandardError.ReadToEnd()}");
                        continue;
                    }
                }

                UnityEngine.Debug.Log($"分支 {localBranch} 拉取成功。");

                ProcessStartInfo setUpstreamInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"branch --set-upstream-to={branch} {localBranch}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = setUpstreamInfo })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"设置本地分支 {localBranch} 与远程分支 {branch} 关联时出错: {process.StandardError.ReadToEnd()}");
                    }
                }

                missingBranches.Remove(localBranch);
                localBranches.Add(localBranch);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"拉取并创建分支 {branch} 时发生错误: {ex.Message}");
            }
        }

        UpdateBranchLists();
    }


    private void FetchAllTags()
    {
        foreach (string tag in remoteTags)
        {
            try
            {
                if (!localTags.Contains(tag))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = $"checkout tags/{tag} -b {tag}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process process = new Process { StartInfo = startInfo })
                    {
                        process.Start();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            UnityEngine.Debug.LogError($"拉取标签 {tag} 时出错: {process.StandardError.ReadToEnd()}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"拉取标签 {tag} 时发生错误: {ex.Message}");
            }
        }

        
        UpdateBranchLists();
    }

    private void OnGUI()
    {
        GUILayout.Space(5);
        GUILayout.Label("分支管理工具", EditorStyles.boldLabel);

       
        Rect buttonRect = new Rect(position.width - 110, 10, 100, 30);
        if (GUI.Button(buttonRect, "刷新分支列表"))
        {
            UpdateBranchLists();
        }

        GUILayout.Space(20);

        GUILayout.Space(10);

        GUILayout.Label("本地缺失的远端分支:", EditorStyles.boldLabel);

        GUILayout.Space(10);
      
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(position.height - 300));

        
        if (missingBranches.Count > 0)
        {
            foreach (string branch in missingBranches)
            {
                
                GUILayout.Label(branch);
            }
        }
        else
        {
            
            GUILayout.Label("没有缺失的远端分支。");
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(20);

        fetchAllBranches = EditorGUILayout.Toggle("从远端拉取所有分支", fetchAllBranches);
        fetchAllTags = EditorGUILayout.Toggle("获取所有标签", fetchAllTags);

       
        if (GUILayout.Button("获取", GUILayout.Height(30)))
        {
            if (fetchAllBranches)
            {
                FetchAllBranches();
            }
            if (fetchAllTags)
            {
                FetchAllTags();
            }
            if (!fetchAllBranches && !fetchAllTags)
            {
                EditorUtility.DisplayDialog("提示", "请勾选一个选项来获取分支或标签。", "OK");
            }
        }
    }

}
