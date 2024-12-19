using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public class GitLog : EditorWindow
{
    private string gitLog = "";
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedLogIndex = -1;

    [MenuItem("GIT/查看日志")]
    public static void ShowWindow()
    {
        GitLog window = GetWindow<GitLog>("查看日志");
        window.Show();
    }

    private async void OnEnable()
    {
        await FetchGitLogAsync();
    }

    private async Task FetchGitLogAsync()
    {
        // 获取提交者的信息
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "log --pretty=format:\"%h %an %s %ci\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new Process { StartInfo = startInfo };
        process.Start();

        StringBuilder logBuilder = new StringBuilder();
        string line;
        while ((line = await process.StandardOutput.ReadLineAsync()) != null)
        {
            logBuilder.AppendLine(line);
        }

        string error = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrEmpty(error))
        {
            logBuilder.AppendLine("Error: " + error);
        }

        gitLog = logBuilder.ToString();

        // 捕获进程退出状态
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"Git 命令执行失败，退出代码: {process.ExitCode}");
        }

        Repaint(); // 更新 UI
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        Rect buttonRect = new Rect(position.width - 110, 10, 100, 30);

        if (GUI.Button(buttonRect,"刷新"))
        {
            _ = FetchGitLogAsync();  // 刷新日志
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(30);
        GUILayout.Label("Git Log", EditorStyles.boldLabel);

        if (string.IsNullOrEmpty(gitLog))
        {
            GUILayout.Label("No logs found.");
        }
        else
        {
            string[] logs = gitLog.Split('\n');

            // 计算可见区域
            float itemHeight = 30f; // 每行高度
            int visibleCount = Mathf.CeilToInt(position.height / itemHeight); // 可见区域内最多显示行数
            int totalCount = logs.Length;

            // 滚动条处理
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            Rect scrollArea = GUILayoutUtility.GetRect(position.width, totalCount * itemHeight);
            float scrollY = scrollPosition.y;
            int firstVisibleIndex = Mathf.FloorToInt(scrollY / itemHeight);
            int lastVisibleIndex = Mathf.Min(firstVisibleIndex + visibleCount, totalCount - 1);

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                // 解析日志
                string[] logParts = logs[i].Split(new[] { ' ' }, 4);
                if (logParts.Length == 4)
                {
                    string commitHash = logParts[0];
                    string author = logParts[1];
                    string description = logParts[2];
                    string time = logParts[3];

                    // 绘制行
                    Rect itemRect = new Rect(0, i * itemHeight, position.width, itemHeight);
                    GUI.BeginGroup(itemRect);

                    GUI.Label(new Rect(10, 5, position.width * 0.2f, itemHeight), commitHash);
                    GUI.Label(new Rect(position.width * 0.2f, 5, position.width * 0.2f, itemHeight), author);
                    GUI.Label(new Rect(position.width * 0.4f, 5, position.width * 0.4f, itemHeight), description);
                    GUI.Label(new Rect(position.width * 0.8f, 5, position.width * 0.2f, itemHeight), time);

                    // 鼠标点击选择
                    if (itemRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        selectedLogIndex = i;
                        Repaint();
                    }

                    GUI.EndGroup();
                }
            }

            GUILayout.EndScrollView();
        }
    }
}
