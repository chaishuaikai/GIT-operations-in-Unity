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
        //获取提交者的信息
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
        process.WaitForExit();
        Repaint(); 
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("刷新", GUILayout.Width(70), GUILayout.Height(30)))
        {
            FetchGitLogAsync();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("Git Log", EditorStyles.boldLabel);

        if (string.IsNullOrEmpty(gitLog))
        {
            GUILayout.Label("No logs found.");
        }
        else
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            GUILayout.BeginVertical();
            string[] logs = gitLog.Split('\n');

            for (int i = 0; i < logs.Length; i++)
            {
                string[] logParts = logs[i].Split(new[] { ' ' }, 4); 
                if (logParts.Length == 4)
                {
                    string commitHash = logParts[0];
                    string author = logParts[1];
                    string description = logParts[2];
                    string time = logParts[3];

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(commitHash, GUILayout.Width(position.width * 0.2f));
                    GUILayout.Label(author, GUILayout.Width(position.width * 0.2f));
                    GUILayout.Label(description, GUILayout.Width(position.width * 0.4f)); 
                    GUILayout.Label(time, GUILayout.ExpandWidth(true)); 

                    GUILayout.EndHorizontal();

                    if (i == selectedLogIndex)
                    {
                        GUI.contentColor = Color.blue;
                    }
                    else
                    {
                        GUI.contentColor = Color.white;
                    }

                    if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) &&
                        Event.current.type == EventType.MouseDown)
                    {
                        selectedLogIndex = i;
                        Repaint();
                    }

                    GUI.contentColor = Color.white;

                    GUILayout.Space(5);
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }
    }
}
