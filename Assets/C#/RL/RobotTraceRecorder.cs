using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RobotTraceRecorder : MonoBehaviour
{
    private List<string> logs = new List<string>();
    private EnvControl env;
    private float timer = 0f;
    private RobotControl robotControl;

    void Start()
    {
        env = FindObjectOfType<EnvControl>();
        robotControl = GetComponent<RobotControl>();

        // CSV Header
        logs.Add("Episode,RobotID,Time,X,Z,FollowerCount");
    }

    void FixedUpdate()
    {
        // 只在测试模式下记录，避免训练时卡顿
        if (env != null && env.isTest)
        {
            timer += Time.fixedDeltaTime;
            // 每 0.5 秒记录一次 (频率可调)
            if (timer >= 0.5f)
            {
                timer = 0f;
                RecordFrame();
            }
        }
    }

    void RecordFrame()
    {
        // 获取数据
        int ep = env.EpisodeNum;
        string id = gameObject.name; // Robot1, Robot2...
        float t = env.EnpisodeTime;
        float x = transform.position.x;
        float z = transform.position.z;
        int followers = robotControl != null ? robotControl.myDirectFollowers.Count : 0;

        // 格式: Episode,RobotID,Time,X,Z,FollowerCount
        string line = $"{ep},{id},{t:F2},{x:F2},{z:F2},{followers}";
        logs.Add(line);
    }

    void OnDestroy()
    {
        // 游戏结束/物体销毁时保存文件
        if (logs.Count > 1) // 大于1是因为有一行Header
        {
            SaveToFile();
        }
    }

    void SaveToFile()
    {
        // 文件名包含模式和时间，防止覆盖
        // 例如: Trajectory_Case3_Robot1_1027_1030.csv
        string modeName = env.currentExperimentMode.ToString();
        string filename = $"Trajectory_{modeName}_{gameObject.name}_{System.DateTime.Now:MMdd_HHmm}.csv";

        string path;
#if UNITY_EDITOR
        path = Path.Combine(Directory.GetCurrentDirectory(), "TestData_RobotTrace", filename);
#else
        path = Path.Combine(Application.persistentDataPath, filename);
#endif

        // 确保目录存在
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        File.WriteAllLines(path, logs, Encoding.UTF8);
        Debug.Log($"[Trajectory] 轨迹已保存: {filename}");
    }
}