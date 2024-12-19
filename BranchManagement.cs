using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Text;



public class BranchManagement : EditorWindow
{
    private List<string> remoteBranches = new List<string>();
    private List<string> localBranches = new List<string>();
    private List<string> missingBranches = new List<string>();
    private List<string> remoteTags = new List<string>();
    private List<string> localTags = new List<string>();

    private Vector2 scrollPosition;

    //private bool fetchAllBranches = false;
    //private bool fetchMissingBranchesOnly = false;
    private bool fetchAllTags = false;

    private Dictionary<string, bool> branchSelectionState = new Dictionary<string, bool>();


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

        // 初始化复选框状态字典
        branchSelectionState.Clear();
        foreach (string branch in missingBranches)
        {
            branchSelectionState[branch] = false;
        }

    }

    private void FetchSingleBranch(string branch)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe", 
                Arguments = $"/c git checkout -b {branch} origin/{branch}", 
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false, 
                CreateNoWindow = true,  
                StandardOutputEncoding = Encoding.UTF8, 
                StandardErrorEncoding = Encoding.UTF8    
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"成功拉取分支: {branch}");
                    localBranches.Add(branch);
                }
                else
                {
                    UnityEngine.Debug.LogError($"拉取分支 {branch} 时出错: {process.StandardError.ReadToEnd()}");
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"拉取分支 {branch} 时发生错误: {ex.Message}");
        }

        UpdateBranchLists();
    }


    private string GetCurrentBranch()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe", // 使用 cmd.exe
                Arguments = "/c git symbolic-ref --short HEAD", // 通过 /c 执行 git 命令
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, // 设置标准输出编码为 UTF-8
                StandardErrorEncoding = Encoding.UTF8    // 设置标准错误编码为 UTF-8
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
                FileName = "cmd.exe", // 使用 cmd.exe
                Arguments = $"/c git {gitArgs}", // 通过 /c 执行 git 命令
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false, // 不使用 shell 执行
                CreateNoWindow = true,   // 不创建命令行窗口
                StandardOutputEncoding = Encoding.UTF8, // 设置标准输出编码为 UTF-8
                StandardErrorEncoding = Encoding.UTF8    // 设置标准错误编码为 UTF-8
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
        int totalBranches = remoteBranches.Count;
        int currentProgress = 0;

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

                
                EditorUtility.DisplayProgressBar("拉取分支", $"正在拉取分支: {branch}", (float)currentProgress / totalBranches);

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
                        continue;
                    }
                }

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

                
                currentProgress++;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"拉取并创建分支 {branch} 时发生错误: {ex.Message}");
            }
        }

        
        EditorUtility.ClearProgressBar();

        
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
        GUILayout.Label("分支管理", EditorStyles.boldLabel);

        
        Rect buttonRect = new Rect(position.width - 110, 10, 100, 30);
        if (GUI.Button(buttonRect, "刷新"))
        {
            UpdateBranchLists();
        }

        GUILayout.Space(10);
        GUILayout.Label("本地缺失的远端分支:", EditorStyles.boldLabel);

        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(position.height - 200));

        
        if (missingBranches.Count > 0)
        {
            foreach (string branch in missingBranches)
            {
                EditorGUILayout.BeginHorizontal();

                
                if (!branchSelectionState.ContainsKey(branch))
                {
                    branchSelectionState[branch] = false;
                }

                
                branchSelectionState[branch] = EditorGUILayout.Toggle(branchSelectionState[branch], GUILayout.Width(20));

                
                GUILayout.Label(branch);

                EditorGUILayout.EndHorizontal();

                
                GUILayout.Space(10);
            }
        }
        else
        {
            GUILayout.Label("没有缺失的远端分支。");
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(20);

        
        fetchAllTags = EditorGUILayout.Toggle("获取所有标签", fetchAllTags);

        GUILayout.Space(10);

        
        if (GUILayout.Button("拉取选中分支", GUILayout.Height(30)))
        {
            
            var selectedBranches = branchSelectionState.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

           
            if (selectedBranches.Count > 0)
            {
                
                EditorUtility.DisplayProgressBar("拉取分支", "正在拉取分支...", 0f);

                int branchCount = selectedBranches.Count;
                for (int i = 0; i < branchCount; i++)
                {
                    
                    EditorUtility.DisplayProgressBar("拉取分支", $"正在拉取：{selectedBranches[i]}", (float)(i + 1) / branchCount);

                    FetchSingleBranch(selectedBranches[i]);
                }

                
                EditorUtility.ClearProgressBar();
            }

            
            if (fetchAllTags)
            {
                EditorUtility.DisplayProgressBar("获取标签", "正在获取所有标签...", 0f);
                FetchAllTags();
                EditorUtility.ClearProgressBar();
            }
        }
    }

}
